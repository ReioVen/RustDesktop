using Microsoft.Win32;
using RustDesktop.Core.Models;
using System.Text.Json;
using System.Linq;
using System.IO;

namespace RustDesktop.Core.Services;

/// <summary>
/// Service to automatically detect Rust+ connection information from Rust game files/registry
/// </summary>
public class RustDataService : IRustDataService
{
    private readonly ILoggingService? _logger;
    
    public RustDataService(ILoggingService? logger = null)
    {
        _logger = logger;
    }
    
    public async Task<ServerInfo?> GetPairedServerAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger?.LogInfo("🔍 DEBUG: Starting GetPairedServerAsync...");
                
                // Try to get from registry first (Windows)
                _logger?.LogInfo("🔍 DEBUG: Checking Windows Registry for Server ID + Player Token...");
                var serverInfo = GetFromRegistry();
                if (serverInfo != null)
                {
                    _logger?.LogInfo($"✓✓✓ FOUND PAIRED SERVER IN REGISTRY!");
                    _logger?.LogInfo($"  Server ID: {serverInfo.ServerId}");
                    _logger?.LogInfo($"  Player Token: {serverInfo.PlayerToken?.Substring(0, Math.Min(20, serverInfo.PlayerToken?.Length ?? 0))}...");
                    return serverInfo;
                }
                _logger?.LogWarning("⚠ No paired server found in registry");

                // Try to get from game files
                _logger?.LogInfo("🔍 DEBUG: Checking game files for Server ID + Player Token...");
                serverInfo = GetFromGameFiles();
                if (serverInfo != null)
                {
                    _logger?.LogInfo($"✓✓✓ FOUND PAIRED SERVER IN GAME FILES!");
                    _logger?.LogInfo($"  Server ID: {serverInfo.ServerId}");
                    _logger?.LogInfo($"  Player Token: {serverInfo.PlayerToken?.Substring(0, Math.Min(20, serverInfo.PlayerToken?.Length ?? 0))}...");
                    return serverInfo;
                }
                _logger?.LogWarning("⚠ No paired server found in game files");
                
                // Also check our own saved pairing file
                _logger?.LogInfo("🔍 DEBUG: Checking RustDesktop saved pairing file...");
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configDir = Path.Combine(appDataPath, "RustDesktop");
                var pairingFile = Path.Combine(configDir, "pairing.json");
                if (File.Exists(pairingFile))
                {
                    try
                    {
                        var json = File.ReadAllText(pairingFile);
                        var pairingData = JsonSerializer.Deserialize<ServerInfo>(json);
                        if (pairingData != null && !string.IsNullOrWhiteSpace(pairingData.ServerId) && !string.IsNullOrWhiteSpace(pairingData.PlayerToken))
                        {
                            _logger?.LogInfo($"✓✓✓ FOUND PAIRED SERVER IN RUSTDESKTOP CONFIG!");
                            _logger?.LogInfo($"  Server ID: {pairingData.ServerId}");
                            _logger?.LogInfo($"  Player Token: {pairingData.PlayerToken.Substring(0, Math.Min(20, pairingData.PlayerToken.Length))}...");
                            return pairingData;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"Error reading pairing file: {ex.Message}");
                    }
                }
                
                // Check rustplus.js config file for pairing data
                _logger?.LogInfo("🔍 DEBUG: Checking rustplus.js config file...");
                var rustplusConfigPath = Path.Combine(configDir, "rustplusjs-config.json");
                if (File.Exists(rustplusConfigPath))
                {
                    try
                    {
                        var json = File.ReadAllText(rustplusConfigPath);
                        var config = JsonSerializer.Deserialize<JsonElement>(json);
                        
                        // rustplus.js stores pairing data in the config file
                        // Check for server pairing data
                        if (config.TryGetProperty("servers", out var serversEl) && serversEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var server in serversEl.EnumerateArray())
                            {
                                if (server.TryGetProperty("playerToken", out var tokenEl) &&
                                    server.TryGetProperty("playerId", out var playerIdEl))
                                {
                                    var playerToken = tokenEl.GetString();
                                    var playerId = playerIdEl.GetString();
                                    
                                    if (!string.IsNullOrWhiteSpace(playerToken) && !string.IsNullOrWhiteSpace(playerId))
                                    {
                                        _logger?.LogInfo("✓✓✓ Found pairing in rustplus.js config file!");
                                        return new ServerInfo
                                        {
                                            ServerId = server.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty,
                                            PlayerToken = playerToken,
                                            SteamId = playerId,
                                            IpAddress = server.TryGetProperty("ip", out var ipEl) ? ipEl.GetString() ?? string.Empty : string.Empty,
                                            Port = server.TryGetProperty("port", out var portEl) && portEl.TryGetInt32(out var port) ? port : 28082,
                                            Name = server.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty
                                        };
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"Failed to read rustplus.js config: {ex.Message}");
                    }
                }
                
                _logger?.LogError("❌ NO PAIRED SERVER FOUND - Server ID and Player Token are MISSING");
                _logger?.LogError("   This means you need to pair with the server first!");
                _logger?.LogError("   Pairing options:");
                _logger?.LogError("     1. Use Rust+ mobile app to pair with the server");
                _logger?.LogError("     2. Use in-game pairing (ESC → Rust+ → Pair)");
                _logger?.LogError("   After pairing, click 'Pair Server' again to check for credentials.");
                
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Exception in GetPairedServerAsync: {ex.Message}", ex);
                return null;
            }
        });
    }

    public async Task<string?> GetSteamIdAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Try registry first
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var activeUser = key.GetValue("ActiveProcess")?.ToString();
                    if (!string.IsNullOrEmpty(activeUser))
                    {
                        // Steam ID is usually stored in userdata
                        var steamId = GetSteamIdFromRegistry();
                        if (!string.IsNullOrEmpty(steamId))
                            return steamId;
                    }
                }

                // Try to get from Rust's playerprefs
                return GetSteamIdFromRustFiles();
            }
            catch
            {
                return null;
            }
        });
    }

    public List<ServerInfo> GetAllPairedServers()
    {
        var servers = new List<ServerInfo>();

        try
        {
            // Get from registry
            var registryServer = GetFromRegistry();
            if (registryServer != null)
                servers.Add(registryServer);

            // Get from game files (might have multiple)
            var fileServers = GetFromGameFiles();
            if (fileServers != null && !servers.Any(s => s.ServerId == fileServers.ServerId))
                servers.Add(fileServers);
        }
        catch
        {
            // Ignore errors
        }

        return servers;
    }

    public async Task<ServerInfo?> GetActiveServerFromGameAsync()
    {
        var activeSessionService = new ActiveSessionService();
        return await activeSessionService.GetActiveServerSessionAsync();
    }

    private ServerInfo? GetFromRegistry()
    {
        try
        {
            // Try multiple registry paths where Rust might store pairing info
            // Also check mobile app storage locations (if Rust+ mobile app stores data on Windows)
            var registryPaths = new[]
            {
                @"Software\Facepunch Studios LTD\Rust",
                @"Software\Facepunch Studios LTD\RustPlus",
                @"Software\Valve\Steam\Apps\252490", // Rust's Steam App ID
                @"Software\Facepunch Studios LTD\Rust\RustPlus", // Nested path
                @"Software\Facepunch Studios LTD\Rust\Companion", // Companion app path
                @"Software\Facepunch Studios LTD\Rust\Pairing", // Pairing path
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall", // Check installed apps
                @"Software\Classes\rustplus", // URL protocol handler
                @"Software\Classes\Applications\RustPlus.exe", // If mobile app is installed
            };

            _logger?.LogInfo($"🔍 DEBUG: Checking {registryPaths.Length} registry paths...");

            foreach (var path in registryPaths)
            {
                try
                {
                    _logger?.LogInfo($"🔍 DEBUG: Checking registry path: HKEY_CURRENT_USER\\{path}");
                    using var key = Registry.CurrentUser.OpenSubKey(path);
                    if (key == null)
                    {
                        _logger?.LogDebug($"  ⚠ Registry key does not exist: {path}");
                        continue;
                    }

                    _logger?.LogInfo($"  ✓ Registry key exists! Checking for values...");
                    
                    // Log all available value names for debugging
                    var valueNames = key.GetValueNames();
                    _logger?.LogInfo($"  📋 Found {valueNames.Length} registry values:");
                    foreach (var valueName in valueNames)
                    {
                        var value = key.GetValue(valueName)?.ToString();
                        if (!string.IsNullOrEmpty(value) && value.Length < 100)
                        {
                            _logger?.LogInfo($"    - {valueName} = {value}");
                        }
                        else if (!string.IsNullOrEmpty(value))
                        {
                            _logger?.LogInfo($"    - {valueName} = {value.Substring(0, 50)}... (length: {value.Length})");
                        }
                    }

                    // Try various key name formats - Rust+ might store credentials with different naming
                    // Also check for values that might contain pairing info in their names
                    var serverId = key.GetValue("serverid")?.ToString() 
                        ?? key.GetValue("serverId")?.ToString()
                        ?? key.GetValue("ServerId")?.ToString()
                        ?? key.GetValue("SERVER_ID")?.ToString()
                        ?? key.GetValue("server_id")?.ToString()
                        ?? key.GetValue("rustplus.serverid")?.ToString()
                        ?? key.GetValue("rustplus.server_id")?.ToString();
                    
                    var playerToken = key.GetValue("playertoken")?.ToString()
                        ?? key.GetValue("playerToken")?.ToString()
                        ?? key.GetValue("PlayerToken")?.ToString()
                        ?? key.GetValue("PLAYER_TOKEN")?.ToString()
                        ?? key.GetValue("player_token")?.ToString()
                        ?? key.GetValue("rustplus.playertoken")?.ToString()
                        ?? key.GetValue("rustplus.player_token")?.ToString();
                    
                    // Check if any value names contain "server" or "token" - might be stored differently
                    if (string.IsNullOrEmpty(serverId) || string.IsNullOrEmpty(playerToken))
                    {
                        foreach (var valueName in valueNames)
                        {
                            var lowerName = valueName.ToLower();
                            
                            // Look for Server ID in various formats
                            if (string.IsNullOrEmpty(serverId))
                            {
                                if ((lowerName.Contains("server") && lowerName.Contains("id")) ||
                                    lowerName.Contains("rustplus.serverid") ||
                                    lowerName.Contains("companion.serverid") ||
                                    lowerName.EndsWith("serverid") ||
                                    lowerName.EndsWith("server_id"))
                                {
                                    var val = key.GetValue(valueName)?.ToString();
                                    if (!string.IsNullOrEmpty(val) && val.Length > 10) // Server IDs are usually long
                                    {
                                        serverId = val;
                                        _logger?.LogInfo($"    ✓ Found Server ID in value: {valueName} = {val.Substring(0, Math.Min(30, val.Length))}...");
                                    }
                                }
                            }
                            
                            // Look for Player Token in various formats
                            if (string.IsNullOrEmpty(playerToken))
                            {
                                var tokenValue = key.GetValue(valueName)?.ToString();
                                if ((lowerName.Contains("token") && lowerName.Contains("player")) ||
                                    lowerName.Contains("rustplus.token") ||
                                    lowerName.Contains("companion.token") ||
                                    lowerName.EndsWith("playertoken") ||
                                    lowerName.EndsWith("player_token") ||
                                    (lowerName.Contains("token") && !string.IsNullOrEmpty(tokenValue) && tokenValue.Length > 20)) // Long token values
                                {
                                    if (!string.IsNullOrEmpty(tokenValue) && tokenValue.Length > 20) // Player tokens are usually long
                                    {
                                        playerToken = tokenValue;
                                        _logger?.LogInfo($"    ✓ Found Player Token in value: {valueName} = {tokenValue.Substring(0, Math.Min(20, tokenValue.Length))}...");
                                    }
                                }
                            }
                            
                            // Also check for long string values that might be tokens (tokens are usually 40+ characters)
                            if (string.IsNullOrEmpty(playerToken))
                            {
                                var potentialToken = key.GetValue(valueName)?.ToString();
                                if (!string.IsNullOrEmpty(potentialToken) && potentialToken.Length > 40 && 
                                    !lowerName.Contains("session") && !lowerName.Contains("history") &&
                                    !lowerName.Contains("cookie") && !lowerName.Contains("byte"))
                                {
                                    // This might be a token - check if it looks like a base64 or hex string
                                    if (System.Text.RegularExpressions.Regex.IsMatch(potentialToken, @"^[A-Za-z0-9+/=]{40,}$") ||
                                        System.Text.RegularExpressions.Regex.IsMatch(potentialToken, @"^[A-Fa-f0-9]{40,}$"))
                                    {
                                        _logger?.LogInfo($"    🔍 Potential token found in: {valueName} (length: {potentialToken.Length})");
                                        // Don't auto-assign, but log it for manual inspection
                                    }
                                }
                            }
                        }
                    }
                    
                    var serverIp = key.GetValue("serverip")?.ToString()
                        ?? key.GetValue("serverIp")?.ToString()
                        ?? key.GetValue("ServerIp")?.ToString();
                    
                    var serverPort = key.GetValue("serverport")?.ToString()
                        ?? key.GetValue("serverPort")?.ToString()
                        ?? key.GetValue("ServerPort")?.ToString();

                    _logger?.LogInfo($"  🔍 Search results:");
                    _logger?.LogInfo($"    Server ID: {(string.IsNullOrEmpty(serverId) ? "❌ NOT FOUND" : $"✓ FOUND: {serverId}")}");
                    _logger?.LogInfo($"    Player Token: {(string.IsNullOrEmpty(playerToken) ? "❌ NOT FOUND" : $"✓ FOUND: {playerToken.Substring(0, Math.Min(20, playerToken.Length))}...")}");
                    _logger?.LogInfo($"    Server IP: {(string.IsNullOrEmpty(serverIp) ? "❌ NOT FOUND" : $"✓ FOUND: {serverIp}")}");
                    _logger?.LogInfo($"    Server Port: {(string.IsNullOrEmpty(serverPort) ? "❌ NOT FOUND" : $"✓ FOUND: {serverPort}")}");

                    // Return server info if we have Player Token, IP, and Port (Server ID is optional)
                    if (!string.IsNullOrEmpty(playerToken) && !string.IsNullOrEmpty(serverIp) && !string.IsNullOrEmpty(serverPort))
                    {
                        _logger?.LogInfo($"  ✓✓✓ PAIRING INFO FOUND IN REGISTRY!");
                        if (string.IsNullOrEmpty(serverId))
                        {
                            _logger?.LogInfo($"  ⚠ Server ID not found, but will attempt connection (Server ID may be obtained during connection)");
                        }
                        return new ServerInfo
                        {
                            ServerId = serverId ?? string.Empty,
                            PlayerToken = playerToken,
                            IpAddress = serverIp,
                            Port = int.TryParse(serverPort, out var port) ? port : 28082
                        };
                    }
                    else
                    {
                        _logger?.LogWarning($"  ⚠ Incomplete pairing info - missing {(string.IsNullOrEmpty(serverId) ? "Server ID" : "")} {(string.IsNullOrEmpty(playerToken) ? "Player Token" : "")} {(string.IsNullOrEmpty(serverIp) ? "Server IP" : "")} {(string.IsNullOrEmpty(serverPort) ? "Server Port" : "")}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"  ❌ Error reading registry path {path}: {ex.Message}");
                    // Try next path
                    continue;
                }
            }
            
            _logger?.LogWarning("⚠ No complete pairing info found in any registry path");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Exception in GetFromRegistry: {ex.Message}", ex);
            return null;
        }
        
        return null;
    }

    private ServerInfo? GetFromGameFiles()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var rustPath = Path.Combine(appDataPath, "..", "LocalLow", "Facepunch Studios LTD", "Rust");

            _logger?.LogInfo($"🔍 DEBUG: Checking game files for Server ID + Player Token...");
            _logger?.LogInfo($"  Primary path: {rustPath}");

            if (!Directory.Exists(rustPath))
            {
                _logger?.LogWarning($"  ⚠ Primary path does not exist, trying alternative...");
                // Also try AppData\Roaming
                var roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                rustPath = Path.Combine(roamingPath, "Facepunch Studios LTD", "Rust");
                _logger?.LogInfo($"  Alternative path: {rustPath}");
                
                if (!Directory.Exists(rustPath))
                {
                    _logger?.LogError($"  ❌ Rust game directory not found in either location!");
                    _logger?.LogError($"     This might mean Rust is not installed or hasn't been run yet.");
                    return null;
                }
            }
            
            _logger?.LogInfo($"  ✓ Rust directory found: {rustPath}");

            // Look for playerprefs or serverinfo files
            _logger?.LogInfo($"  🔍 Checking for playerprefs file...");
            var playerPrefsPath = Path.Combine(rustPath, "playerprefs");
            if (File.Exists(playerPrefsPath))
            {
                _logger?.LogInfo($"    ✓ Found playerprefs file: {playerPrefsPath}");
                var content = File.ReadAllText(playerPrefsPath);
                var result = ParsePlayerPrefs(content);
                if (result != null)
                {
                    _logger?.LogInfo($"    ✓✓✓ Found pairing info in playerprefs!");
                    return result;
                }
                _logger?.LogWarning($"    ⚠ playerprefs exists but no pairing info found");
            }
            else
            {
                _logger?.LogDebug($"    ⚠ playerprefs file not found");
            }

            // Try serverinfo.json or similar
            _logger?.LogInfo($"  🔍 Checking for serverinfo.json...");
            var serverInfoPath = Path.Combine(rustPath, "serverinfo.json");
            if (File.Exists(serverInfoPath))
            {
                _logger?.LogInfo($"    ✓ Found serverinfo.json: {serverInfoPath}");
                var content = File.ReadAllText(serverInfoPath);
                var result = ParseServerInfoJson(content);
                if (result != null)
                {
                    _logger?.LogInfo($"    ✓✓✓ Found pairing info in serverinfo.json!");
                    return result;
                }
                _logger?.LogWarning($"    ⚠ serverinfo.json exists but no pairing info found");
            }
            else
            {
                _logger?.LogDebug($"    ⚠ serverinfo.json not found");
            }

            // Try Rust+ specific files and directories
            _logger?.LogInfo($"  🔍 Checking for rustplus directory...");
            var rustPlusPath = Path.Combine(rustPath, "rustplus");
            if (Directory.Exists(rustPlusPath))
            {
                _logger?.LogInfo($"    ✓ Found rustplus directory: {rustPlusPath}");
                var files = Directory.GetFiles(rustPlusPath, "*", SearchOption.AllDirectories);
                _logger?.LogInfo($"    Found {files.Length} files in rustplus directory");
                foreach (var file in files)
                {
                    _logger?.LogInfo($"      Checking: {Path.GetFileName(file)}");
                    try
                    {
                        var content = File.ReadAllText(file);
                        // Try JSON first
                        var serverInfo = ParseServerInfoJson(content);
                        if (serverInfo != null)
                        {
                            _logger?.LogInfo($"      ✓✓✓ Found pairing info in {Path.GetFileName(file)}!");
                            return serverInfo;
                        }
                        // Try key-value format
                        serverInfo = ParsePlayerPrefs(content);
                        if (serverInfo != null)
                        {
                            _logger?.LogInfo($"      ✓✓✓ Found pairing info in {Path.GetFileName(file)}!");
                            return serverInfo;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"      ⚠ Error parsing {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            else
            {
                _logger?.LogDebug($"    ⚠ rustplus directory not found");
            }
            
            // Check for companion/rustplus files in root Rust directory
            _logger?.LogInfo($"  🔍 Checking for companion/rustplus files in root directory...");
            try
            {
                var allFiles = Directory.GetFiles(rustPath, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in allFiles)
                {
                    var fileName = Path.GetFileName(file).ToLower();
                    if (fileName.Contains("companion") || fileName.Contains("rustplus") || fileName.Contains("pairing"))
                    {
                        _logger?.LogInfo($"    Found potential pairing file: {Path.GetFileName(file)}");
                        try
                        {
                            var content = File.ReadAllText(file);
                            var serverInfo = ParseServerInfoJson(content) ?? ParsePlayerPrefs(content);
                            if (serverInfo != null)
                            {
                                _logger?.LogInfo($"    ✓✓✓ Found pairing info in {Path.GetFileName(file)}!");
                                return serverInfo;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning($"    ⚠ Error reading {Path.GetFileName(file)}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"    ❌ Error searching root directory: {ex.Message}");
            }
            
            // Try looking in all JSON files in Rust directory for server info
            _logger?.LogInfo($"  🔍 Searching all JSON files in Rust directory...");
            try
            {
                var allJsonFiles = Directory.GetFiles(rustPath, "*.json", SearchOption.AllDirectories);
                _logger?.LogInfo($"    Found {allJsonFiles.Length} JSON files total");
                foreach (var file in allJsonFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(file);
                        var serverInfo = ParseServerInfoJson(content);
                        if (serverInfo != null && !string.IsNullOrWhiteSpace(serverInfo.ServerId) && !string.IsNullOrWhiteSpace(serverInfo.PlayerToken))
                        {
                            _logger?.LogInfo($"      ✓✓✓ Found pairing info in {Path.GetFileName(file)}!");
                            return serverInfo;
                        }
                    }
                    catch
                    {
                        // Skip files we can't parse
                    }
                }
                _logger?.LogWarning($"    ⚠ No pairing info found in any JSON files");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"    ❌ Error searching JSON files: {ex.Message}");
            }
            
            _logger?.LogWarning("⚠ No pairing info found in any game files");
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private ServerInfo? ParsePlayerPrefs(string content)
    {
        // PlayerPrefs format is typically key=value pairs or JSON
        try
        {
            _logger?.LogDebug($"Parsing playerprefs content (length: {content.Length})...");
            
            // Try JSON first
            if (content.TrimStart().StartsWith("{"))
            {
                _logger?.LogDebug("Content appears to be JSON format");
                return ParseServerInfoJson(content);
            }

            // Parse key=value format (Unity PlayerPrefs format: key=value or key_hash=value)
            _logger?.LogDebug("Content appears to be key=value format");
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var serverId = string.Empty;
            var playerToken = string.Empty;
            var serverIp = string.Empty;
            var port = 28082;

            _logger?.LogDebug($"Parsing {lines.Length} lines...");
            
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length >= 2)
                {
                    var key = parts[0].Trim();
                    var value = string.Join("=", parts.Skip(1)).Trim(); // Handle values with = in them
                    var lowerKey = key.ToLower();

                    // Unity PlayerPrefs often have hashes: "serverid_h1234567890"
                    // Check for various patterns
                    if (lowerKey.Contains("serverid") || lowerKey.Contains("server_id"))
                    {
                        if (string.IsNullOrEmpty(serverId) && value.Length > 10)
                        {
                            serverId = value;
                            _logger?.LogInfo($"    ✓ Found Server ID: {key} = {value.Substring(0, Math.Min(30, value.Length))}...");
                        }
                    }
                    else if (lowerKey.Contains("playertoken") || lowerKey.Contains("player_token") || 
                            (lowerKey.Contains("token") && value.Length > 20))
                    {
                        if (string.IsNullOrEmpty(playerToken) && value.Length > 20)
                        {
                            playerToken = value;
                            _logger?.LogInfo($"    ✓ Found Player Token: {key} = {value.Substring(0, Math.Min(20, value.Length))}...");
                        }
                    }
                    else if (lowerKey.Contains("serverip") || lowerKey.Contains("server_ip"))
                    {
                        serverIp = value;
                    }
                    else if (lowerKey.Contains("serverport") || lowerKey.Contains("server_port"))
                    {
                        if (int.TryParse(value, out var p))
                            port = p;
                    }
                    // Also check for Rust+ specific keys
                    else if (lowerKey.Contains("rustplus") && (lowerKey.Contains("serverid") || lowerKey.Contains("server_id")))
                    {
                        if (string.IsNullOrEmpty(serverId) && value.Length > 10)
                        {
                            serverId = value;
                            _logger?.LogInfo($"    ✓ Found Server ID in Rust+ key: {key}");
                        }
                    }
                    else if (lowerKey.Contains("rustplus") && (lowerKey.Contains("token") || lowerKey.Contains("playertoken")))
                    {
                        if (string.IsNullOrEmpty(playerToken) && value.Length > 20)
                        {
                            playerToken = value;
                            _logger?.LogInfo($"    ✓ Found Player Token in Rust+ key: {key}");
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(serverId) && !string.IsNullOrEmpty(playerToken))
            {
                _logger?.LogInfo($"    ✓✓✓ Successfully parsed pairing info from playerprefs!");
                return new ServerInfo
                {
                    ServerId = serverId,
                    PlayerToken = playerToken,
                    IpAddress = serverIp,
                    Port = port
                };
            }
            else
            {
                _logger?.LogWarning($"    ⚠ Parsed playerprefs but missing: Server ID={(string.IsNullOrEmpty(serverId) ? "MISSING" : "FOUND")}, Token={(string.IsNullOrEmpty(playerToken) ? "MISSING" : "FOUND")}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error parsing playerprefs: {ex.Message}", ex);
        }

        return null;
    }

    private ServerInfo? ParseServerInfoJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Try both camelCase and snake_case property names
            var serverId = root.TryGetProperty("serverId", out var sid) ? sid.GetString() 
                : (root.TryGetProperty("server_id", out var sid2) ? sid2.GetString() : null);
            var playerToken = root.TryGetProperty("playerToken", out var pt) ? pt.GetString()
                : (root.TryGetProperty("player_token", out var pt2) ? pt2.GetString() : null);
            var serverIp = root.TryGetProperty("serverIp", out var sip) ? sip.GetString()
                : (root.TryGetProperty("server_ip", out var sip2) ? sip2.GetString() : null);
            var port = root.TryGetProperty("port", out var p) && p.TryGetInt32(out var portVal) ? portVal : 28082;

            if (!string.IsNullOrEmpty(serverId) && !string.IsNullOrEmpty(playerToken))
            {
                return new ServerInfo
                {
                    ServerId = serverId ?? string.Empty,
                    PlayerToken = playerToken ?? string.Empty,
                    IpAddress = serverIp ?? string.Empty,
                    Port = port
                };
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    private string? GetSteamIdFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            if (key != null)
            {
                var steamId = key.GetValue("ActiveUser")?.ToString();
                if (!string.IsNullOrEmpty(steamId) && steamId.Length >= 17)
                {
                    // Steam ID64 format
                    return steamId;
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private string? GetSteamIdFromRustFiles()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var rustPath = Path.Combine(appDataPath, "..", "LocalLow", "Facepunch Studios LTD", "Rust");

            if (!Directory.Exists(rustPath))
                return null;

            // Look for steamid in various files
            var files = Directory.GetFiles(rustPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    // Look for Steam ID pattern (17 digits)
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"\b[0-9]{17}\b");
                    if (match.Success)
                    {
                        return match.Value;
                    }
                }
                catch
                {
                    // Skip files we can't read
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }
    
    private string? GetSteamUserdataPath()
    {
        try
        {
            // Try to get Steam install path from registry
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key != null)
            {
                var steamPath = key.GetValue("SteamPath")?.ToString();
                if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                {
                    var userdataPath = Path.Combine(steamPath, "userdata");
                    if (Directory.Exists(userdataPath))
                    {
                        return userdataPath;
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }
        
        return null;
    }
    
    private ServerInfo? GetFromSteamUserdata(string userdataPath)
    {
        try
        {
            _logger?.LogInfo($"  🔍 Searching Steam userdata for Rust+ data...");
            var directories = Directory.GetDirectories(userdataPath);
            _logger?.LogInfo($"    Found {directories.Length} user directories");
            
            foreach (var userDir in directories)
            {
                try
                {
                    // Rust+ companion app might store data in userdata\<steamid>\...
                    // Look for Rust+ related files
                    var rustPlusPath = Path.Combine(userDir, "252490"); // Rust's Steam App ID
                    if (Directory.Exists(rustPlusPath))
                    {
                        _logger?.LogInfo($"    Checking Rust app data: {rustPlusPath}");
                        var files = Directory.GetFiles(rustPlusPath, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var fileName = Path.GetFileName(file).ToLower();
                            if (fileName.Contains("rustplus") || fileName.Contains("companion") || fileName.Contains("pairing") || fileName.Contains("server"))
                            {
                                _logger?.LogInfo($"      Found potential pairing file: {Path.GetFileName(file)}");
                                try
                                {
                                    var content = File.ReadAllText(file);
                                    var serverInfo = ParseServerInfoJson(content) ?? ParsePlayerPrefs(content);
                                    if (serverInfo != null && !string.IsNullOrWhiteSpace(serverInfo.ServerId) && !string.IsNullOrWhiteSpace(serverInfo.PlayerToken))
                                    {
                                        _logger?.LogInfo($"      ✓✓✓ Found pairing info in {Path.GetFileName(file)}!");
                                        return serverInfo;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning($"      ⚠ Error reading {Path.GetFileName(file)}: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    // Also check for Rust+ companion app data (might be in a different location)
                    var companionPath = Path.Combine(userDir, "remote", "252490");
                    if (Directory.Exists(companionPath))
                    {
                        _logger?.LogInfo($"    Checking Rust remote data: {companionPath}");
                        var files = Directory.GetFiles(companionPath, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            try
                            {
                                var content = File.ReadAllText(file);
                                var serverInfo = ParseServerInfoJson(content) ?? ParsePlayerPrefs(content);
                                if (serverInfo != null && !string.IsNullOrWhiteSpace(serverInfo.ServerId) && !string.IsNullOrWhiteSpace(serverInfo.PlayerToken))
                                {
                                    _logger?.LogInfo($"      ✓✓✓ Found pairing info in {Path.GetFileName(file)}!");
                                    return serverInfo;
                                }
                            }
                            catch
                            {
                                // Skip files we can't read
                            }
                        }
                    }
                }
                catch
                {
                    // Skip user directories we can't access
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"  ❌ Error searching Steam userdata: {ex.Message}");
        }
        
        return null;
    }
}

