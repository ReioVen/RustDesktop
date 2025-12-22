using RustDesktop.Core.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RustDesktop.Core.Services;

/// <summary>
/// FCM-based pairing listener using Node.js and rustplus-cli (like working version)
/// This uses the rustplus.js CLI tool to listen for FCM notifications
/// </summary>
public class PairingListenerRealProcess : IPairingListener
{
    public event EventHandler<PairingPayload>? Paired;
    public event EventHandler? Listening;
    public event EventHandler? Stopped;
    public event EventHandler<string>? Failed;
    public event EventHandler<AlarmNotification>? AlarmReceived;
    public event EventHandler<TeamChatMessage>? ChatReceived;

    private static readonly Regex Ansi = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex RustUrl = new(@"rustplus://[^\s'\"">]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex KvLine = new(@"\{\s*key:\s*'(?<k>[^']+)'\s*,\s*value:\s*'(?<v>.*)'\s*\}", RegexOptions.Compiled);
    private static readonly Regex BodyJson = new(@"value:\s*(?:'|`)(?<json>\{.*?\})(?:'|`)", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex MsgLine = new(@"\{\s*key:\s*'(?:message|gcm\.notification\.body)'\s*,\s*value:\s*'(?<msg>[^']+)'\s*\}", RegexOptions.Compiled);

    private readonly Action<string> _log;
    private CancellationTokenSource? _cts;
    private Process? _listenProc;
    private volatile bool _running;
    private string? _lastPairKey;
    private DateTime _lastPairAt;

    private (string? server, string? entityName, uint? entityId)? _pendingAlarm;
    private string? _pendingAlarmMsg;
    private DateTime? _pendingAlarmMsgTs;

    private bool _chatBundleOpen;
    private string? _pendingChatMsg;
    private string? _pendingChatTitle;
    private DateTime? _pendingChatTs;

    public bool IsRunning => _running;

    private string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RustDesktop", "rustplusjs-config.json");

    public PairingListenerRealProcess(Action<string> log)
    {
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _log("Starting FCM pairing listener...");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (_running && _listenProc != null && !_listenProc.HasExited)
        {
            _log("Listener already running.");
            return;
        }

        // Try to find Node.js (system or bundled)
        var node = FindNode()
            ?? throw new InvalidOperationException("Node.js not found. Please install Node.js or bundle it with the app.");

        // Try to resolve rustplus-cli (via npx or bundled)
        var (cli, workingDir) = ResolveRustplusCli()
            ?? throw new InvalidOperationException("rustplus-cli not found. Please install @liamcottle/rustplus.js or bundle rustplus-cli.");

        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

        // Detect if we should run via npx (preferred)
        bool useNpx = string.Equals(node, "npx", StringComparison.OrdinalIgnoreCase) ||
                      node.EndsWith("npx.exe", StringComparison.OrdinalIgnoreCase) ||
                      cli.StartsWith("@liamcottle", StringComparison.OrdinalIgnoreCase);
        
        // If we need npx but node is not npx, use npx instead
        string executable = useNpx && !string.Equals(node, "npx", StringComparison.OrdinalIgnoreCase) && !node.EndsWith("npx.exe", StringComparison.OrdinalIgnoreCase)
            ? "npx"
            : node;

        // 1) Register if config doesn't exist
        if (!File.Exists(ConfigPath) || new FileInfo(ConfigPath).Length < 50)
        {
            _log("Starting one-time FCM registration (fcm-register)...");
            try
            {
                // If using npx, use package name; otherwise use file path
                var args = useNpx
                    ? $"-y @liamcottle/rustplus.js fcm-register --config-file=\"{ConfigPath}\""
                    : $"\"{cli}\" fcm-register --config-file=\"{ConfigPath}\"";
                
                await RunCliAsync(
                    executable,
                    args,
                    useNpx ? null : workingDir,
                    _cts.Token
                );
                _log("Registration completed. Please confirm login in browser if applicable.");
            }
            catch (Exception ex)
            {
                _log($"Registration failed: {ex.Message}");
                Failed?.Invoke(this, $"Registration failed: {ex.Message}");
                throw;
            }
        }

        // 2) Start FCM listener
        _log("Starting FCM listener (fcm-listen)...");
        // If using npx, use package name; otherwise use file path
        var listenArgs = useNpx
            ? $"-y @liamcottle/rustplus.js fcm-listen --config-file=\"{ConfigPath}\""
            : $"\"{cli}\" fcm-listen --config-file=\"{ConfigPath}\"";
        
        _listenProc = StartProcess(
            executable,
            listenArgs,
            useNpx ? null : workingDir,
            HandleListenOutput,
            s => _log("[fcm-listen:err] " + s),
            noWindow: true
        );

        _running = true;
        _listenProc.EnableRaisingEvents = true;
        _listenProc.Exited += async (_, __) =>
        {
            _running = false;
            Stopped?.Invoke(this, EventArgs.Empty);
            if (_cts is null || _cts.IsCancellationRequested) return;
            _log("FCM listener exited - restarting in 3s...");
            try
            {
                await Task.Delay(3000, _cts.Token);
                if (_cts is not null && !_cts.IsCancellationRequested)
                    await StartAsync(_cts.Token);
            }
            catch { /* ignore */ }
        };
    }

    public Task StopAsync()
    {
        try { _listenProc?.Kill(entireProcessTree: true); } catch { }
        _listenProc?.Dispose();
        _listenProc = null;
        _cts?.Cancel();
        _cts = null;

        var wasRunning = _running;
        _running = false;
        if (wasRunning) Stopped?.Invoke(this, EventArgs.Empty);

        _log("FCM listener stopped.");
        return Task.CompletedTask;
    }

    private void HandleListenOutput(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var s = Ansi.Replace(line, "").Trim();

        // Status markers
        if (s.IndexOf("Listening for FCM Notifications", StringComparison.OrdinalIgnoreCase) >= 0)
            Listening?.Invoke(this, EventArgs.Empty);

        if (s.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
            s.IndexOf("ERR!", StringComparison.OrdinalIgnoreCase) >= 0)
            Failed?.Invoke(this, s);

        // Parse rustplus:// URLs
        var urlMatch = RustUrl.Match(s);
        if (urlMatch.Success && TryParseRustPlusUrl(urlMatch.Value, out var urlPayload) && urlPayload != null)
        {
            Paired?.Invoke(this, urlPayload);
            _log($"Pairing (via rustplus://) → {urlPayload.Host}:{urlPayload.Port}");
            return;
        }

        // Parse key/value lines
        var kv = KvLine.Match(s);
        if (kv.Success)
        {
            var k = kv.Groups["k"].Value;
            var v = kv.Groups["v"].Value;

            // Chat bundle handling
            if (k.Equals("gcm.notification.android_channel_id", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("channelId", StringComparison.OrdinalIgnoreCase))
            {
                _chatBundleOpen = v.Equals("chat", StringComparison.OrdinalIgnoreCase);
                if (!_chatBundleOpen)
                {
                    _pendingChatMsg = null;
                    _pendingChatTitle = null;
                    _pendingChatTs = null;
                }
            }

            if (k.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("gcm.notification.title", StringComparison.OrdinalIgnoreCase))
            {
                if (_chatBundleOpen)
                {
                    _pendingChatTitle = v;
                    TryFlushChat();
                    return;
                }
            }
        }

        // Parse message/body lines
        var msgMatch = MsgLine.Match(s);
        if (msgMatch.Success)
        {
            if (_chatBundleOpen)
            {
                _pendingChatMsg = msgMatch.Groups["msg"].Value;
                _pendingChatTs = DateTime.Now;
                TryFlushChat();
                return;
            }
        }

        // Parse JSON body
        var jsonMatch = BodyJson.Match(s);
        if (jsonMatch.Success)
        {
            var json = jsonMatch.Groups["json"].Value;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                static string? J(JsonElement el, string name) =>
                    el.TryGetProperty(name, out var v) ? v.GetString() : null;

                var type = J(root, "type");

                // Chat messages
                if (string.Equals(type, "chat", StringComparison.OrdinalIgnoreCase))
                {
                    var author = J(root, "name") ?? J(root, "username") ?? "Team";
                    var text = J(root, "message") ?? _pendingChatMsg ?? "";
                    ChatReceived?.Invoke(this, new TeamChatMessage(DateTime.Now, author, text));
                    _pendingChatMsg = null;
                    _pendingChatTitle = null;
                    _pendingChatTs = null;
                    return;
                }

                // Server/Entity pairing
                string? host = J(root, "ip");
                string? portStr = J(root, "port");
                string? name = J(root, "name");
                string? playerId = J(root, "playerId");
                string? playerToken = J(root, "playerToken");
                string? entityIdStr = J(root, "entityId") ?? J(root, "entityID");
                string? entityName = J(root, "entityName");
                string? entityType = J(root, "entityType");

                if (!int.TryParse(portStr, out var port)) port = 28082;
                uint? entityId = uint.TryParse(entityIdStr, out var eid) ? eid : null;

                if (!string.IsNullOrWhiteSpace(host) &&
                    !string.IsNullOrWhiteSpace(playerId) &&
                    !string.IsNullOrWhiteSpace(playerToken) &&
                    (string.Equals(type, "server", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(type, "entity", StringComparison.OrdinalIgnoreCase)))
                {
                    var payload = new PairingPayload
                    {
                        Host = host!,
                        Port = port,
                        ServerName = string.IsNullOrWhiteSpace(name) ? null : name,
                        SteamId64 = playerId!,
                        PlayerToken = playerToken!,
                        EntityId = entityId,
                        EntityName = string.IsNullOrWhiteSpace(entityName) ? null : entityName,
                        EntityType = entityType ?? type
                    };

                    var key = $"{payload.Host}:{payload.Port}|{payload.SteamId64}|{payload.PlayerToken}|{payload.EntityId}";
                    if (_lastPairKey == key && (DateTime.UtcNow - _lastPairAt).TotalSeconds < 20)
                    {
                        _log("[fcm] duplicate pairing ignored.");
                        return;
                    }
                    _lastPairKey = key;
                    _lastPairAt = DateTime.UtcNow;

                    Paired?.Invoke(this, payload);
                    _log($"Pairing received → {(payload.ServerName ?? payload.Host)}:{payload.Port}");
                    return;
                }

                // Alarm notifications
                if (string.Equals(type, "alarm", StringComparison.OrdinalIgnoreCase))
                {
                    _pendingAlarm = (name, entityName, entityId);
                    if (_pendingAlarmMsg != null)
                    {
                        var ts = _pendingAlarmMsgTs ?? DateTime.Now;
                        AlarmReceived?.Invoke(this, new AlarmNotification(
                            ts,
                            name ?? "-",
                            (entityName ?? "Alarm") + (entityId.HasValue ? $"#{entityId}" : ""),
                            entityId,
                            _pendingAlarmMsg
                        ));
                        _pendingAlarm = null;
                        _pendingAlarmMsg = null;
                        _pendingAlarmMsgTs = null;
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                _log($"[fcm-listen] JSON parse error: {ex.Message}");
            }
        }

        // Handle alarm messages
        if (msgMatch.Success)
        {
            _pendingAlarmMsg = msgMatch.Groups["msg"].Value;
            _pendingAlarmMsgTs = DateTime.Now;

            if (_pendingAlarm != null)
            {
                AlarmReceived?.Invoke(this, new AlarmNotification(
                    _pendingAlarmMsgTs ?? DateTime.Now,
                    _pendingAlarm.Value.server ?? "-",
                    (_pendingAlarm.Value.entityName ?? "Alarm") + (_pendingAlarm.Value.entityId.HasValue ? $"#{_pendingAlarm.Value.entityId}" : ""),
                    _pendingAlarm.Value.entityId,
                    _pendingAlarmMsg ?? ""
                ));
                _pendingAlarm = null;
                _pendingAlarmMsg = null;
                _pendingAlarmMsgTs = null;
            }
        }
    }

    private void TryFlushChat()
    {
        if (!_chatBundleOpen || string.IsNullOrEmpty(_pendingChatMsg)) return;
        var author = string.IsNullOrWhiteSpace(_pendingChatTitle) ? "Team" : _pendingChatTitle!;
        ChatReceived?.Invoke(this, new TeamChatMessage(_pendingChatTs ?? DateTime.Now, author, _pendingChatMsg!));
        _pendingChatMsg = null;
        _pendingChatTitle = null;
        _pendingChatTs = null;
    }

    private static bool TryParseRustPlusUrl(string url, out PairingPayload? p)
    {
        p = null;
        try
        {
            var qIndex = url.IndexOf('?');
            if (qIndex < 0) return false;
            var query = url.Substring(qIndex + 1);

            string? ip = null, portStr = null, name = null, playerId = null, playerToken = null;

            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                var k = Uri.UnescapeDataString(kv[0]).ToLowerInvariant();
                var v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                switch (k)
                {
                    case "ip": ip = v; break;
                    case "host": ip = v; break;
                    case "port": portStr = v; break;
                    case "name": name = v; break;
                    case "playerid": playerId = v; break;
                    case "playertoken": playerToken = v; break;
                }
            }

            if (string.IsNullOrWhiteSpace(ip) ||
                string.IsNullOrWhiteSpace(playerId) ||
                string.IsNullOrWhiteSpace(playerToken))
                return false;

            if (!int.TryParse(portStr, out var port)) port = 28082;

            p = new PairingPayload
            {
                Host = ip!,
                Port = port,
                ServerName = string.IsNullOrWhiteSpace(name) ? null : name,
                SteamId64 = playerId!,
                PlayerToken = playerToken!
            };
            return true;
        }
        catch { return false; }
    }

    private static string? FindNode()
    {
        // 1) Try bundled Node.js (like working version)
        var bundled = Path.Combine(AppContext.BaseDirectory, "runtime", "node-win-x64", "node.exe");
        if (File.Exists(bundled))
        {
            // Check for bundled npx too
            var bundledNpx = Path.Combine(AppContext.BaseDirectory, "runtime", "node-win-x64", "npx.cmd");
            if (File.Exists(bundledNpx))
                return bundledNpx;
            return bundled;
        }

        // 2) Try npx directly (preferred, pulls package automatically)
        try
        {
            var psi = new ProcessStartInfo("npx", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(1000);
                if (p.ExitCode == 0)
                    return "npx"; // Can use npx to run rustplus-cli
            }
        }
        catch { }

        // 3) Try system Node.js and find npx
        try
        {
            var psi = new ProcessStartInfo("node", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(1000);
                if (p.ExitCode == 0)
                {
                    // Node.js found, try to find npx in same location
                    try
                    {
                        var nodePath = GetExecutablePath("node");
                        if (!string.IsNullOrEmpty(nodePath))
                        {
                            var nodeDir = Path.GetDirectoryName(nodePath);
                            if (!string.IsNullOrEmpty(nodeDir))
                            {
                                // Check for npx.cmd or npx.exe in same directory
                                var npxCmd = Path.Combine(nodeDir, "npx.cmd");
                                var npxExe = Path.Combine(nodeDir, "npx.exe");
                                if (File.Exists(npxCmd))
                                    return npxCmd;
                                if (File.Exists(npxExe))
                                    return npxExe;
                            }
                        }
                    }
                    catch { }
                    
                    // Fallback: try npx via where command (Windows)
                    try
                    {
                        var wherePsi = new ProcessStartInfo("where", "npx")
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };
                        using var whereP = Process.Start(wherePsi);
                        if (whereP != null)
                        {
                            whereP.WaitForExit(1000);
                            if (whereP.ExitCode == 0)
                            {
                                var output = whereP.StandardOutput.ReadToEnd().Trim();
                                if (!string.IsNullOrEmpty(output) && File.Exists(output))
                                    return output;
                            }
                        }
                    }
                    catch { }
                    
                    return "node"; // Use system node (but npx might not work)
                }
            }
        }
        catch { }

        return null;
    }

    private static string? GetExecutablePath(string executable)
    {
        try
        {
            var psi = new ProcessStartInfo("where", executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(1000);
                if (p.ExitCode == 0)
                {
                    var output = p.StandardOutput.ReadToEnd().Trim();
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        var path = lines[0];
                        // On Windows, if we found npx (not .cmd), try to find npx.cmd in same directory
                        if (executable == "npx" && !path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            var dir = Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(dir))
                            {
                                var npxCmd = Path.Combine(dir, "npx.cmd");
                                if (File.Exists(npxCmd))
                                    return npxCmd;
                            }
                        }
                        return path;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static (string cli, string workingDir)? ResolveRustplusCli()
    {
        // 1) Try bundled rustplus-cli
        var bundledRoot = Path.Combine(AppContext.BaseDirectory, "runtime", "rustplus-cli");
        if (Directory.Exists(bundledRoot))
        {
            var pkgRoot = Path.Combine(bundledRoot, "node_modules", "@liamcottle", "rustplus.js");
            if (Directory.Exists(pkgRoot))
            {
                var cli = Path.Combine(pkgRoot, "cli", "index.js");
                if (File.Exists(cli))
                    return (cli, pkgRoot);
            }
        }

        // 2) Try extracting from zip
        var zipPath = Path.Combine(AppContext.BaseDirectory, "runtime", "rustplus-cli.zip");
        if (File.Exists(zipPath))
        {
            var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RustDesktop", "runtime", "rustplus-cli");
            Directory.CreateDirectory(target);

            var stamp = Path.Combine(target, ".stamp");
            var sig = $"{new FileInfo(zipPath).Length}-{File.GetLastWriteTimeUtc(zipPath).Ticks}";
            var need = !File.Exists(stamp) || (File.Exists(stamp) && File.ReadAllText(stamp) != sig)
                       || !Directory.Exists(Path.Combine(target, "node_modules"));

            if (need)
            {
                try { if (Directory.Exists(target)) Directory.Delete(target, true); } catch { }
                Directory.CreateDirectory(target);
                ZipFile.ExtractToDirectory(zipPath, target);
                File.WriteAllText(stamp, sig);
            }

            var pkgRoot = Path.Combine(target, "node_modules", "@liamcottle", "rustplus.js");
            if (Directory.Exists(pkgRoot))
            {
                var cli = Path.Combine(pkgRoot, "cli", "index.js");
                if (File.Exists(cli))
                    return (cli, pkgRoot);
            }
        }

        // 3) Use npx (will download on first use) - npx handles the package path
        // When using npx, we pass the package name, not the file path
        return ("@liamcottle/rustplus.js", Environment.CurrentDirectory);
    }

    private static Process StartProcess(
        string fileName, string args, string? workingDir,
        Action<string>? onOut, Action<string>? onErr, bool noWindow = true)
    {
        // On Windows, if fileName is "npx" or ends with "npx.cmd", we might need to use cmd.exe
        string actualFileName = fileName;
        string actualArgs = args;
        
        if (fileName == "npx" || fileName.EndsWith("npx.cmd", StringComparison.OrdinalIgnoreCase) || fileName.Contains("npx"))
        {
            // Try to find npx.cmd path (Windows needs .cmd)
            var npxPath = GetExecutablePath("npx");
            if (!string.IsNullOrEmpty(npxPath))
            {
                // If we got npx (not .cmd), try to find npx.cmd in same directory
                if (!npxPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) && !npxPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var dir = Path.GetDirectoryName(npxPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        var npxCmd = Path.Combine(dir, "npx.cmd");
                        if (File.Exists(npxCmd))
                            npxPath = npxCmd;
                    }
                }
                
                if (File.Exists(npxPath))
                {
                    actualFileName = npxPath;
                }
                else
                {
                    // Fallback: use cmd.exe to run npx
                    actualFileName = "cmd.exe";
                    actualArgs = $"/c \"npx {args}\"";
                }
            }
            else if (fileName == "npx")
            {
                // If npx is not found, use cmd.exe to run it
                actualFileName = "cmd.exe";
                actualArgs = $"/c \"npx {args}\"";
            }
        }
        
        var psi = new ProcessStartInfo(actualFileName, actualArgs)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = noWindow,
            WorkingDirectory = string.IsNullOrEmpty(workingDir) ? "" : workingDir
        };
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) onOut?.Invoke(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) onErr?.Invoke(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    private static async Task<int> RunCliAsync(
        string node, string args, string? workingDir, CancellationToken token)
    {
        // On Windows, if node is "npx" or ends with "npx.cmd", we might need to use cmd.exe
        string actualFileName = node;
        string actualArgs = args;
        
        if (node == "npx" || node.EndsWith("npx.cmd", StringComparison.OrdinalIgnoreCase) || node.Contains("npx"))
        {
            // Try to find npx.cmd path (Windows needs .cmd)
            var npxPath = GetExecutablePath("npx");
            if (!string.IsNullOrEmpty(npxPath))
            {
                // If we got npx (not .cmd), try to find npx.cmd in same directory
                if (!npxPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) && !npxPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var dir = Path.GetDirectoryName(npxPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        var npxCmd = Path.Combine(dir, "npx.cmd");
                        if (File.Exists(npxCmd))
                            npxPath = npxCmd;
                    }
                }
                
                if (File.Exists(npxPath))
                {
                    actualFileName = npxPath;
                }
                else
                {
                    // Fallback: use cmd.exe to run npx
                    actualFileName = "cmd.exe";
                    actualArgs = $"/c \"npx {args}\"";
                }
            }
            else if (node == "npx")
            {
                // If npx is not found, use cmd.exe to run it
                actualFileName = "cmd.exe";
                actualArgs = $"/c \"npx {args}\"";
            }
        }
        
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var psi = new ProcessStartInfo(actualFileName, actualArgs)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false, // Browser needs to open for registration
            WorkingDirectory = string.IsNullOrEmpty(workingDir) ? "" : workingDir
        };
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.WriteLine("[out] " + e.Data); };
        p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.WriteLine("[err] " + e.Data); };
        p.Exited += (_, __) => tcs.TrySetResult(p.ExitCode);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using (token.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } }))
            return await tcs.Task.ConfigureAwait(false);
    }
}







