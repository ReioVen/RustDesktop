using System.Net.WebSockets;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RustDesktop.Core.Models;
using Microsoft.Win32;
using System.Diagnostics;
using System.Linq;

namespace RustDesktop.Core.Services;

/// <summary>
/// Service for pairing with Rust+ server to get Server ID and Player Token
/// </summary>
public class PairingService : IPairingService
{
    private readonly ILoggingService? _logger;
    private ClientWebSocket? _pairingWebSocket;
    private CancellationTokenSource? _pairingCancellationTokenSource;
    private string? _steamId;
    private string? _steamSessionToken;
    private HttpListener? _callbackServer;
    private readonly string _configPath;
    
    public event EventHandler<ServerInfo>? PairingSuccessful;
    public event EventHandler<string>? PairingFailed;
    public event EventHandler<string>? SteamAuthenticated;
    public event EventHandler<string>? SteamAuthenticationFailed;

    public PairingService(ILoggingService? logger = null)
    {
        _logger = logger;
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "RustDesktop");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "steam-auth.json");
        
        LoadSteamAuth();
    }

    public async Task<bool> IsPairedAsync()
    {
        return await Task.Run(() =>
        {
            // Check if we have stored pairing credentials
            var rustDataService = new RustDataService(_logger);
            var pairedServer = rustDataService.GetPairedServerAsync().Result;
            
            if (pairedServer != null && !string.IsNullOrWhiteSpace(pairedServer.ServerId) && !string.IsNullOrWhiteSpace(pairedServer.PlayerToken))
            {
                _logger?.LogInfo("Pairing found: Server ID and Player Token exist");
                return true;
            }
            
            _logger?.LogInfo("No pairing found");
            return false;
        });
    }
    
    public async Task<bool> IsSteamAuthenticatedAsync()
    {
        return await Task.Run(() =>
        {
            if (!string.IsNullOrWhiteSpace(_steamId))
            {
                _logger?.LogInfo($"Steam authenticated: {_steamId}");
                return true;
            }
            
            _logger?.LogInfo("Steam not authenticated");
            return false;
        });
    }
    
    public string? GetSteamId()
    {
        return _steamId;
    }
    
    public async Task<bool> AuthenticateWithSteamAsync()
    {
        try
        {
            _logger?.LogInfo("=== Starting Steam authentication ===");
            
            // Start local HTTP server to receive Steam callback
            var port = FindAvailablePort();
            var callbackUrl = $"http://localhost:{port}/steam-callback";
            
            _logger?.LogInfo($"Starting callback server on port {port}");
            _logger?.LogInfo($"Callback URL: {callbackUrl}");
            
            _callbackServer = new HttpListener();
            _callbackServer.Prefixes.Add($"http://localhost:{port}/");
            _callbackServer.Start();
            
            _logger?.LogInfo("✓ Callback server started");
            
            // Start listening for callback in background
            var callbackTask = Task.Run(async () => await ListenForSteamCallbackAsync());
            
            // Open browser to Steam login
            var steamLoginUrl = $"https://steamcommunity.com/openid/login?" +
                $"openid.ns=http://specs.openid.net/auth/2.0&" +
                $"openid.mode=checkid_setup&" +
                $"openid.return_to={Uri.EscapeDataString(callbackUrl)}&" +
                $"openid.realm={Uri.EscapeDataString(callbackUrl)}&" +
                $"openid.identity=http://specs.openid.net/auth/2.0/identifier_select&" +
                $"openid.claimed_id=http://specs.openid.net/auth/2.0/identifier_select";
            
            _logger?.LogInfo($"Opening browser to Steam login...");
            _logger?.LogInfo($"Please complete Steam login in the browser");
            
            Process.Start(new ProcessStartInfo
            {
                FileName = steamLoginUrl,
                UseShellExecute = true
            });
            
            // Wait for callback (with timeout)
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5)); // 5 minute timeout
            var completedTask = await Task.WhenAny(callbackTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _logger?.LogError("Steam authentication timeout");
                SteamAuthenticationFailed?.Invoke(this, "Authentication timeout - please try again");
                await StopCallbackServerAsync();
                return false;
            }
            
            var result = await callbackTask;
            await StopCallbackServerAsync();
            
            if (result)
            {
                _logger?.LogInfo($"✓✓✓ Steam authentication successful!");
                _logger?.LogInfo($"  Steam ID: {_steamId}");
                SaveSteamAuth();
                SteamAuthenticated?.Invoke(this, _steamId ?? string.Empty);
                return true;
            }
            else
            {
                SteamAuthenticationFailed?.Invoke(this, "Steam authentication failed");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during Steam authentication: {ex.Message}", ex);
            SteamAuthenticationFailed?.Invoke(this, $"Error: {ex.Message}");
            await StopCallbackServerAsync();
            return false;
        }
    }
    
    private async Task<bool> ListenForSteamCallbackAsync()
    {
        try
        {
            _logger?.LogInfo("Waiting for Steam callback...");
            
            var context = await _callbackServer!.GetContextAsync();
            var request = context.Request;
            var response = context.Response;
            
            _logger?.LogInfo($"Received callback: {request.Url}");
            
            // Extract Steam ID from callback
            var queryString = request.QueryString;
            var steamId = ExtractSteamIdFromCallback(queryString);
            
            if (!string.IsNullOrWhiteSpace(steamId))
            {
                _steamId = steamId;
                
                // Send success response to browser
                var successHtml = @"
<!DOCTYPE html>
<html>
<head>
    <title>Steam Authentication Successful</title>
    <style>
        body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #1b2838; color: #c7d5e0; }
        .success { color: #66c0f4; font-size: 24px; margin-bottom: 20px; }
        .message { font-size: 16px; }
    </style>
</head>
<body>
    <div class=""success"">✓ Steam Authentication Successful!</div>
    <div class=""message"">You can close this window and return to the application.</div>
    <script>setTimeout(function(){ window.close(); }, 2000);</script>
</body>
</html>";
                
                var buffer = Encoding.UTF8.GetBytes(successHtml);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();
                
                _logger?.LogInfo($"✓ Steam ID extracted: {steamId}");
                return true;
            }
            else
            {
                // Send error response
                var errorHtml = @"
<!DOCTYPE html>
<html>
<head>
    <title>Steam Authentication Failed</title>
    <style>
        body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #1b2838; color: #c7d5e0; }
        .error { color: #ff6b6b; font-size: 24px; margin-bottom: 20px; }
    </style>
</head>
<body>
    <div class=""error"">✗ Authentication Failed</div>
    <div>Please try again.</div>
</body>
</html>";
                
                var buffer = Encoding.UTF8.GetBytes(errorHtml);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();
                
                _logger?.LogError("Failed to extract Steam ID from callback");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error listening for Steam callback: {ex.Message}", ex);
            return false;
        }
    }
    
    private string? ExtractSteamIdFromCallback(System.Collections.Specialized.NameValueCollection queryString)
    {
        try
        {
            // Steam OpenID returns the Steam ID in the claimed_id parameter
            // Format: https://steamcommunity.com/openid/id/7656119XXXXXXXXXX
            var claimedId = queryString["openid.claimed_id"];
            if (!string.IsNullOrWhiteSpace(claimedId))
            {
                var match = Regex.Match(claimedId, @"https://steamcommunity\.com/openid/id/(\d+)");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            
            // Also check identity parameter
            var identity = queryString["openid.identity"];
            if (!string.IsNullOrWhiteSpace(identity))
            {
                var match = Regex.Match(identity, @"https://steamcommunity\.com/openid/id/(\d+)");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error extracting Steam ID: {ex.Message}", ex);
            return null;
        }
    }
    
    private int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
    
    private Task StopCallbackServerAsync()
    {
        try
        {
            if (_callbackServer != null)
            {
                _callbackServer.Stop();
                _callbackServer.Close();
                _callbackServer = null;
                _logger?.LogInfo("Callback server stopped");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error stopping callback server: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
    
    private void LoadSteamAuth()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<SteamAuthConfig>(json);
                
                if (config != null)
                {
                    _steamId = config.SteamId;
                    _steamSessionToken = config.SessionToken;
                    _logger?.LogDebug("Loaded Steam auth from file");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error loading Steam auth: {ex.Message}", ex);
        }
    }
    
    private void SaveSteamAuth()
    {
        try
        {
            var config = new SteamAuthConfig
            {
                SteamId = _steamId ?? string.Empty,
                SessionToken = _steamSessionToken ?? string.Empty,
                AuthenticatedAt = DateTime.UtcNow
            };
            
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
            
            _logger?.LogDebug("Saved Steam auth to file");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error saving Steam auth: {ex.Message}", ex);
        }
    }
    
    private class SteamAuthConfig
    {
        public string SteamId { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public DateTime AuthenticatedAt { get; set; }
    }

    public async Task<ServerInfo?> PairWithServerAsync(string ipAddress, int gamePort, string steamId)
    {
        try
        {
            _logger?.LogInfo("=== Starting server pairing process ===");
            _logger?.LogInfo($"Server IP: {ipAddress}");
            _logger?.LogInfo($"Game Port: {gamePort}");
            _logger?.LogInfo($"Steam ID: {steamId}");
            
            // Calculate Rust+ port (typically gamePort + 67, or try common ports)
            var rustPlusPorts = new List<int>();
            if (gamePort > 0)
            {
                rustPlusPorts.Add(gamePort + 67); // Standard calculation (most common) - THIS IS THE KEY!
                rustPlusPorts.Add(gamePort + 68); // Sometimes +68 instead of +67
                rustPlusPorts.Add(gamePort + 66); // Sometimes +66
                rustPlusPorts.Add(gamePort + 1);   // Alternative
                rustPlusPorts.Add(gamePort - 1);   // Alternative
            }
            // Common Rust+ ports
            rustPlusPorts.Add(28082); // Default Rust+ port
            rustPlusPorts.Add(28083); // Alternative default
            rustPlusPorts.Add(28084); // Another common port
            rustPlusPorts.Add(28015); // Sometimes same as game port
            rustPlusPorts.Add(28016); // Sometimes game port + 1
            
            // Remove duplicates and sort
            rustPlusPorts = rustPlusPorts.Distinct().OrderBy(p => p).ToList();
            
            _logger?.LogInfo($"Will try Rust+ ports: {string.Join(", ", rustPlusPorts)}");
            _logger?.LogInfo($"Calculated from game port {gamePort} (most likely: {gamePort + 67})");
            _logger?.LogInfo("");
            _logger?.LogInfo("⚠️ IMPORTANT: Make sure Rust+ is enabled in-game FIRST!");
            _logger?.LogInfo("   1. In Rust, press F1 to open console");
            _logger?.LogInfo("   2. Type: rustplus.enabled true");
            _logger?.LogInfo("   3. Wait 10-15 seconds for Rust+ to initialize");
            _logger?.LogInfo("   4. Then initiate pairing: ESC → Rust+ → Pair");
            _logger?.LogInfo("");
                   
                   ClientWebSocket? webSocket = null;
                   Exception? lastException = null;
                   
                   // Try both ws:// and wss:// protocols
                   var protocols = new[] { "ws", "wss" };
                   
                   // First, try to check if Rust+ port is actually listening by doing a quick TCP connection test
                   _logger?.LogInfo("🔍 Checking which Rust+ port is actually listening...");
                   var listeningPorts = new List<int>();
                   
                   foreach (var rustPlusPort in rustPlusPorts)
                   {
                       try
                       {
                           using var tcpClient = new TcpClient();
                           var tcpConnectTask = tcpClient.ConnectAsync(ipAddress, rustPlusPort);
                           var tcpTimeoutTask = Task.Delay(TimeSpan.FromMilliseconds(500));
                           var tcpCompletedTask = await Task.WhenAny(tcpConnectTask, tcpTimeoutTask);
                           
                           if (tcpCompletedTask == tcpConnectTask && tcpClient.Connected)
                           {
                               _logger?.LogInfo($"  ✓ Port {rustPlusPort} is listening (TCP connection successful)");
                               listeningPorts.Add(rustPlusPort);
                               tcpClient.Close();
                           }
                       }
                       catch
                       {
                           // Port not listening, continue
                       }
                   }
                   
                   if (listeningPorts.Count > 0)
                   {
                       _logger?.LogInfo($"✓ Found {listeningPorts.Count} listening port(s): {string.Join(", ", listeningPorts)}");
                       _logger?.LogInfo("  Trying WebSocket connection to these ports first...");
                       rustPlusPorts = listeningPorts.Concat(rustPlusPorts.Where(p => !listeningPorts.Contains(p))).ToList();
                   }
                   else
                   {
                       _logger?.LogWarning("⚠ No Rust+ ports are listening!");
                       _logger?.LogWarning("  This usually means Rust+ is not enabled in-game.");
                       _logger?.LogWarning("  → Press F1 in-game and type: rustplus.enabled true");
                       _logger?.LogWarning("  → Wait 10-15 seconds, then try again");
                   }
                   
                   foreach (var rustPlusPort in rustPlusPorts)
                   {
                       foreach (var protocol in protocols)
                       {
                           try
                           {
                               _logger?.LogInfo($"Trying Rust+ port: {rustPlusPort} ({protocol}://) (timeout: 5s)...");
                               
                               var uri = new Uri($"{protocol}://{ipAddress}:{rustPlusPort}/");
                               _logger?.LogDebug($"Connecting to: {uri}");
                               
                               webSocket = new ClientWebSocket();
                               webSocket.Options.SetRequestHeader("User-Agent", "RustDesktop/1.0");
                               webSocket.Options.SetRequestHeader("Origin", $"http://{ipAddress}");
                               webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                               
                               _pairingCancellationTokenSource = new CancellationTokenSource();
                               _pairingCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5)); // 5 second timeout per port
                               
                               var connectTask = webSocket.ConnectAsync(uri, _pairingCancellationTokenSource.Token);
                               await connectTask;
                               
                               if (webSocket.State == WebSocketState.Open)
                               {
                                   _logger?.LogInfo($"✓✓✓ Connected to Rust+ endpoint on port {rustPlusPort} ({protocol}://)!");
                                   _pairingWebSocket = webSocket;
                                   goto ConnectionSuccessful; // Break out of both loops
                               }
                               else
                               {
                                   _logger?.LogWarning($"WebSocket connected but state is {webSocket.State}");
                                   webSocket?.Dispose();
                                   webSocket = null;
                               }
                           }
                           catch (OperationCanceledException)
                           {
                               _logger?.LogDebug($"Connection to {protocol}://{ipAddress}:{rustPlusPort} timed out (5s)");
                               webSocket?.Dispose();
                               webSocket = null;
                               continue; // Try next protocol
                           }
                           catch (WebSocketException wsEx)
                           {
                               // If it's a 400 error, the port exists but isn't a WebSocket - log as info
                               if (wsEx.WebSocketErrorCode == WebSocketError.NotAWebSocket)
                               {
                                   _logger?.LogDebug($"Port {rustPlusPort} ({protocol}://) exists but is not a WebSocket endpoint");
                               }
                               else
                               {
                                   _logger?.LogDebug($"WebSocket error on port {rustPlusPort} ({protocol}://): {wsEx.Message}");
                               }
                               lastException = wsEx;
                               webSocket?.Dispose();
                               webSocket = null;
                               continue; // Try next protocol
                           }
                           catch (Exception ex)
                           {
                               _logger?.LogDebug($"Failed to connect on port {rustPlusPort} ({protocol}://): {ex.Message}");
                               lastException = ex;
                               webSocket?.Dispose();
                               webSocket = null;
                               continue; // Try next protocol
                           }
                       }
                   }
                   
                   ConnectionSuccessful:
            
                   if (webSocket == null || webSocket.State != WebSocketState.Open)
                   {
                       var errorMsg = $"Could not connect to Rust+ endpoint. Tried {rustPlusPorts.Count} ports with both ws:// and wss:// protocols.";
                       _logger?.LogError(errorMsg);
                       _logger?.LogError($"Ports tried: {string.Join(", ", rustPlusPorts)}");
                       _logger?.LogError("");
                       _logger?.LogError("Possible reasons:");
                       _logger?.LogError("  1. Server does not have Rust+ enabled (server-side setting)");
                       _logger?.LogError("     → Contact server admin to enable Rust+ on the server");
                       _logger?.LogError("  2. Rust+ is not enabled in-game - you MUST enable it first!");
                       _logger?.LogError("     → Press F1 in-game and type: rustplus.enabled true");
                       _logger?.LogError("     → Wait 10-15 seconds for Rust+ to initialize");
                       _logger?.LogError("     → Then try pairing again");
                       _logger?.LogError("  3. Firewall is blocking the connection");
                       _logger?.LogError("     → Check Windows Firewall settings");
                       _logger?.LogError("     → Check if antivirus is blocking the connection");
                       _logger?.LogError("  4. Rust+ port is different (check server admin panel)");
                       _logger?.LogError("     → Some servers use custom Rust+ ports");
                       _logger?.LogError("     → Ask server admin for the Rust+ port number");
                       _logger?.LogError("");
                       _logger?.LogError("NOTE: Port 27021 responded with HTTP 400 - something is listening there but it's not Rust+");
                       PairingFailed?.Invoke(this, $"{errorMsg}\n\nEnable Rust+ in-game first (F1 → rustplus.enabled true), then wait 10-15 seconds.");
                       return null;
                   }
            
            _logger?.LogInfo("WebSocket connected, starting pairing...");
            
            // Get the actual port that connected (from the WebSocket URI)
            var connectedPort = rustPlusPorts.FirstOrDefault(p => 
            {
                try
                {
                    var testUri = new Uri($"ws://{ipAddress}:{p}/");
                    return webSocket.State == WebSocketState.Open;
                }
                catch { return false; }
            });
            
            // Start listening for pairing messages
            var pairingTask = Task.Run(async () => await ListenForPairingAsync(webSocket, ipAddress, connectedPort > 0 ? connectedPort : rustPlusPorts[0], steamId));
            
            // Send initial pairing request (some servers need this)
            try
            {
                var pairRequest = new Dictionary<string, object>
                {
                    ["type"] = "pair",
                    ["steam_id"] = steamId
                };
                
                var json = JsonSerializer.Serialize(pairRequest);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _pairingCancellationTokenSource?.Token ?? CancellationToken.None
                );
                
                _logger?.LogInfo($"Sent pairing request: {json}");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Failed to send pairing request: {ex.Message}");
            }
            
            // Wait for pairing to complete (with longer timeout - server might need time to process)
            _logger?.LogInfo("Waiting for server to respond with pairing information...");
            _logger?.LogInfo("NOTE: The server may send a notification to your mobile Rust+ app.");
            _logger?.LogInfo("The pairing credentials will be sent via WebSocket to this connection.");
            
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60)); // Increased to 60 seconds
            var completedTask = await Task.WhenAny(pairingTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _logger?.LogError("Pairing timeout - no response from server after 60 seconds");
                _logger?.LogError("The server may require pairing through the mobile Rust+ app first.");
                _logger?.LogError("Try pairing with the mobile app, then the credentials will be available.");
                PairingFailed?.Invoke(this, "Pairing timeout - server did not respond. You may need to pair via mobile app first.");
                await CleanupPairingAsync();
                return null;
            }
            
            var result = await pairingTask;
            await CleanupPairingAsync();
            
            if (result != null)
            {
                _logger?.LogInfo("✓✓✓ Pairing successful!");
                _logger?.LogInfo($"  Server ID: {result.ServerId}");
                _logger?.LogInfo($"  Player Token: {result.PlayerToken?.Substring(0, Math.Min(20, result.PlayerToken?.Length ?? 0))}...");
                
                // Save pairing credentials
                await SavePairingAsync(result);
                
                PairingSuccessful?.Invoke(this, result);
                return result;
            }
            else
            {
                PairingFailed?.Invoke(this, "Pairing failed - could not extract credentials");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during pairing: {ex.Message}", ex);
            PairingFailed?.Invoke(this, $"Pairing error: {ex.Message}");
            await CleanupPairingAsync();
            return null;
        }
    }
    
    private async Task<ServerInfo?> ListenForPairingAsync(ClientWebSocket webSocket, string ipAddress, int port, string steamId)
    {
        try
        {
            _logger?.LogInfo("Listening for pairing messages...");
            
            var buffer = new byte[65536];
            var messageBuilder = new StringBuilder();
            
            while (webSocket.State == WebSocketState.Open && !(_pairingCancellationTokenSource?.Token.IsCancellationRequested ?? false))
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    _pairingCancellationTokenSource?.Token ?? CancellationToken.None
                );
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger?.LogInfo("Server closed connection during pairing");
                    break;
                }
                
                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                
                if (result.EndOfMessage)
                {
                    var message = messageBuilder.ToString();
                    messageBuilder.Clear();
                    
                    _logger?.LogInfo($"Received message during pairing: {message}");
                    
                    // Try to parse pairing response
                    var serverInfo = ParsePairingMessage(message, ipAddress, port, steamId);
                    if (serverInfo != null)
                    {
                        return serverInfo;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error listening for pairing: {ex.Message}", ex);
        }
        
        return null;
    }
    
    private ServerInfo? ParsePairingMessage(string message, string ipAddress, int port, string steamId)
    {
        try
        {
            var root = JsonDocument.Parse(message).RootElement;
            
            if (!root.TryGetProperty("type", out var type))
                return null;
            
            var messageType = type.GetString();
            _logger?.LogInfo($"Pairing message type: {messageType}");
            
            var serverInfo = new ServerInfo
            {
                IpAddress = ipAddress,
                Port = port,
                SteamId = steamId
            };
            
            // Extract Server ID
            if (root.TryGetProperty("serverId", out var serverId))
            {
                serverInfo.ServerId = serverId.GetString() ?? string.Empty;
                _logger?.LogInfo($"✓ Got Server ID: {serverInfo.ServerId}");
            }
            else if (root.TryGetProperty("server_id", out var serverId2))
            {
                serverInfo.ServerId = serverId2.GetString() ?? string.Empty;
                _logger?.LogInfo($"✓ Got Server ID: {serverInfo.ServerId}");
            }
            
            // Extract Player Token
            if (root.TryGetProperty("playerToken", out var playerToken))
            {
                serverInfo.PlayerToken = playerToken.GetString() ?? string.Empty;
                _logger?.LogInfo($"✓ Got Player Token: {serverInfo.PlayerToken.Substring(0, Math.Min(20, serverInfo.PlayerToken.Length))}...");
            }
            else if (root.TryGetProperty("player_token", out var playerToken2))
            {
                serverInfo.PlayerToken = playerToken2.GetString() ?? string.Empty;
                _logger?.LogInfo($"✓ Got Player Token: {serverInfo.PlayerToken.Substring(0, Math.Min(20, serverInfo.PlayerToken.Length))}...");
            }
            
            // Extract Server Name
            if (root.TryGetProperty("name", out var name))
            {
                serverInfo.Name = name.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("serverName", out var serverName))
            {
                serverInfo.Name = serverName.GetString() ?? string.Empty;
            }
            
            // Check if we have both required credentials
            if (!string.IsNullOrWhiteSpace(serverInfo.ServerId) && !string.IsNullOrWhiteSpace(serverInfo.PlayerToken))
            {
                _logger?.LogInfo("✓✓✓ Complete pairing credentials received!");
                return serverInfo;
            }
            
            // Some servers might send credentials in separate messages
            // Check for any pairing-related data
            if (messageType == "pair" || messageType == "pairing" || messageType == "auth" || messageType == "pair_response")
            {
                _logger?.LogInfo("Received pairing response message");
                // Continue listening for more messages
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error parsing pairing message: {ex.Message}", ex);
            return null;
        }
    }
    
    public async Task<bool> SavePairingAsync(ServerInfo serverInfo)
    {
        try
        {
            _logger?.LogInfo("Saving pairing credentials...");
            
            // Save to registry (Windows)
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Facepunch Studios LTD\Rust");
                if (key != null)
                {
                    key.SetValue("ServerId", serverInfo.ServerId ?? string.Empty, RegistryValueKind.String);
                    key.SetValue("PlayerToken", serverInfo.PlayerToken ?? string.Empty, RegistryValueKind.String);
                    if (!string.IsNullOrWhiteSpace(serverInfo.IpAddress))
                        key.SetValue("ServerIp", serverInfo.IpAddress, RegistryValueKind.String);
                    if (serverInfo.Port > 0)
                        key.SetValue("ServerPort", serverInfo.Port.ToString(), RegistryValueKind.String);
                    
                    _logger?.LogInfo("✓ Saved pairing credentials to registry");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Failed to save to registry: {ex.Message}");
            }
            
            // Also save to file as backup
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configDir = Path.Combine(appDataPath, "RustDesktop");
                Directory.CreateDirectory(configDir);
                var configPath = Path.Combine(configDir, "pairing.json");
                
                var config = new
                {
                    ServerId = serverInfo.ServerId,
                    PlayerToken = serverInfo.PlayerToken,
                    IpAddress = serverInfo.IpAddress,
                    Port = serverInfo.Port,
                    ServerName = serverInfo.Name,
                    PairedAt = DateTime.UtcNow
                };
                
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configPath, json);
                
                _logger?.LogInfo("✓ Saved pairing credentials to file");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Failed to save to file: {ex.Message}");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error saving pairing: {ex.Message}", ex);
            return false;
        }
    }
    
    public void ClearPairing()
    {
        try
        {
            // Clear from registry
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Facepunch Studios LTD\Rust", true);
                if (key != null)
                {
                    key.DeleteValue("ServerId", false);
                    key.DeleteValue("PlayerToken", false);
                    key.DeleteValue("ServerIp", false);
                    key.DeleteValue("ServerPort", false);
                }
            }
            catch { }
            
            // Clear from file
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configPath = Path.Combine(appDataPath, "RustDesktop", "pairing.json");
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
            catch { }
            
            _logger?.LogInfo("Pairing cleared");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error clearing pairing: {ex.Message}", ex);
        }
    }
    
    private async Task CleanupPairingAsync()
    {
        try
        {
            if (_pairingWebSocket != null)
            {
                if (_pairingWebSocket.State == WebSocketState.Open)
                {
                    await _pairingWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Pairing complete", CancellationToken.None);
                }
                _pairingWebSocket.Dispose();
                _pairingWebSocket = null;
            }
            
            _pairingCancellationTokenSource?.Cancel();
            _pairingCancellationTokenSource?.Dispose();
            _pairingCancellationTokenSource = null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error cleaning up pairing connection: {ex.Message}");
        }
    }
}
