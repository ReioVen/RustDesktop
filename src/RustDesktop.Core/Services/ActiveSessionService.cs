using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

/// <summary>
/// Service to detect the currently active Rust server session from the running game
/// </summary>
public class ActiveSessionService : IActiveSessionService
{
    private readonly ILoggingService? _logger;

    public ActiveSessionService(ILoggingService? logger = null)
    {
        _logger = logger;
    }
    public bool IsRustRunning()
    {
        try
        {
            _logger?.LogDebug("Checking if Rust is running...");
            var processes = Process.GetProcessesByName("RustClient");
            _logger?.LogDebug($"Found {processes.Length} RustClient processes");
            
            if (processes.Length == 0)
            {
                processes = Process.GetProcessesByName("rust");
                _logger?.LogDebug($"Found {processes.Length} rust processes");
            }

            var isRunning = processes.Length > 0;
            _logger?.LogInfo($"Rust is {(isRunning ? "running" : "not running")}");
            return isRunning;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error checking if Rust is running", ex);
            return false;
        }
    }

    public async Task<ServerInfo?> GetActiveServerSessionAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger?.LogInfo("Starting active server session detection...");
                
                // Try multiple methods to get active server info
                _logger?.LogDebug("Method 1: Trying GetFromActiveConnection...");
                var serverInfo = GetFromActiveConnection();
                if (serverInfo != null)
                {
                    _logger?.LogInfo($"Found server via active connection: {serverInfo.IpAddress}:{serverInfo.Port}");
                    return serverInfo;
                }
                _logger?.LogDebug("GetFromActiveConnection returned null");

                _logger?.LogDebug("Method 2: Trying GetFromRustLogs...");
                serverInfo = GetFromRustLogs();
                if (serverInfo != null)
                {
                    _logger?.LogInfo($"Found server via logs: {serverInfo.IpAddress}:{serverInfo.Port}");
                    return serverInfo;
                }
                _logger?.LogDebug("GetFromRustLogs returned null");

                _logger?.LogDebug("Method 3: Trying GetFromNetworkConnections...");
                serverInfo = GetFromNetworkConnections();
                if (serverInfo != null)
                {
                    _logger?.LogInfo($"Found server via network connections: {serverInfo.IpAddress}:{serverInfo.Port}");
                    return serverInfo;
                }
                _logger?.LogDebug("GetFromNetworkConnections returned null");

                _logger?.LogDebug("Method 4: Trying GetFromRegistryActiveSession...");
                serverInfo = GetFromRegistryActiveSession();
                if (serverInfo != null)
                {
                    _logger?.LogInfo($"Found server via registry: {serverInfo.IpAddress}:{serverInfo.Port}");
                    return serverInfo;
                }
                _logger?.LogDebug("GetFromRegistryActiveSession returned null");

                _logger?.LogWarning("All detection methods failed - no server found");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Exception in GetActiveServerSessionAsync", ex);
                return null;
            }
        });
    }

    public async Task<string?> GetCurrentServerIpAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Check network connections from Rust process
                var ip = GetServerIpFromNetworkConnections();
                if (!string.IsNullOrEmpty(ip))
                    return ip;

                // Check logs
                ip = GetServerIpFromLogs();
                if (!string.IsNullOrEmpty(ip))
                    return ip;

                // Check registry
                return GetServerIpFromRegistry();
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<int?> GetCurrentServerRustPlusPortAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Rust+ port is typically server port + 1, or 28082 by default
                // Try to get from server connection or use default
                var port = GetRustPlusPortFromLogs();
                if (port.HasValue)
                    return port;

                // Default Rust+ port
                return 28082;
            }
            catch
            {
                return 28082;
            }
        });
    }

    private ServerInfo? GetFromActiveConnection()
    {
        try
        {
            var serverIp = GetServerIpFromNetworkConnections();
            if (string.IsNullOrEmpty(serverIp))
                return null;

            var rustPlusPort = GetRustPlusPortFromLogs() ?? 28082;
            var steamId = GetSteamId();

            return new ServerInfo
            {
                IpAddress = serverIp,
                Port = rustPlusPort,
                SteamId = steamId ?? string.Empty,
                // Server ID and token will be obtained during connection
                ServerId = string.Empty,
                PlayerToken = string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    private ServerInfo? GetFromRustLogs()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var rustPath = Path.Combine(appDataPath, "..", "LocalLow", "Facepunch Studios LTD", "Rust");

            _logger?.LogDebug($"GetFromRustLogs: Checking path: {rustPath}");

            if (!Directory.Exists(rustPath))
            {
                _logger?.LogWarning($"GetFromRustLogs: Rust directory does not exist: {rustPath}");
                return null;
            }

            _logger?.LogDebug("GetFromRustLogs: Rust directory exists, searching for log files...");

            // Look for recent log files
            var logFiles = Directory.GetFiles(rustPath, "*.log", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .Take(5)
                .ToList();

            _logger?.LogDebug($"GetFromRustLogs: Found {logFiles.Count} log files");

            foreach (var logFile in logFiles)
            {
                try
                {
                    _logger?.LogDebug($"GetFromRustLogs: Reading log file: {logFile}");
                    var content = File.ReadAllText(logFile);
                    _logger?.LogDebug($"GetFromRustLogs: Log file size: {content.Length} characters");
                    var serverInfo = ParseServerInfoFromLog(content);
                    if (serverInfo != null)
                    {
                        _logger?.LogInfo($"GetFromRustLogs: Found server info in {Path.GetFileName(logFile)}");
                        return serverInfo;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"GetFromRustLogs: Error reading log file {logFile}", ex);
                }
            }

            _logger?.LogDebug("GetFromRustLogs: No server info found in log files");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in GetFromRustLogs", ex);
        }

        return null;
    }

    private ServerInfo? ParseServerInfoFromLog(string logContent)
    {
        try
        {
            // Look for server connection patterns in logs
            // Common patterns: "Connecting to server", "Server IP:", etc.
            var ipPattern = @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b";
            var ipMatches = Regex.Matches(logContent, ipPattern);
            
            if (ipMatches.Count > 0)
            {
                // Get the most recent IP (likely the server)
                var serverIp = ipMatches[ipMatches.Count - 1].Value;
                
                // Look for port information
                var portPattern = @"port[:\s]+(\d+)";
                var portMatch = Regex.Match(logContent, portPattern, RegexOptions.IgnoreCase);
                var port = portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out var p) ? p : 28082;

                return new ServerInfo
                {
                    IpAddress = serverIp,
                    Port = port,
                    SteamId = GetSteamId() ?? string.Empty
                };
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    private ServerInfo? GetFromNetworkConnections()
    {
        try
        {
            _logger?.LogDebug("GetFromNetworkConnections: Checking if Rust is running...");
            if (!IsRustRunning())
            {
                _logger?.LogDebug("GetFromNetworkConnections: Rust is not running");
                return null;
            }

            var rustProcesses = Process.GetProcessesByName("RustClient");
            if (rustProcesses.Length == 0)
                rustProcesses = Process.GetProcessesByName("rust");

            if (rustProcesses.Length == 0)
            {
                _logger?.LogDebug("GetFromNetworkConnections: No Rust processes found");
                return null;
            }

            _logger?.LogDebug($"GetFromNetworkConnections: Found {rustProcesses.Length} Rust process(es)");
            var rustProcess = rustProcesses[0];
            _logger?.LogDebug($"GetFromNetworkConnections: Rust process ID: {rustProcess.Id}");

            // Get all TCP connections
            var allConnections = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections()
                .Where(c => c.State == TcpState.Established)
                .ToList();

            _logger?.LogDebug($"GetFromNetworkConnections: Found {allConnections.Count} total established TCP connections");

            // Try to get connections for the Rust process using netstat approach
            // Since we can't directly filter by process, we'll look for external IPs with common Rust ports
            var externalConnections = allConnections
                .Where(c => !c.RemoteEndPoint.Address.ToString().StartsWith("127.") && 
                           !c.RemoteEndPoint.Address.ToString().StartsWith("::1") &&
                           !c.RemoteEndPoint.Address.ToString().StartsWith("192.168.") &&
                           !c.RemoteEndPoint.Address.ToString().StartsWith("10.") &&
                           !c.RemoteEndPoint.Address.ToString().StartsWith("172."))
                .ToList();

            _logger?.LogDebug($"GetFromNetworkConnections: Found {externalConnections.Count} external (non-local) connections");
            
            // Log all external connections for debugging
            _logger?.LogDebug("All external connections:");
            foreach (var conn in externalConnections.Take(20))
            {
                _logger?.LogDebug($"  - {conn.RemoteEndPoint.Address}:{conn.RemoteEndPoint.Port}");
            }

            // Look for connections in Rust port ranges (game ports are typically 28015-28100, but can vary)
            var rustPortRanges = new[]
            {
                (28015, 28100),  // Standard Rust range
                (27015, 27050),  // Alternative range (your server is here: 27022)
                (28000, 29000),  // Extended range
            };

            ServerInfo? serverInfo = null;

            foreach (var (minPort, maxPort) in rustPortRanges)
            {
                var serverConnections = externalConnections
                    .Where(c => c.RemoteEndPoint.Port >= minPort && c.RemoteEndPoint.Port <= maxPort)
                    .OrderByDescending(c => c.RemoteEndPoint.Port)
                    .ToList();

                if (serverConnections.Count > 0)
                {
                    _logger?.LogDebug($"GetFromNetworkConnections: Found {serverConnections.Count} connections in port range {minPort}-{maxPort}");
                    
                    foreach (var conn in serverConnections)
                    {
                        _logger?.LogDebug($"GetFromNetworkConnections: Potential server - {conn.RemoteEndPoint.Address}:{conn.RemoteEndPoint.Port}");
                    }

                    var serverConnection = serverConnections[0];
                    var serverIp = serverConnection.RemoteEndPoint.Address.ToString();
                    var gamePort = serverConnection.RemoteEndPoint.Port;
                    
                    // Rust+ port calculation: game port + 67, or default 28082
                    // Some servers use game port + 1, others use +67
                    // Try multiple possibilities
                    var rustPlusPortOptions = new[]
                    {
                        gamePort + 67,  // Standard Rust+ calculation
                        28082,          // Default Rust+ port
                        gamePort + 1,   // Alternative
                    };

                    var rustPlusPort = rustPlusPortOptions[0]; // Start with standard
                    _logger?.LogInfo($"GetFromNetworkConnections: Selected server {serverIp}:{gamePort}, Rust+ port options: {string.Join(", ", rustPlusPortOptions)}");

                    serverInfo = new ServerInfo
                    {
                        IpAddress = serverIp,
                        Port = rustPlusPort,
                        GamePort = gamePort, // Store game port for reference
                        SteamId = GetSteamId() ?? string.Empty
                    };
                    break;
                }
            }

            // If no connections in standard ranges, look for any external connection with high port (might be custom server)
            if (serverInfo == null && externalConnections.Count > 0)
            {
                _logger?.LogDebug("GetFromNetworkConnections: No connections in standard Rust port ranges, checking all external connections...");
                
                // Look for connections with ports > 20000 (likely game servers)
                var highPortConnections = externalConnections
                    .Where(c => c.RemoteEndPoint.Port > 20000)
                    .OrderByDescending(c => c.RemoteEndPoint.Port)
                    .ToList();

                if (highPortConnections.Count > 0)
                {
                    _logger?.LogDebug($"GetFromNetworkConnections: Found {highPortConnections.Count} external connections with high ports (>20000)");
                    
                    foreach (var conn in highPortConnections.Take(10))
                    {
                        _logger?.LogDebug($"GetFromNetworkConnections: High port connection - {conn.RemoteEndPoint.Address}:{conn.RemoteEndPoint.Port}");
                    }

                    var serverConnection = highPortConnections[0];
                    var serverIp = serverConnection.RemoteEndPoint.Address.ToString();
                    var gamePort = serverConnection.RemoteEndPoint.Port;
                    var rustPlusPort = gamePort + 67; // Use standard calculation
                    if (rustPlusPort > 65535) rustPlusPort = 28082;

                    _logger?.LogInfo($"GetFromNetworkConnections: Using high port connection as server: {serverIp}:{gamePort}, Rust+ port: {rustPlusPort}");

                    serverInfo = new ServerInfo
                    {
                        IpAddress = serverIp,
                        Port = rustPlusPort,
                        GamePort = gamePort,
                        SteamId = GetSteamId() ?? string.Empty
                    };
                }
            }

            if (serverInfo == null)
            {
                _logger?.LogWarning("GetFromNetworkConnections: No suitable server connection found");
                // Log sample of all external connections for debugging
                _logger?.LogDebug($"GetFromNetworkConnections: Sample of external connections:");
                foreach (var conn in externalConnections.Take(10))
                {
                    _logger?.LogDebug($"  - {conn.RemoteEndPoint.Address}:{conn.RemoteEndPoint.Port}");
                }
            }

            return serverInfo;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in GetFromNetworkConnections", ex);
        }

        return null;
    }

    private ServerInfo? GetFromRegistryActiveSession()
    {
        try
        {
            _logger?.LogDebug("GetFromRegistryActiveSession: Opening registry key...");
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Facepunch Studios LTD\Rust");
            if (key == null)
            {
                _logger?.LogWarning("GetFromRegistryActiveSession: Registry key does not exist");
                return null;
            }

            _logger?.LogDebug("GetFromRegistryActiveSession: Registry key opened, reading values...");

            // Check for active session info
            var currentServerIp = key.GetValue("currentserverip")?.ToString();
            var currentServerPort = key.GetValue("currentserverport")?.ToString();
            var lastConnectedServer = key.GetValue("lastconnectedserver")?.ToString();

            _logger?.LogDebug($"GetFromRegistryActiveSession: currentserverip = {currentServerIp ?? "null"}");
            _logger?.LogDebug($"GetFromRegistryActiveSession: currentserverport = {currentServerPort ?? "null"}");
            _logger?.LogDebug($"GetFromRegistryActiveSession: lastconnectedserver = {lastConnectedServer ?? "null"}");

            // Log all registry values for debugging
            var allValues = key.GetValueNames();
            _logger?.LogDebug($"GetFromRegistryActiveSession: Found {allValues.Length} registry values");
            foreach (var valueName in allValues)
            {
                var value = key.GetValue(valueName)?.ToString();
                _logger?.LogDebug($"GetFromRegistryActiveSession: {valueName} = {value ?? "null"}");
            }

            if (!string.IsNullOrEmpty(currentServerIp))
            {
                var port = int.TryParse(currentServerPort, out var p) ? p : 28082;
                _logger?.LogInfo($"GetFromRegistryActiveSession: Found server from currentserverip: {currentServerIp}:{port}");
                return new ServerInfo
                {
                    IpAddress = currentServerIp,
                    Port = port,
                    SteamId = GetSteamId() ?? string.Empty
                };
            }

            // Try parsing last connected server (might be in format "ip:port")
            if (!string.IsNullOrEmpty(lastConnectedServer))
            {
                var parts = lastConnectedServer.Split(':');
                if (parts.Length >= 1)
                {
                    var ip = parts[0];
                    var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 28082;
                    
                    _logger?.LogInfo($"GetFromRegistryActiveSession: Found server from lastconnectedserver: {ip}:{port}");
                    return new ServerInfo
                    {
                        IpAddress = ip,
                        Port = port + 1, // Rust+ port
                        SteamId = GetSteamId() ?? string.Empty
                    };
                }
            }

            _logger?.LogDebug("GetFromRegistryActiveSession: No valid server info found in registry");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in GetFromRegistryActiveSession", ex);
        }

        return null;
    }

    private string? GetServerIpFromNetworkConnections()
    {
        try
        {
            if (!IsRustRunning())
                return null;

            var connections = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections()
                .Where(c => c.State == TcpState.Established)
                .ToList();

            // Find Rust server connection (typically port 28015-28100)
            var serverConnection = connections
                .FirstOrDefault(c => c.RemoteEndPoint.Port >= 28015 && c.RemoteEndPoint.Port <= 28100);

            return serverConnection?.RemoteEndPoint.Address.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string? GetServerIpFromLogs()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var rustPath = Path.Combine(appDataPath, "..", "LocalLow", "Facepunch Studios LTD", "Rust");

            if (!Directory.Exists(rustPath))
                return null;

            var logFiles = Directory.GetFiles(rustPath, "*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .Take(3);

            foreach (var logFile in logFiles)
            {
                try
                {
                    var content = File.ReadAllText(logFile);
                    var ipPattern = @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b";
                    var matches = Regex.Matches(content, ipPattern);
                    if (matches.Count > 0)
                    {
                        return matches[matches.Count - 1].Value;
                    }
                }
                catch
                {
                    // Skip
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private string? GetServerIpFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Facepunch Studios LTD\Rust");
            return key?.GetValue("currentserverip")?.ToString() 
                ?? key?.GetValue("lastconnectedserver")?.ToString()?.Split(':')[0];
        }
        catch
        {
            return null;
        }
    }

    private int? GetRustPlusPortFromLogs()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var rustPath = Path.Combine(appDataPath, "..", "LocalLow", "Facepunch Studios LTD", "Rust");

            if (!Directory.Exists(rustPath))
                return null;

            var logFiles = Directory.GetFiles(rustPath, "*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .Take(3);

            foreach (var logFile in logFiles)
            {
                try
                {
                    var content = File.ReadAllText(logFile);
                    var portPattern = @"rust\+.*port[:\s]+(\d+)|app\.port[:\s]+(\d+)";
                    var match = Regex.Match(content, portPattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var portStr = match.Groups[1].Value;
                        if (string.IsNullOrEmpty(portStr))
                            portStr = match.Groups[2].Value;
                        
                        if (int.TryParse(portStr, out var port))
                            return port;
                    }
                }
                catch
                {
                    // Skip
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private string? GetSteamId()
    {
        try
        {
            _logger?.LogDebug("GetSteamId: Attempting to retrieve Steam ID...");
            
            // Method 1: Try registry ActiveProcess
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
                var steamId = key?.GetValue("ActiveUser")?.ToString();
                if (!string.IsNullOrEmpty(steamId) && steamId.Length >= 17)
                {
                    _logger?.LogInfo($"GetSteamId: Found Steam ID from ActiveProcess: {steamId}");
                    return steamId;
                }
                _logger?.LogDebug("GetSteamId: ActiveProcess registry key found but no valid Steam ID");
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"GetSteamId: ActiveProcess registry check failed: {ex.Message}");
            }

            // Method 2: Try Steam userdata folders
            try
            {
                var steamPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath")?.ToString();
                if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                {
                    var userdataPath = Path.Combine(steamPath, "userdata");
                    if (Directory.Exists(userdataPath))
                    {
                        var userFolders = Directory.GetDirectories(userdataPath);
                        foreach (var userFolder in userFolders)
                        {
                            var folderName = Path.GetFileName(userFolder);
                            // Steam ID64 is 17 digits
                            if (folderName.Length == 17 && folderName.All(char.IsDigit))
                            {
                                _logger?.LogInfo($"GetSteamId: Found Steam ID from userdata: {folderName}");
                                return folderName;
                            }
                        }
                    }
                }
                _logger?.LogDebug("GetSteamId: userdata folder check completed");
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"GetSteamId: userdata check failed: {ex.Message}");
            }

            // Method 3: Try Steam loginusers.vdf file
            try
            {
                var steamPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath")?.ToString();
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                    if (File.Exists(loginUsersPath))
                    {
                        var content = File.ReadAllText(loginUsersPath);
                        // Look for Steam ID64 pattern (17 digits)
                        var match = System.Text.RegularExpressions.Regex.Match(content, @"\b[0-9]{17}\b");
                        if (match.Success)
                        {
                            _logger?.LogInfo($"GetSteamId: Found Steam ID from loginusers.vdf: {match.Value}");
                            return match.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"GetSteamId: loginusers.vdf check failed: {ex.Message}");
            }

            // Method 4: Try Rust playerprefs
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var rustPath = Path.Combine(appDataPath, "..", "LocalLow", "Facepunch Studios LTD", "Rust");
                if (Directory.Exists(rustPath))
                {
                    var files = Directory.GetFiles(rustPath, "*", SearchOption.TopDirectoryOnly);
                    foreach (var file in files.Take(10)) // Limit to avoid too many files
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            var match = System.Text.RegularExpressions.Regex.Match(content, @"\b[0-9]{17}\b");
                            if (match.Success)
                            {
                                _logger?.LogInfo($"GetSteamId: Found Steam ID from Rust file {Path.GetFileName(file)}: {match.Value}");
                                return match.Value;
                            }
                        }
                        catch
                        {
                            // Skip files we can't read
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"GetSteamId: Rust files check failed: {ex.Message}");
            }

            _logger?.LogWarning("GetSteamId: Could not retrieve Steam ID from any method");
        }
        catch (Exception ex)
        {
            _logger?.LogError("GetSteamId: Exception occurred", ex);
        }

        return null;
    }
}

