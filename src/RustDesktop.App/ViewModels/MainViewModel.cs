using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RustDesktop.Core.Models;
using RustDesktop.Core.Services;
using RustDesktop.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;

namespace RustDesktop.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IRustPlusService _rustPlusService;
    private readonly IRustDataService _rustDataService;
    private readonly IActiveSessionService _activeSessionService;
    private readonly IPairingService _pairingService;
    private readonly IPairingListener? _pairingListener;
    private readonly ILoggingService? _logger;
    private readonly IItemNameService? _itemNameService;
    private readonly IconValidationService? _iconValidationService;
    private readonly INotificationService? _notificationService;
    private DispatcherTimer? _vendingMachinePollTimer;
    private CancellationTokenSource? _vendingMachinePollCts;
    private DispatcherTimer? _searchDebounceTimer;

    [ObservableProperty]
    private ServerInfo? _currentServer;

    [ObservableProperty]
    private bool _isConnected;
    
    [ObservableProperty]
    private bool _isAuthenticated;
    
    [ObservableProperty]
    private bool _isSteamConnected;

    [ObservableProperty]
    private MapInfo? _mapInfo;

    [ObservableProperty]
    private ObservableCollection<VendingMachine> _vendingMachines = new();

    [ObservableProperty]
    private ObservableCollection<SmartDevice> _smartDevices = new();

    [ObservableProperty]
    private List<TeamMember> _teamMembers = new();

    [ObservableProperty]
    private TeamInfo? _teamInfo;

    [ObservableProperty]
    private string _statusMessage = "Not connected";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private ObservableCollection<string> _logMessages = new();

    [ObservableProperty]
    private bool _showLogs = true;

    [ObservableProperty]
    private string _logText = string.Empty;

    // Search/Filter properties
    [ObservableProperty]
    private bool _filterHasStock = false;

    [ObservableProperty]
    private bool _filterSells = false;

    [ObservableProperty]
    private bool _filterBuys = false;

    [ObservableProperty]
    private string _searchItemName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<VendingMachine> _filteredVendingMachines = new();

    [ObservableProperty]
    private int _filteredResultsCount = 0;

    [ObservableProperty]
    private string _filteredResultsText = "Results: 0";

    [ObservableProperty]
    private ObservableCollection<string> _availableItemNames = new();

    [ObservableProperty]
    private ObservableCollection<AlarmNotification> _raidAlerts = new();

    [ObservableProperty]
    private ObservableCollection<WorldEvent> _worldEvents = new();

    partial void OnFilterHasStockChanged(bool value) => FilterVendingMachines();
    partial void OnFilterSellsChanged(bool value) => FilterVendingMachines();
    partial void OnFilterBuysChanged(bool value) => FilterVendingMachines();
    
    partial void OnSearchItemNameChanged(string value)
    {
        // Debounce search to avoid filtering on every keystroke
        if (_searchDebounceTimer == null)
        {
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300) // Wait 300ms after user stops typing
            };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                FilterVendingMachines();
            };
        }
        
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }
    
    partial void OnVendingMachinesChanged(ObservableCollection<VendingMachine> value)
    {
        FilterVendingMachines();
        UpdateAvailableItemNames();
    }
    
    private void UpdateAvailableItemNames()
    {
        try
        {
            if (VendingMachines == null)
            {
                AvailableItemNames.Clear();
                return;
            }

            var uniqueItems = VendingMachines
                .Where(vm => vm != null && vm.Items != null)
                .SelectMany(vm => vm.Items)
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ItemName))
                .Select(item => item.ItemName)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            AvailableItemNames.Clear();
            foreach (var item in uniqueItems)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    AvailableItemNames.Add(item);
                }
            }
        }
        catch
        {
            // Silently handle any errors to prevent crashes
            AvailableItemNames.Clear();
        }
    }

    public MainViewModel(IRustPlusService rustPlusService, IRustDataService rustDataService, IActiveSessionService activeSessionService, IPairingService pairingService, IPairingListener? pairingListener = null, ILoggingService? logger = null, IItemNameService? itemNameService = null, IconValidationService? iconValidationService = null, INotificationService? notificationService = null)
    {
        _rustPlusService = rustPlusService;
        _rustDataService = rustDataService;
        _activeSessionService = activeSessionService;
        _pairingService = pairingService;
        _pairingListener = pairingListener;
        _logger = logger;
        _itemNameService = itemNameService;
        _iconValidationService = iconValidationService;
        _notificationService = notificationService;
        _rustPlusService.MapUpdated += OnMapUpdated;
        _rustPlusService.VendingMachinesUpdated += OnVendingMachinesUpdated;
        _rustPlusService.WorldEventsUpdated += OnWorldEventsUpdated;
        _rustPlusService.TeamInfoUpdated += OnTeamInfoUpdated;
        _rustPlusService.ErrorOccurred += OnErrorOccurred;
        _rustPlusService.ServerInfoUpdated += OnServerInfoUpdated;
        
        _pairingService.PairingSuccessful += OnPairingSuccessful;
        _pairingService.PairingFailed += OnPairingFailed;
        _pairingService.SteamAuthenticated += OnSteamAuthenticated;
        _pairingService.SteamAuthenticationFailed += OnSteamAuthenticationFailed;
        
        // Subscribe to FCM pairing listener events
        if (_pairingListener != null)
        {
            _pairingListener.Paired += OnFcmPairingReceived;
            _pairingListener.Failed += OnFcmPairingFailed;
            _pairingListener.Listening += OnFcmListening;
            _pairingListener.Stopped += OnFcmStopped;
            _pairingListener.AlarmReceived += OnAlarmReceived;
        }
        
        // Check initial Steam authentication status
        _ = CheckSteamStatusAsync();
        
        // Subscribe to log events
        if (_logger != null)
        {
            _logger.LogAdded += OnLogAdded;
            // Load only recent 100 logs to avoid cluttering UI on startup
            var recentLogs = _logger.GetRecentLogs(100);
            foreach (var log in recentLogs)
            {
                LogMessages.Add(log);
            }
            UpdateLogText();
        }
    }

    private void OnLogAdded(object? sender, string logMessage)
    {
        // Ensure UI updates happen on UI thread
        if (Dispatcher.CurrentDispatcher.CheckAccess())
        {
            LogMessages.Add(logMessage);
            // Keep only last 1000 logs to allow copying more at a time
            while (LogMessages.Count > 1000)
            {
                LogMessages.RemoveAt(0);
            }
            UpdateLogText();
        }
        else
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                LogMessages.Add(logMessage);
                // Keep only last 1000 logs to allow copying more at a time
                while (LogMessages.Count > 1000)
                {
                    LogMessages.RemoveAt(0);
                }
                UpdateLogText();
            });
        }
    }

    private void UpdateLogText()
    {
        LogText = string.Join(Environment.NewLine, LogMessages);
    }

    public void ClearLogs()
    {
        LogMessages.Clear();
        LogText = string.Empty;
    }

    public string GetAllLogsText()
    {
        return LogText;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ConnectAsync()
    {
        IsLoading = true;
        StatusMessage = "Checking prerequisites...";
        IsConnected = false;

        try
        {
            _logger?.LogInfo("=== Starting connection process ===");
            
            // STEP 1: Check if Steam is connected
            var isSteamAuth = await _pairingService.IsSteamAuthenticatedAsync();
            if (!isSteamAuth)
            {
                StatusMessage = "Please connect Steam first! Click 'Connect Steam' button.";
                _logger?.LogError("Steam not authenticated - cannot connect");
                return;
            }
            _logger?.LogInfo("✓ Steam is authenticated");
            
            // STEP 2: Check if server is paired (has Server ID + Player Token)
            StatusMessage = "Checking pairing status...";
            _logger?.LogInfo("=== STEP 2: Checking for paired server credentials ===");
            
            // ALWAYS prefer CurrentServer if it exists and has valid credentials
            // This ensures we use the newly paired server, not old cached data from registry
            ServerInfo? serverInfo = null;
            
            // CRITICAL: Always use CurrentServer if it exists and has PlayerToken
            // This is the most recently paired server and takes absolute priority
            if (CurrentServer != null && !string.IsNullOrWhiteSpace(CurrentServer.PlayerToken))
            {
                _logger?.LogInfo("✓✓✓ Found CurrentServer with valid PlayerToken - using it (recently paired server)");
                _logger?.LogInfo($"  Server: {CurrentServer.Name ?? CurrentServer.IpAddress}");
                _logger?.LogInfo($"  IP: {CurrentServer.IpAddress}");
                _logger?.LogInfo($"  Port: {CurrentServer.Port}");
                _logger?.LogInfo($"  ServerId: {CurrentServer.ServerId ?? "(will be obtained during connection)"}");
                _logger?.LogInfo("✓✓✓ Using CurrentServer - IGNORING any saved pairing data from registry/files");
                serverInfo = CurrentServer;
            }
            else if (CurrentServer != null)
            {
                _logger?.LogWarning("⚠ CurrentServer exists but has no PlayerToken - this shouldn't happen after pairing");
                _logger?.LogWarning($"  CurrentServer IP: {CurrentServer.IpAddress}, Port: {CurrentServer.Port}");
                _logger?.LogWarning("⚠ Will check saved pairing data as fallback, but this might be the OLD server!");
            }
            else
            {
                _logger?.LogWarning("⚠⚠⚠ CurrentServer is NULL - this means no recent pairing was stored!");
                _logger?.LogWarning("⚠ This will fall back to registry/files which might have OLD server data!");
            }
            
            // Only fall back to saved pairing data if CurrentServer is null or invalid
            if (serverInfo == null)
            {
                _logger?.LogInfo("CurrentServer not available or invalid, checking saved pairing data...");
                _logger?.LogWarning("⚠⚠⚠ WARNING: This might return an OLD server from registry if you just paired a new one!");
                var pairedServer = await _rustDataService.GetPairedServerAsync();
                
                if (pairedServer == null || string.IsNullOrWhiteSpace(pairedServer.PlayerToken))
                {
                    StatusMessage = "Please pair with server first! Click 'Pair Server' button.";
                    _logger?.LogError("Server not paired - cannot connect");
                    _logger?.LogError("   Workflow: 1. Connect Steam → 2. Pair Server → 3. Connect to Rust+");
                    IsLoading = false;
                    return;
                }
                
                serverInfo = pairedServer;
                _logger?.LogInfo($"✓ Using saved paired server from registry/files: {pairedServer.IpAddress}:{pairedServer.Port}");
                _logger?.LogWarning("⚠⚠⚠ If this is the OLD server, the issue is that CurrentServer was NULL!");
                _logger?.LogWarning("⚠ Make sure you paired the NEW server and CurrentServer was set!");
            }
            
            // Final validation and logging
            if (serverInfo != null)
            {
                _logger?.LogInfo($"=== FINAL SERVER SELECTION ===");
                _logger?.LogInfo($"  Using server: {serverInfo.IpAddress}:{serverInfo.Port}");
                _logger?.LogInfo($"  Server name: {serverInfo.Name ?? "Unknown"}");
                _logger?.LogInfo($"  Source: {(ReferenceEquals(serverInfo, CurrentServer) ? "CurrentServer (recently paired)" : "Registry/Files (saved pairing)")}");
            }
            
            // Server ID is optional - we can get it during connection
            if (serverInfo != null && string.IsNullOrWhiteSpace(serverInfo.ServerId))
            {
                _logger?.LogInfo("⚠ Server ID not found, but will attempt connection (Server ID may be obtained during connection)");
            }
            _logger?.LogInfo("✓ Server is paired");
            
            StatusMessage = "Detecting active Rust server session...";
            
            // If we don't have valid credentials, try to get active server session as fallback
            if (serverInfo == null || string.IsNullOrWhiteSpace(serverInfo.PlayerToken))
            {
                _logger?.LogWarning("⚠ No paired server found with Server ID + Player Token");
                _logger?.LogWarning("Authentication will likely FAIL without these credentials");
                
                // Fallback: Try to get active server session
                _logger?.LogInfo("=== STEP 2: Trying active server session detection ===");
                var isRustRunning = _activeSessionService.IsRustRunning();
                _logger?.LogInfo($"Rust is running: {isRustRunning}");
                
                if (isRustRunning)
                {
                    StatusMessage = "Rust is running. Detecting server connection...";
                    _logger?.LogInfo("Attempting to get active server session...");
                    serverInfo = await _activeSessionService.GetActiveServerSessionAsync();
                    
                    if (serverInfo != null)
                    {
                        _logger?.LogInfo($"Active session found: {serverInfo.IpAddress}:{serverInfo.Port}");
                        _logger?.LogError("⚠⚠⚠ WARNING: No Server ID or Player Token available!");
                        _logger?.LogError("The server will likely REJECT authentication!");
                    }
                    else
                    {
                        _logger?.LogWarning("No active session found from ActiveSessionService");
                    }
                }
                else
                {
                    _logger?.LogWarning("Rust is not running");
                }
            }

            if (serverInfo == null || string.IsNullOrWhiteSpace(serverInfo.IpAddress))
            {
                var errorMsg = "No server found. Please join a Rust server first, or ensure Rust+ is enabled.";
                StatusMessage = errorMsg;
                _logger?.LogError(errorMsg);
                _logger?.LogInfo("Check the log file for detailed detection information");
                IsLoading = false;
                return;
            }

            _logger?.LogInfo($"Using server: {serverInfo.IpAddress}:{serverInfo.Port}");

            // Get Steam ID if not already set
            if (string.IsNullOrWhiteSpace(serverInfo.SteamId))
            {
                StatusMessage = "Getting Steam ID...";
                _logger?.LogDebug("Getting Steam ID...");
                var steamId = await _rustDataService.GetSteamIdAsync();
                if (!string.IsNullOrWhiteSpace(steamId))
                {
                    serverInfo.SteamId = steamId;
                    _logger?.LogInfo($"Steam ID retrieved: {steamId}");
                }
                else
                {
                    _logger?.LogWarning("Could not retrieve Steam ID");
                }
            }

            // Ensure we have a valid port (Rust+ default is 28082)
            if (serverInfo.Port == 0)
            {
                _logger?.LogDebug("Port is 0, getting Rust+ port...");
                var rustPlusPort = await _activeSessionService.GetCurrentServerRustPlusPortAsync();
                serverInfo.Port = rustPlusPort ?? 28082;
                _logger?.LogInfo($"Using Rust+ port: {serverInfo.Port}");
            }

            StatusMessage = $"Connecting to {serverInfo.IpAddress}:{serverInfo.Port}...";
            _logger?.LogInfo($"Attempting connection to {serverInfo.IpAddress}:{serverInfo.Port}");
            var success = await _rustPlusService.ConnectAsync(serverInfo);

            if (success)
            {
                // Only update CurrentServer if it's null or if we're connecting to a different server
                // This preserves the newly paired server even if we fell back to registry
                if (CurrentServer == null || 
                    CurrentServer.IpAddress != serverInfo.IpAddress || 
                    CurrentServer.Port != serverInfo.Port)
                {
                    _logger?.LogInfo($"Updating CurrentServer to connected server: {serverInfo.IpAddress}:{serverInfo.Port}");
                    CurrentServer = serverInfo;
                }
                else
                {
                    _logger?.LogInfo($"CurrentServer already matches connected server, keeping it");
                }
                IsConnected = _rustPlusService.IsConnected;
                IsAuthenticated = _rustPlusService.IsAuthenticated;
                
                // Set default name if not provided
                if (string.IsNullOrWhiteSpace(serverInfo.Name))
                {
                    serverInfo.Name = serverInfo.IpAddress;
                }
                
                if (IsAuthenticated)
                {
                    StatusMessage = $"✓ Connected and authenticated to {serverInfo.Name}";
                    _logger?.LogInfo($"✓✓✓ Successfully connected and authenticated to {serverInfo.IpAddress}:{serverInfo.Port}");
                    
                    // Automatically load initial data only if authenticated
                    await LoadDataAsync();
                    
                    // Start periodic polling for vending machines
                    StartVendingMachinePolling();
                }
                else
                {
                    StatusMessage = $"⚠ Connected to {serverInfo.IpAddress} but NOT authenticated";
                    _logger?.LogWarning($"⚠ Connected to {serverInfo.IpAddress}:{serverInfo.Port} but authentication failed");
                }
            }
            else
            {
                CurrentServer = null;
                IsConnected = false;
                IsAuthenticated = false;
                var errorMsg = "❌ Connection failed - Server rejected authentication or connection closed";
                StatusMessage = errorMsg;
                _logger?.LogError(errorMsg);
                _logger?.LogError("Possible reasons:");
                _logger?.LogError("  1. Server requires Server ID + Player Token (pair with mobile app first)");
                _logger?.LogError("  2. Server has Rust+ disabled");
                _logger?.LogError("  3. Authentication message format is incorrect");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error: {ex.Message}";
            StatusMessage = errorMsg;
            _logger?.LogError("Exception in ConnectAsync", ex);
        }
        finally
        {
            IsLoading = false;
            _logger?.LogInfo("=== Connection process completed ===");
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task PairServerAsync()
    {
        IsLoading = true;
        StatusMessage = "Starting server pairing...";
        
        try
        {
            _logger?.LogInfo("=== Starting server pairing process ===");
            
            // STEP 1: Check if Steam is connected
            var isSteamAuth = await _pairingService.IsSteamAuthenticatedAsync();
            if (!isSteamAuth)
            {
                StatusMessage = "Please connect Steam first! Click 'Connect Steam' button.";
                _logger?.LogError("Steam not authenticated - cannot pair");
                return;
            }
            
            // STEP 2: Get Steam ID from authenticated session
            var steamId = _pairingService.GetSteamId();
            if (string.IsNullOrWhiteSpace(steamId))
            {
                StatusMessage = "Steam ID not found. Please reconnect Steam.";
                _logger?.LogError("Steam ID not found - cannot pair");
                return;
            }
            
            _logger?.LogInfo($"Using authenticated Steam ID: {steamId}");
            
            // STEP 2: Check for existing pairing credentials (from mobile app or in-game pairing)
            // BUT: Only use them if CurrentServer is not already set (from a recent pairing)
            if (CurrentServer == null || string.IsNullOrWhiteSpace(CurrentServer.PlayerToken))
            {
                _logger?.LogInfo("CurrentServer is null or invalid, checking for existing pairing credentials...");
                _logger?.LogInfo("Looking for credentials stored by mobile Rust+ app or in-game pairing...");
                var existingPairing = await _rustDataService.GetPairedServerAsync();
                
                // Accept pairing if we have Player Token, IP, and Port (Server ID is optional)
                if (existingPairing != null && !string.IsNullOrWhiteSpace(existingPairing.PlayerToken) 
                    && !string.IsNullOrWhiteSpace(existingPairing.IpAddress) && existingPairing.Port > 0)
                {
                    StatusMessage = $"✓ Found existing pairing! Server: {existingPairing.IpAddress ?? "Unknown"}";
                    _logger?.LogInfo("✓✓✓ Found existing pairing credentials!");
                    _logger?.LogInfo($"  Server ID: {existingPairing.ServerId ?? "(will be obtained during connection)"}");
                    _logger?.LogInfo($"  Player Token: {existingPairing.PlayerToken.Substring(0, Math.Min(20, existingPairing.PlayerToken.Length))}...");
                    _logger?.LogInfo($"  IP: {existingPairing.IpAddress}");
                    _logger?.LogInfo($"  Port: {existingPairing.Port}");
                    
                    // Ensure Steam ID is set
                    if (string.IsNullOrWhiteSpace(existingPairing.SteamId))
                    {
                        existingPairing.SteamId = steamId;
                    }
                    
                    CurrentServer = existingPairing;
                    
                    // Automatically connect to the found pairing
                    _logger?.LogInfo("Automatically connecting to paired server...");
                    StatusMessage = "Connecting to paired server...";
                    
                    try
                    {
                        var success = await _rustPlusService.ConnectAsync(existingPairing);
                        
                        if (success)
                        {
                            IsConnected = _rustPlusService.IsConnected;
                            IsAuthenticated = _rustPlusService.IsAuthenticated;
                            
                            if (IsAuthenticated)
                            {
                                StatusMessage = $"✓ Connected and authenticated to {existingPairing.Name ?? existingPairing.IpAddress}";
                                _logger?.LogInfo($"✓✓✓ Successfully connected and authenticated!");
                                
                                // Automatically load initial data
                                await LoadDataAsync();
                            }
                            else
                            {
                                StatusMessage = $"⚠ Connected but NOT authenticated";
                                _logger?.LogWarning($"⚠ Connected but authentication failed");
                            }
                        }
                        else
                        {
                            StatusMessage = "❌ Connection failed";
                            _logger?.LogError("Connection failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Connection error: {ex.Message}";
                        _logger?.LogError($"Connection error: {ex.Message}", ex);
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                    
                    return;
                }
            }
            else
            {
                _logger?.LogInfo("CurrentServer already set - skipping registry check to preserve newly paired server");
            }
            
            // STEP 3: No existing pairing found - try FCM listener or guide user to mobile app pairing
            _logger?.LogWarning("⚠ No existing pairing credentials found");
            _logger?.LogInfo("");
            
            // Try to start FCM listener (requires Node.js)
            bool fcmAvailable = false;
            if (_pairingListener != null)
            {
                try
                {
                    _logger?.LogInfo("=== ATTEMPTING FCM PAIRING LISTENER ===");
                    _logger?.LogInfo("Starting FCM listener to receive pairing notifications...");
                    _logger?.LogInfo("You need to initiate pairing in-game:");
                    _logger?.LogInfo("  1. Make sure Rust is running and connected to server");
                    _logger?.LogInfo("  2. Press ESC → Rust+ → Pair");
                    _logger?.LogInfo("  3. The pairing info will be received via FCM");
                    _logger?.LogInfo("");
                    
                    StatusMessage = "Starting FCM listener...";
                    
                    await _pairingListener.StartAsync();
                    _logger?.LogInfo("✓ FCM listener started successfully!");
                    _logger?.LogInfo("Waiting for pairing notification...");
                    StatusMessage = "FCM listener active. Initiate pairing in-game (ESC → Rust+ → Pair)";
                    fcmAvailable = true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to start FCM listener: {ex.Message}");
                    _logger?.LogInfo("");
                    _logger?.LogInfo("═══════════════════════════════════════════════════════");
                    _logger?.LogInfo("  FCM LISTENER REQUIRES NODE.JS");
                    _logger?.LogInfo("═══════════════════════════════════════════════════════");
                    _logger?.LogInfo("");
                    _logger?.LogInfo("Node.js is not installed on your system.");
                    _logger?.LogInfo("");
                    _logger?.LogInfo("OPTION 1 (Recommended - No Node.js needed):");
                    _logger?.LogInfo("  1. Install Rust+ mobile app on your phone");
                    _logger?.LogInfo("  2. Open Rust+ app and pair with your server");
                    _logger?.LogInfo("  3. Come back to this app and click 'Pair Server' again");
                    _logger?.LogInfo("  4. The app will automatically detect the pairing from mobile app");
                    _logger?.LogInfo("");
                    _logger?.LogInfo("OPTION 2 (If you want in-game pairing):");
                    _logger?.LogInfo("  1. Install Node.js from: https://nodejs.org/ (LTS version)");
                    _logger?.LogInfo("  2. Restart this application");
                    _logger?.LogInfo("  3. Click 'Pair Server' again");
                    _logger?.LogInfo("  4. Then pair in-game (ESC → Rust+ → Pair)");
                    _logger?.LogInfo("");
                    _logger?.LogInfo("═══════════════════════════════════════════════════════");
                    _logger?.LogInfo("");
                    
                    StatusMessage = "Node.js not found. Please pair via mobile app (see logs for instructions)";
                    
                    // Show user-friendly message box
                    System.Windows.MessageBox.Show(
                        "Node.js is required for in-game pairing.\n\n" +
                        "RECOMMENDED: Pair via Rust+ mobile app instead:\n" +
                        "1. Install Rust+ app on your phone\n" +
                        "2. Pair with your server in the mobile app\n" +
                        "3. Click 'Pair Server' again in this app\n\n" +
                        "OR install Node.js from nodejs.org for in-game pairing.",
                        "Node.js Not Found",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            
            if (!fcmAvailable)
            {
                _logger?.LogWarning("FCM listener not available. Please pair via mobile app first.");
                StatusMessage = "Please pair via mobile Rust+ app first, then click 'Pair Server' again";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Pairing error: {ex.Message}";
            _logger?.LogError($"Error during pairing: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async void OnPairingSuccessful(object? sender, ServerInfo serverInfo)
    {
        await Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
        {
            StatusMessage = $"Successfully paired with {serverInfo.IpAddress}!";
            // Clear old pairing data first to ensure we use the new server
            _pairingService.ClearPairing();
            // Save the new pairing
            await _pairingService.SavePairingAsync(serverInfo);
            // Set CurrentServer to the newly paired server
            CurrentServer = serverInfo;
            _logger?.LogInfo($"✓✓✓ New server paired and set as CurrentServer: {serverInfo.IpAddress}:{serverInfo.Port}");
        });
    }
    
    private void OnPairingFailed(object? sender, string error)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            StatusMessage = $"Pairing failed: {error}";
        });
    }
    
    private void OnSteamAuthenticated(object? sender, string steamId)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            IsSteamConnected = true;
            StatusMessage = $"✓ Steam authenticated: {steamId}";
        });
    }
    
    private void OnSteamAuthenticationFailed(object? sender, string error)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            IsSteamConnected = false;
            StatusMessage = $"Steam authentication failed: {error}";
        });
    }
    
    private void OnFcmPairingReceived(object? sender, PairingPayload payload)
    {
        Dispatcher.CurrentDispatcher.Invoke(async () =>
        {
            _logger?.LogInfo("✓✓✓ FCM Pairing received!");
            _logger?.LogInfo($"  Server: {payload.Host}:{payload.Port}");
            _logger?.LogInfo($"  Server Name: {payload.ServerName ?? "Unknown"}");
            _logger?.LogInfo($"  Steam ID: {payload.SteamId64}");
            _logger?.LogInfo($"  Player Token: {payload.PlayerToken.Substring(0, Math.Min(20, payload.PlayerToken.Length))}...");
            
            // Convert PairingPayload to ServerInfo
            var serverInfo = new ServerInfo
            {
                IpAddress = payload.Host,
                Port = payload.Port,
                ServerId = "", // FCM pairing doesn't provide Server ID, we'll get it on connect
                PlayerToken = payload.PlayerToken,
                SteamId = payload.SteamId64,
                Name = payload.ServerName ?? payload.Host,
                GamePort = payload.Port // May need adjustment
            };
            
            // Clear old pairing data first to ensure we use the new server
            _pairingService.ClearPairing();
            // Save the new pairing credentials
            await _pairingService.SavePairingAsync(serverInfo);
            
            CurrentServer = serverInfo;
            StatusMessage = $"✓ Successfully paired with {serverInfo.Name ?? serverInfo.IpAddress}!";
            _logger?.LogInfo($"✓✓✓ New server paired via FCM and set as CurrentServer: {serverInfo.IpAddress}:{serverInfo.Port}");
            _logger?.LogInfo("Pairing credentials saved. Automatically connecting...");
            
            // Small delay to ensure pairing is saved to registry
            await Task.Delay(300);
            
            // Automatically attempt connection after pairing
            // Use the saved server info directly since we just saved it
            IsLoading = true;
            StatusMessage = "Connecting to server...";
            
            try
            {
                var success = await _rustPlusService.ConnectAsync(serverInfo);
                
                if (success)
                {
                    IsConnected = _rustPlusService.IsConnected;
                    IsAuthenticated = _rustPlusService.IsAuthenticated;
                    
                    if (IsAuthenticated)
                    {
                        StatusMessage = $"✓ Connected and authenticated to {serverInfo.Name ?? serverInfo.IpAddress}";
                        _logger?.LogInfo($"✓✓✓ Successfully connected and authenticated!");
                        
                        // Automatically load initial data
                        await LoadDataAsync();
                    }
                    else
                    {
                        StatusMessage = $"⚠ Connected but NOT authenticated";
                        _logger?.LogWarning($"⚠ Connected but authentication failed");
                    }
                }
                else
                {
                    StatusMessage = "❌ Connection failed";
                    _logger?.LogError("Connection failed");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection error: {ex.Message}";
                _logger?.LogError($"Connection error: {ex.Message}", ex);
            }
            finally
            {
                IsLoading = false;
            }
        });
    }
    
    private void OnFcmPairingFailed(object? sender, string error)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            _logger?.LogError($"FCM pairing error: {error}");
            StatusMessage = $"FCM error: {error}";
        });
    }
    
    private void OnFcmListening(object? sender, EventArgs e)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            _logger?.LogInfo("✓ FCM listener is now listening for pairing notifications");
            StatusMessage = "FCM listener active. Initiate pairing in-game (ESC → Rust+ → Pair)";
        });
    }
    
    private void OnFcmStopped(object? sender, EventArgs e)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            _logger?.LogWarning("FCM listener stopped");
            StatusMessage = "FCM listener stopped";
        });
    }

    private void OnAlarmReceived(object? sender, AlarmNotification alarm)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            _logger?.LogWarning($"🚨 RAID ALERT RECEIVED:");
            _logger?.LogWarning($"  Device: {alarm.DeviceName}");
            _logger?.LogWarning($"  Message: {alarm.Message}");
            _logger?.LogWarning($"  Server: {alarm.Server}");
            _logger?.LogWarning($"  Entity ID: {alarm.EntityId}");
            _logger?.LogWarning($"  Timestamp: {alarm.Timestamp}");
            
            // Add to raid alerts collection
            RaidAlerts.Insert(0, alarm); // Add to beginning for newest first
            
            _logger?.LogInfo($"✓ Raid alert added. Total alerts: {RaidAlerts.Count}");
            
            // Keep only last 50 alerts
            while (RaidAlerts.Count > 50)
            {
                RaidAlerts.RemoveAt(RaidAlerts.Count - 1);
            }
            
            // Show desktop notification with 5 second delay for testing
            _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
            {
                _notificationService?.ShowNotification(
                    "🚨 Raid Alert",
                    $"{alarm.DeviceName}: {alarm.Message}",
                    null
                );
            });
            
            StatusMessage = $"🚨 Raid Alert: {alarm.DeviceName}";
        });
    }

    [RelayCommand]
    private void TestRaidAlert()
    {
        // Create a test raid alert with realistic data
        var testAlarm = new AlarmNotification(
            DateTime.Now,
            CurrentServer?.Name ?? CurrentServer?.IpAddress ?? "Test Server",
            "Test Alarm Device",
            12345,
            "Test raid alert - Your entity has been destroyed!"
        );
        
        _logger?.LogInfo("=== TEST: Raid Alert ===");
        _logger?.LogInfo($"Device: {testAlarm.DeviceName}");
        _logger?.LogInfo($"Message: {testAlarm.Message}");
        _logger?.LogInfo($"Server: {testAlarm.Server}");
        _logger?.LogInfo($"Timestamp: {testAlarm.Timestamp}");
        
        OnAlarmReceived(this, testAlarm);
        _logger?.LogInfo("✓ Test raid alert added to collection. Check Raid Alerts panel.");
    }

    [RelayCommand]
    private void TestWorldEvent()
    {
        _logger?.LogInfo("=== TEST: World Events ===");
        
        // Test different event types with realistic coordinates
        var testEvents = new List<WorldEvent>
        {
            new WorldEvent
            {
                EventType = "Cargo Ship",
                X = 2250f, // Center of map
                Y = 2250f,
                SpawnTime = DateTime.Now
            },
            new WorldEvent
            {
                EventType = "Patrol Helicopter",
                X = 1500f,
                Y = 3000f,
                SpawnTime = DateTime.Now.AddMinutes(-2)
            },
            new WorldEvent
            {
                EventType = "Chinook Crate",
                X = 3500f,
                Y = 1000f,
                SpawnTime = DateTime.Now.AddMinutes(-5)
            },
            new WorldEvent
            {
                EventType = "Locked Crate",
                X = 800f,
                Y = 3800f,
                SpawnTime = DateTime.Now.AddSeconds(-30)
            }
        };
        
        foreach (var evt in testEvents)
        {
            evt.GridCoordinate = CalculateGridCoordinate(evt.X, evt.Y);
            _logger?.LogInfo($"Event: {evt.EventType} at Grid {evt.GridCoordinate} ({evt.X:F0}, {evt.Y:F0}) - {evt.TimeAgo}");
        }
        
        // Simulate the event handler being called
        OnWorldEventsUpdated(this, testEvents);
        _logger?.LogInfo($"✓ Added {testEvents.Count} test world events. Check World Events panel.");
        StatusMessage = $"✓ Test: Added {testEvents.Count} world events";
    }
    
    private async Task CheckSteamStatusAsync()
    {
        try
        {
            var isSteamAuth = await _pairingService.IsSteamAuthenticatedAsync();
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                IsSteamConnected = isSteamAuth;
                if (isSteamAuth)
                {
                    var steamId = _pairingService.GetSteamId();
                    StatusMessage = $"✓ Steam connected: {steamId}";
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error checking Steam status: {ex.Message}", ex);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AuthenticateSteamAsync()
    {
        IsLoading = true;
        StatusMessage = "Starting Steam authentication...";
        
        try
        {
            _logger?.LogInfo("=== Starting Steam authentication ===");
            
            var success = await _pairingService.AuthenticateWithSteamAsync();
            
            if (success)
            {
                var steamId = _pairingService.GetSteamId();
                IsSteamConnected = true;
                StatusMessage = $"✓ Steam authenticated: {steamId}. You can now pair with a server.";
                _logger?.LogInfo($"✓✓✓ Steam authentication successful: {steamId}");
            }
            else
            {
                IsSteamConnected = false;
                StatusMessage = "Steam authentication failed. Check logs for details.";
                _logger?.LogError("Steam authentication failed");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Steam authentication error: {ex.Message}";
            _logger?.LogError($"Error during Steam authentication: {ex.Message}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task DisconnectAsync()
    {
        // Stop periodic polling
        StopVendingMachinePolling();
        
        await _rustPlusService.DisconnectAsync();
        IsConnected = false;
        IsAuthenticated = false;
        
        // Clear CurrentServer when disconnecting to ensure fresh pairing on next connect
        _logger?.LogInfo("Disconnecting - clearing CurrentServer to ensure fresh pairing on next connect");
        CurrentServer = null;
        
        StatusMessage = "Disconnected";
        MapInfo = null;
        VendingMachines.Clear();
        SmartDevices.Clear();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshMapAsync()
    {
        if (!IsConnected) return;

        StatusMessage = "Refreshing map...";
        var map = await _rustPlusService.GetMapAsync();
        if (map != null)
        {
            MapInfo = map;
            StatusMessage = "Map refreshed";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshVendingMachinesAsync()
    {
        if (!IsConnected) return;

        try
        {
            StatusMessage = "Refreshing vending machines...";
            _logger?.LogInfo("Refreshing vending machines...");
            
            var machines = await _rustPlusService.GetVendingMachinesAsync();
            
            if (Dispatcher.CurrentDispatcher.CheckAccess())
            {
                VendingMachines.Clear();
                foreach (var machine in machines)
                {
                    VendingMachines.Add(machine);
                }
            }
            else
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    VendingMachines.Clear();
                    foreach (var machine in machines)
                    {
                        VendingMachines.Add(machine);
                    }
                });
            }
            
            StatusMessage = $"Found {machines.Count} vending machines";
            _logger?.LogInfo($"✓ Found {machines.Count} vending machines");
            
            if (machines.Count > 0)
            {
                _logger?.LogInfo($"Vending machines: {string.Join(", ", machines.Select(vm => $"{vm.Name} ({vm.X}, {vm.Y})"))}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing vending machines: {ex.Message}";
            _logger?.LogError($"Error refreshing vending machines: {ex.Message}", ex);
        }
    }

    private async Task LoadDataAsync()
    {
        await RefreshMapAsync();
        await RefreshVendingMachinesAsync();
        await RefreshTeamInfoAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshShopsAsync()
    {
        await RefreshVendingMachinesAsync();
        // Filter will be automatically triggered by OnVendingMachinesChanged
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshTeamInfoAsync()
    {
        if (!IsConnected) return;

        try
        {
            var teamInfo = await _rustPlusService.GetTeamInfoAsync();
            if (teamInfo != null)
            {
                TeamMembers.Clear();
                foreach (var member in teamInfo.Members)
                {
                    TeamMembers.Add(member);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error refreshing team info: {ex.Message}");
        }
    }

    private void OnMapUpdated(object? sender, MapInfo mapInfo)
    {
        // Ensure UI updates happen on UI thread
        if (Dispatcher.CurrentDispatcher.CheckAccess())
        {
            MapInfo = mapInfo;
            StatusMessage = "Map updated";
        }
        else
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                MapInfo = mapInfo;
                StatusMessage = "Map updated";
            });
        }
    }

    private void OnWorldEventsUpdated(object? sender, List<WorldEvent> events)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            _logger?.LogInfo($"=== WORLD EVENTS UPDATE: Received {events.Count} events ===");
            
            var newEventsCount = 0;
            var updatedEventsCount = 0;
            
            // Update existing events or add new ones
            foreach (var evt in events)
            {
                // Calculate grid coordinate
                evt.GridCoordinate = CalculateGridCoordinate(evt.X, evt.Y);
                
                _logger?.LogInfo($"Processing: {evt.EventType} at Grid {evt.GridCoordinate} ({evt.X:F0}, {evt.Y:F0})");
                
                // Check if event already exists (same type and location)
                var existing = WorldEvents.FirstOrDefault(e => 
                    e.EventType == evt.EventType && 
                    Math.Abs(e.X - evt.X) < 10 && 
                    Math.Abs(e.Y - evt.Y) < 10);
                
                if (existing != null)
                {
                    // Update spawn time if it's a new spawn
                    existing.SpawnTime = evt.SpawnTime;
                    updatedEventsCount++;
                    _logger?.LogInfo($"  → Updated existing event (was {existing.TimeAgo})");
                }
                else
                {
                    // Add new event
                    WorldEvents.Insert(0, evt);
                    newEventsCount++;
                    _logger?.LogInfo($"  → Added NEW event");
                    
                    // Show notification for new world events with 5 second delay for testing
                    var eventType = evt.EventType;
                    var gridCoord = evt.GridCoordinate;
                    _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                    {
                        _notificationService?.ShowNotification(
                            "🌍 World Event",
                            $"{eventType} spawned at {gridCoord}",
                            null
                        );
                    });
                }
            }
            
            _logger?.LogInfo($"✓ World Events Update Complete: {newEventsCount} new, {updatedEventsCount} updated. Total: {WorldEvents.Count}");
            
            // Keep only last 50 events
            while (WorldEvents.Count > 50)
            {
                WorldEvents.RemoveAt(WorldEvents.Count - 1);
            }
        });
    }

    private string CalculateGridCoordinate(float x, float y)
    {
        // Use the same logic as GridCoordinateConverter
        const float DefaultWorldSize = 4500f;
        const float CellSize = 150f;
        
        double worldSizeD = DefaultWorldSize;
        int cells = Math.Max(1, (int)Math.Round(worldSizeD / CellSize));
        double cell = worldSizeD / cells;
        
        double offsetX = cell * 0.3;
        double offsetY = cell * 2.5;
        
        int col = Math.Clamp((int)Math.Floor((x + offsetX) / cell), 0, cells - 1);
        int row = Math.Clamp((int)Math.Floor((worldSizeD - y + offsetY) / cell), 0, cells - 1);
        
        string letter = ColumnLabel(col);
        int number = row + 1;
        return $"{letter}{number}";
    }

    private static string ColumnLabel(int index)
    {
        var s = "";
        index++;
        while (index > 0)
        {
            index--;
            s = (char)('A' + (index % 26)) + s;
            index /= 26;
        }
        return s;
    }

    private void OnVendingMachinesUpdated(object? sender, List<VendingMachine> machines)
    {
        // Ensure UI updates happen on UI thread
        if (Dispatcher.CurrentDispatcher.CheckAccess())
        {
            VendingMachines.Clear();
            foreach (var machine in machines)
            {
                VendingMachines.Add(machine);
            }
            
            // Test all icons and log errors
            TestIconsForVendingMachines(machines);
        }
        else
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                VendingMachines.Clear();
                foreach (var machine in machines)
                {
                    VendingMachines.Add(machine);
                }
                
                // Test all icons and log errors
                TestIconsForVendingMachines(machines);
            });
        }
    }

    private void TestIconsForVendingMachines(List<VendingMachine> machines)
    {
        if (_iconValidationService == null || machines == null || machines.Count == 0)
        {
            return;
        }

        // Run icon testing in background to avoid blocking UI
        Task.Run(() =>
        {
            try
            {
                _logger?.LogInfo("Starting icon validation for all vending machine items...");
                var summary = _iconValidationService.TestAllIcons(machines);
                
                _logger?.LogInfo($"Icon validation complete: {summary.IconsFound}/{summary.TotalItems} icons found, {summary.IconsMissing} missing");
                
                if (summary.IconsMissing > 0)
                {
                    _logger?.LogWarning($"Found {summary.IconsMissing} items with missing icons. Check icon-errors.log for details.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error during icon validation", ex);
            }
        });
    }

    private void OnTeamInfoUpdated(object? sender, TeamInfo teamInfo)
    {
        // Ensure UI updates happen on UI thread
        if (Dispatcher.CurrentDispatcher.CheckAccess())
        {
            TeamInfo = teamInfo;
            TeamMembers = teamInfo.Members.ToList();
        }
        else
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                TeamInfo = teamInfo;
                TeamMembers = teamInfo.Members.ToList();
            });
        }
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        // Ensure UI updates happen on UI thread
        if (Dispatcher.CurrentDispatcher.CheckAccess())
        {
            StatusMessage = $"Error: {error}";
        }
        else
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                StatusMessage = $"Error: {error}";
            });
        }
    }

    private void OnServerInfoUpdated(object? sender, ServerInfo serverInfo)
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            if (CurrentServer != null)
            {
                CurrentServer.Name = serverInfo.Name;
                CurrentServer.ServerId = serverInfo.ServerId;
                
                // Update connection status
                IsConnected = _rustPlusService.IsConnected;
                IsAuthenticated = _rustPlusService.IsAuthenticated;
                
                if (IsAuthenticated)
                {
                    StatusMessage = $"✓ Connected & Authenticated: {serverInfo.Name ?? serverInfo.IpAddress}";
                }
                else
                {
                    StatusMessage = $"Connected to {serverInfo.Name ?? serverInfo.IpAddress}";
                }
                
                _logger?.LogInfo($"Server info updated: {serverInfo.Name}");
            }
        });
    }

    private void StartVendingMachinePolling()
    {
        StopVendingMachinePolling(); // Stop any existing timer
        
        _vendingMachinePollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30) // Poll every 30 seconds
        };
        _vendingMachinePollTimer.Tick += async (s, e) =>
        {
            if (IsConnected && !IsLoading)
            {
                await RefreshVendingMachinesAsync();
            }
        };
        _vendingMachinePollTimer.Start();
        _logger?.LogInfo("Started periodic vending machine polling (every 30 seconds)");
    }

    private void StopVendingMachinePolling()
    {
        if (_vendingMachinePollTimer != null)
        {
            _vendingMachinePollTimer.Stop();
            _vendingMachinePollTimer = null;
            _logger?.LogInfo("Stopped periodic vending machine polling");
        }
        
        _vendingMachinePollCts?.Cancel();
        _vendingMachinePollCts = null;
    }

    private void FilterVendingMachines()
    {
        if (VendingMachines == null)
        {
            FilteredVendingMachines.Clear();
            FilteredResultsCount = 0;
            FilteredResultsText = "Results: 0";
            return;
        }

        var filtered = VendingMachines.AsEnumerable();

        // Filter by stock status
        if (FilterHasStock)
        {
            filtered = filtered.Where(vm => vm.IsActive && vm.Items != null && vm.Items.Count > 0);
        }

        // Filter by item name - also filter out shops with empty quantities for the searched item
        if (!string.IsNullOrWhiteSpace(SearchItemName))
        {
            var searchTerm = SearchItemName.Trim().ToLowerInvariant();
            filtered = filtered.Where(vm => 
            {
                if (vm.Items == null) return false;
                
                // Find items matching the search term
                var matchingItems = vm.Items.Where(item => 
                    item.ItemName.ToLowerInvariant().Contains(searchTerm)
                ).ToList();
                
                // Only include shops that have matching items with quantity > 0
                return matchingItems.Any(item => item.Quantity > 0);
            });
        }

        // Filter by sells (items being sold - currently all items are sell orders)
        if (FilterSells)
        {
            filtered = filtered.Where(vm => 
                vm.Items != null && vm.Items.Count > 0);
        }

        // Filter by buys (items being bought)
        if (FilterBuys)
        {
            filtered = filtered.Where(vm => 
                vm.BuyItems != null && vm.BuyItems.Count > 0);
        }

        FilteredVendingMachines.Clear();
        foreach (var vm in filtered)
        {
            FilteredVendingMachines.Add(vm);
        }
        
        // Count vending machines (not items)
        FilteredResultsCount = FilteredVendingMachines.Count;
        
        // Update results text to show number of vending machines
        if (!string.IsNullOrWhiteSpace(SearchItemName))
        {
            var machineCount = FilteredResultsCount;
            var itemName = SearchItemName.Trim();
            if (machineCount == 1)
            {
                FilteredResultsText = $"1 vending machine sells '{itemName}'";
            }
            else
            {
                FilteredResultsText = $"{machineCount} vending machines sell '{itemName}'";
            }
        }
        else
        {
            if (FilteredResultsCount == 1)
            {
                FilteredResultsText = "1 vending machine";
            }
            else
            {
                FilteredResultsText = $"{FilteredResultsCount} vending machines";
            }
        }
    }

    private void UpdateFilteredResultsText(int machineCount)
    {
        FilteredResultsCount = machineCount;
        
        // Update results text to show number of vending machines
        if (!string.IsNullOrWhiteSpace(SearchItemName))
        {
            var itemName = SearchItemName.Trim();
            if (machineCount == 1)
            {
                FilteredResultsText = $"1 vending machine sells '{itemName}'";
            }
            else
            {
                FilteredResultsText = $"{machineCount} vending machines sell '{itemName}'";
            }
        }
        else
        {
            if (machineCount == 1)
            {
                FilteredResultsText = "1 vending machine";
            }
            else
            {
                FilteredResultsText = $"{machineCount} vending machines";
            }
        }
    }
}

