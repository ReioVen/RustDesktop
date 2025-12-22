using RustPlusApi;
using RustDesktop.Core.Models;
using System.Reflection;

namespace RustDesktop.Core.Services;

/// <summary>
/// Service for communicating with Rust+ servers via RustPlusApi library
/// This matches the working version's RustPlusClientReal implementation
/// </summary>
public class RustPlusService : IRustPlusService, IDisposable
{
    private RustPlus? _api;
    private ServerInfo? _currentServer;
    private bool _isConnected;
    private bool _isAuthenticated;
    private bool _useProxyCurrent;
    private readonly ILoggingService? _logger;
    private readonly IServerInfoService? _serverInfoService;
    private readonly IItemNameService? _itemNameService;
    
    public bool IsConnected => _isConnected && _api != null;
    public bool IsAuthenticated => _isAuthenticated;

    public event EventHandler<MapInfo>? MapUpdated;
    public event EventHandler<List<VendingMachine>>? VendingMachinesUpdated;
    public event EventHandler<TeamInfo>? TeamInfoUpdated;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<List<WorldEvent>>? WorldEventsUpdated;
#pragma warning disable CS0067 // Event is never used - reserved for future use
    public event EventHandler<ServerInfo>? ServerInfoUpdated;
#pragma warning restore CS0067

    public RustPlusService(ILoggingService? logger = null, IServerInfoService? serverInfoService = null, IItemNameService? itemNameService = null)
    {
        _logger = logger;
        _serverInfoService = serverInfoService;
        _itemNameService = itemNameService;
    }

    public async Task<bool> ConnectAsync(ServerInfo serverInfo)
    {
        try
        {
            _currentServer = serverInfo;
            
            if (string.IsNullOrWhiteSpace(serverInfo.SteamId))
            {
                _logger?.LogError("Steam ID is required for connection");
                throw new ArgumentException("Steam ID is required", nameof(serverInfo));
            }
            
            if (!ulong.TryParse(serverInfo.SteamId, out var steamId))
            {
                _logger?.LogError($"Invalid Steam ID format: {serverInfo.SteamId}");
                throw new ArgumentException($"Invalid Steam ID format: {serverInfo.SteamId}", nameof(serverInfo));
            }
            
            if (string.IsNullOrWhiteSpace(serverInfo.PlayerToken))
            {
                _logger?.LogError("Player Token is required for connection");
                throw new ArgumentException("Player Token is required", nameof(serverInfo));
            }
            
            if (!int.TryParse(serverInfo.PlayerToken, out var playerToken))
            {
                _logger?.LogError($"Invalid Player Token format: {serverInfo.PlayerToken}");
                throw new ArgumentException($"Invalid Player Token format: {serverInfo.PlayerToken}", nameof(serverInfo));
            }

            _logger?.LogInfo($"Connecting to {serverInfo.IpAddress}:{serverInfo.Port}...");
            _logger?.LogInfo($"Steam ID: {steamId}, Player Token: {playerToken}");

            // Try connection with proxy fallback (like working version)
            async Task<(bool ok, string? err)> TryAsync(bool useProxy)
            {
                _api = new RustPlus(serverInfo.IpAddress, serverInfo.Port, steamId, playerToken, useProxy);

                // Call ConnectAsync if available
                try
                {
                    var mConnect = _api.GetType().GetMethod("ConnectAsync", new[] { typeof(CancellationToken) })
                                 ?? _api.GetType().GetMethod("ConnectAsync", Type.EmptyTypes);
                    if (mConnect != null)
                    {
                        var res = mConnect.GetParameters().Length == 1
                            ? mConnect.Invoke(_api, new object[] { CancellationToken.None })
                            : mConnect.Invoke(_api, Array.Empty<object>());
                        if (res is Task t) await t;
                    }
                        }
                        catch (Exception ex)
                        {
                    _logger?.LogWarning($"ConnectAsync call failed: {ex.Message}"); 
                }

                // Test connection with GetInfoAsync
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                    var infoTask = _api.GetInfoAsync();
                    var done = await Task.WhenAny(infoTask, Task.Delay(7000, cts.Token));
                    if (done != infoTask) return (false, "Timeout");

                    var info = infoTask.Result;
                    if (info?.IsSuccess == true)
                    {
                        _logger?.LogInfo($"Authenticated – {(useProxy ? "via Facepunch proxy" : "direct")}.");
                        return (true, null);
                    }
                    return (false, info?.Error?.Message ?? "no response / error");
                        }
                        catch (Exception ex)
                        {
                    return (false, ex.Message); 
                }
            }

            // Try direct first, then proxy (or vice versa based on preference)
            var useProxyFirst = false; // Can be made configurable
            
            var (ok1, err1) = await TryAsync(useProxyFirst);
            if (ok1)
            {
                _useProxyCurrent = useProxyFirst;
                _isConnected = true;
                _isAuthenticated = true;
                HookEvents();
                return true;
            }

            var (ok2, err2) = await TryAsync(!useProxyFirst);
            if (ok2)
            {
                _useProxyCurrent = !useProxyFirst;
                _isConnected = true;
                _isAuthenticated = true;
                HookEvents();
                return true;
            }

            _logger?.LogError($"GetInfo (Path1: {(useProxyFirst ? "Proxy" : "Direct")}): {err1}");
            _logger?.LogError($"GetInfo (Path2: {(!useProxyFirst ? "Proxy" : "Direct")}): {err2}");
            throw new InvalidOperationException("Rust+ not reachable (direct & proxy).");
                }
                catch (Exception ex)
                {
            _logger?.LogError($"Connection failed: {ex.Message}", ex);
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
            _isConnected = false;
            _isAuthenticated = false;
            return false;
        }
    }

    private void HookEvents()
    {
        if (_api == null) return;
        
        try
        {
            // Hook into RustPlusApi events if available
            // The working version hooks events here for map updates, etc.
            _logger?.LogDebug("Events hooked");
                }
                catch (Exception ex)
                {
            _logger?.LogWarning($"Failed to hook events: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        try 
        { 
            if (_api != null) 
            {
                await _api.DisconnectAsync();
            }
            }
            catch (Exception ex)
            {
            _logger?.LogWarning($"Error during disconnect: {ex.Message}");
        }
        finally 
        { 
            _api = null; 
                    _isConnected = false;
                    _isAuthenticated = false;
        }
        _logger?.LogInfo("Disconnected.");
    }

    public async Task<MapInfo?> GetMapAsync()
    {
        if (_api == null) return null;

        try
        {
            var t = _api.GetType();
            var mMap = t.GetMethod("GetMapAsync", new[] { typeof(CancellationToken) })
                     ?? t.GetMethod("GetMapAsync", Type.EmptyTypes);
            if (mMap == null) return null;

            object? call = mMap.GetParameters().Length == 1
                ? mMap.Invoke(_api, new object[] { CancellationToken.None })
                : mMap.Invoke(_api, Array.Empty<object>());

            if (call is not Task task) return null;
            await task.ConfigureAwait(false);

            var result = task.GetType().GetProperty("Result")?.GetValue(task);
            var data = result?.GetType().GetProperty("Data")?.GetValue(result) ?? result;
            if (data == null) return null;

            // Extract image bytes
            byte[]? bytes = null;
            var imageProps = new[] { "PngImage", "JpgImage", "Image", "Bytes", "Data" };
            foreach (var propName in imageProps)
            {
                var prop = data.GetType().GetProperty(propName, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
                if (prop != null)
                {
                    var value = prop.GetValue(data);
                    if (value is byte[] b) bytes = b;
                    else if (value is System.Collections.Generic.IEnumerable<byte> en) bytes = en.ToArray();
                }
            }

            if (bytes == null || bytes.Length == 0) return null;

            // Get dimensions
            int width = 0, height = 0;
            var widthProp = data.GetType().GetProperty("Width");
            var heightProp = data.GetType().GetProperty("Height");
            if (widthProp != null) width = Convert.ToInt32(widthProp.GetValue(data) ?? 0);
            if (heightProp != null) height = Convert.ToInt32(heightProp.GetValue(data) ?? 0);

            // If dimensions not available, use defaults (typical Rust map is 4096x4096)
            if (width == 0 || height == 0)
            {
                width = 4096;
                height = 4096;
            }

            var mapInfo = new MapInfo
            {
                Width = width > 0 ? width : 4096,
                Height = height > 0 ? height : 4096,
                ImageData = bytes,
                Markers = new List<MapMarker>()
            };

            // Get markers and extract world events from raw marker data
            var markers = await GetMapMarkersAsync();
            if (markers != null)
            {
                mapInfo.Markers = markers;
            }
            
            // Extract world events from raw API marker data
            await ExtractWorldEventsFromApiAsync();

            MapUpdated?.Invoke(this, mapInfo);
            return mapInfo;
                }
                catch (Exception ex)
                {
            _logger?.LogError($"Error getting map: {ex.Message}", ex);
            ErrorOccurred?.Invoke(this, $"Failed to get map: {ex.Message}");
            return null;
        }
    }

    private async Task<List<MapMarker>?> GetMapMarkersAsync()
    {
        if (_api == null) return null;

        try
        {
            var t = _api.GetType();
            var m = t.GetMethod("GetMapMarkersAsync", new[] { typeof(CancellationToken) })
                 ?? t.GetMethod("GetMapMarkersAsync", Type.EmptyTypes)
                 ?? t.GetMethod("GetMapMarkers", Type.EmptyTypes);
            if (m == null) return null;

            object? call = m.GetParameters().Length == 1 
                ? m.Invoke(_api, new object[] { CancellationToken.None }) 
                : m.Invoke(_api, Array.Empty<object>());

            object? result = call;
            if (call is Task task)
            {
                await task.ConfigureAwait(false);
                result = task.GetType().GetProperty("Result")?.GetValue(task);
            }

            var data = ReadProp<object>(result, "Data") ?? result;
            if (data == null) return new List<MapMarker>();

            var markers = new List<MapMarker>();
            var markersProp = ReadProp<object>(data, "Markers", "Marker");
            if (markersProp is System.Collections.IEnumerable markersEnum)
            {
                foreach (var marker in markersEnum)
                {
                    if (marker == null) continue;
                    
                    var x = ReadProp<float>(marker, "X", "PositionX");
                    var y = ReadProp<float>(marker, "Y", "PositionY");
                    var name = ReadProp<string>(marker, "Name", "Label", "Text") ?? "";
                    var type = ReadProp<string>(marker, "Type", "MarkerType") ?? "";

                    markers.Add(new MapMarker
                    {
                        X = x,
                        Y = y,
                        Name = name,
                        Type = type
                    });
                }
            }

            return markers;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error getting map markers: {ex.Message}");
            return new List<MapMarker>();
        }
    }

    public async Task<List<VendingMachine>> GetVendingMachinesAsync()
    {
        if (_api == null)
        {
            _logger?.LogWarning("GetVendingMachinesAsync: API is null");
            return new List<VendingMachine>();
        }

        try
        {
            _logger?.LogInfo("=== GetVendingMachinesAsync: Starting ===");
            var shops = new List<VendingMachine>();

            // Try GetMapMarkersAsync first (vending machines are in map markers)
            var t = _api.GetType();
            var m = t.GetMethod("GetMapMarkersAsync", new[] { typeof(CancellationToken) })
                 ?? t.GetMethod("GetMapMarkersAsync", Type.EmptyTypes)
                 ?? t.GetMethod("GetMapMarkers", Type.EmptyTypes);

            if (m == null)
            {
                _logger?.LogWarning("GetVendingMachinesAsync: GetMapMarkersAsync method not found");
            }
            else
            {
                _logger?.LogInfo($"GetVendingMachinesAsync: Found method {m.Name}, parameters: {m.GetParameters().Length}");
                
                try
                {
                    object? call = m.GetParameters().Length == 1 
                        ? m.Invoke(_api, new object[] { CancellationToken.None }) 
                        : m.Invoke(_api, Array.Empty<object>());

                    object? result = null;
                    if (call is Task task)
                    {
                        await task.ConfigureAwait(false);
                        result = task.GetType().GetProperty("Result")?.GetValue(task);
                    }
                    else
                    {
                        result = call;
                    }

                    _logger?.LogInfo($"GetVendingMachinesAsync: Got result, type: {result?.GetType().Name ?? "null"}");

                    var data = ReadProp<object>(result, "Data") ?? result;
                    if (data == null)
                {
                    _logger?.LogWarning("GetVendingMachinesAsync: data is null");
                }
                else
                {
                    _logger?.LogInfo($"GetVendingMachinesAsync: data type: {data.GetType().Name}");
                    _logger?.LogInfo($"GetVendingMachinesAsync: data properties: {string.Join(", ", data.GetType().GetProperties().Select(p => p.Name))}");

                    // Prefer explicit vending list if present
                    var vend = ReadProp<object>(data, "VendingMachines", "Vending");
                    if (vend != null)
                    {
                        _logger?.LogInfo($"GetVendingMachinesAsync: Found VendingMachines/Vending property, type: {vend.GetType().Name}");
                        if (vend is System.Collections.IEnumerable vendEnum)
                        {
                            var count = vendEnum.Cast<object>().Count();
                            _logger?.LogInfo($"GetVendingMachinesAsync: VendingMachines collection has {count} items");
                            ExtractVendingMachines(vendEnum, shops);
                            _logger?.LogInfo($"GetVendingMachinesAsync: After extracting from VendingMachines, found {shops.Count} shops");
                        }
                    }
                    else
                    {
                        _logger?.LogInfo("GetVendingMachinesAsync: VendingMachines/Vending property not found");
                    }

                    // Also check Markers collection (vending machines are often in the general markers list)
                    if (shops.Count == 0)
                    {
                        var markers = ReadProp<object>(data, "Markers", "Marker");
                        if (markers != null)
                        {
                            _logger?.LogInfo($"GetVendingMachinesAsync: Found Markers/Marker property, type: {markers.GetType().Name}");
                            if (markers is System.Collections.IEnumerable markersEnum)
                            {
                                var count = markersEnum.Cast<object>().Count();
                                _logger?.LogInfo($"GetVendingMachinesAsync: Markers collection has {count} items");
                                ExtractVendingMachines(markersEnum, shops);
                                _logger?.LogInfo($"GetVendingMachinesAsync: After extracting from Markers, found {shops.Count} shops");
                            }
                        }
                        else
                        {
                            _logger?.LogInfo("GetVendingMachinesAsync: Markers/Marker property not found");
                        }
                    }

                    // Fallback: scan all IEnumerable properties
                    if (shops.Count == 0)
                    {
                        _logger?.LogInfo("GetVendingMachinesAsync: Scanning all IEnumerable properties as fallback");
                        foreach (var prop in data.GetType().GetProperties())
                        {
                            var v = prop.GetValue(data);
                            if (v is System.Collections.IEnumerable en && v is not string)
                            {
                                var count = en.Cast<object>().Count();
                                _logger?.LogInfo($"GetVendingMachinesAsync: Checking property {prop.Name} (type: {v.GetType().Name}, count: {count})");
                                ExtractVendingMachines(en, shops);
                                if (shops.Count > 0)
                                {
                                    _logger?.LogInfo($"GetVendingMachinesAsync: Found {shops.Count} shops in property {prop.Name}");
                                    break;
                                }
                            }
                        }
                    }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"GetVendingMachinesAsync: Exception in GetMapMarkersAsync (likely unknown marker type): {ex.Message}");
                    _logger?.LogInfo("GetVendingMachinesAsync: Will try fallback AppRequest method");
                    // Continue to fallback method below - shops.Count is still 0
                }
            }

            // Fallback: Use raw AppRequest
            if (shops.Count == 0)
            {
                _logger?.LogInfo("GetVendingMachinesAsync: Trying fallback AppRequest method");
                try
                {
                    var asm = typeof(RustPlus).Assembly;
                    var reqType = asm.GetTypes().FirstOrDefault(x => x.Name.Equals("AppRequest", StringComparison.OrdinalIgnoreCase));
                    var emptyType = asm.GetTypes().FirstOrDefault(x => x.Name.Equals("AppEmpty", StringComparison.OrdinalIgnoreCase));
                    
                    if (reqType == null || emptyType == null)
                    {
                        _logger?.LogWarning("GetVendingMachinesAsync: AppRequest or AppEmpty type not found");
                    }
                    else
                    {
                        _logger?.LogInfo("GetVendingMachinesAsync: Creating AppRequest with GetMapMarkers");
                        var req = Activator.CreateInstance(reqType)!;
                        reqType.GetProperty("GetMapMarkers", BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public)
                               ?.SetValue(req, Activator.CreateInstance(emptyType)!);

                        var send = _api.GetType().GetMethod("SendRequestAsync", new[] { reqType });
                        if (send == null)
                        {
                            _logger?.LogWarning("GetVendingMachinesAsync: SendRequestAsync method not found");
                        }
                        else
                        {
                            _logger?.LogInfo("GetVendingMachinesAsync: Sending AppRequest");
                            var taskObj = send.Invoke(_api, new object[] { req });
                            object? resp = taskObj;
                            if (taskObj is Task tsk)
                            {
                                await tsk.ConfigureAwait(false);
                                resp = tsk.GetType().GetProperty("Result")?.GetValue(tsk);
                            }

                            _logger?.LogInfo($"GetVendingMachinesAsync: Got response, type: {resp?.GetType().Name ?? "null"}");
                            
                            var r = ReadProp<object>(resp, "Response") ?? resp;
                            var mm = ReadProp<object>(r, "MapMarkers") ?? r;
                            
                            if (mm == null)
                            {
                                _logger?.LogWarning("GetVendingMachinesAsync: MapMarkers is null in response");
                            }
                            else
                            {
                                _logger?.LogInfo($"GetVendingMachinesAsync: MapMarkers type: {mm.GetType().Name}");
                                _logger?.LogInfo($"GetVendingMachinesAsync: MapMarkers properties: {string.Join(", ", mm.GetType().GetProperties().Select(p => p.Name))}");
                                
                                // Prefer explicit vending list
                                var vend = ReadProp<object>(mm, "VendingMachines", "Vending");
                                if (vend != null)
                                {
                                    _logger?.LogInfo($"GetVendingMachinesAsync: Found VendingMachines/Vending in MapMarkers, type: {vend.GetType().Name}");
                                    if (vend is System.Collections.IEnumerable vendEnum2)
                                    {
                                        var count = vendEnum2.Cast<object>().Count();
                                        _logger?.LogInfo($"GetVendingMachinesAsync: VendingMachines collection has {count} items");
                                        ExtractVendingMachines(vendEnum2, shops);
                                        _logger?.LogInfo($"GetVendingMachinesAsync: After extracting from VendingMachines (fallback), found {shops.Count} shops");
                                    }
                                }
                                
                                // Also check Markers collection
                                if (shops.Count == 0)
                                {
                                    var markers = ReadProp<object>(mm, "Markers", "Marker");
                                    if (markers != null)
                                    {
                                        _logger?.LogInfo($"GetVendingMachinesAsync: Found Markers/Marker in MapMarkers, type: {markers.GetType().Name}");
                                        if (markers is System.Collections.IEnumerable markersEnum)
                                        {
                                            var count = markersEnum.Cast<object>().Count();
                                            _logger?.LogInfo($"GetVendingMachinesAsync: Markers collection has {count} items");
                                            ExtractVendingMachines(markersEnum, shops);
                                            _logger?.LogInfo($"GetVendingMachinesAsync: After extracting from Markers (fallback), found {shops.Count} shops");
                                        }
                                    }
                                    
                                    // Fallback: scan all properties
                                    if (shops.Count == 0)
                                    {
                                        _logger?.LogInfo("GetVendingMachinesAsync: Scanning all MapMarkers properties as fallback");
                                        foreach (var p in mm.GetType().GetProperties())
                                        {
                                            var v = p.GetValue(mm);
                                            if (v is System.Collections.IEnumerable en && v is not string)
                                            {
                                                var count = en.Cast<object>().Count();
                                                _logger?.LogInfo($"GetVendingMachinesAsync: Checking MapMarkers property {p.Name} (type: {v.GetType().Name}, count: {count})");
                                                ExtractVendingMachines(en, shops);
                                                if (shops.Count > 0)
                                                {
                                                    _logger?.LogInfo($"GetVendingMachinesAsync: Found {shops.Count} shops in MapMarkers property {p.Name}");
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Error in fallback vending machine extraction: {ex.Message}");
                    _logger?.LogError($"Fallback extraction exception: {ex}", ex);
                }
            }

            VendingMachinesUpdated?.Invoke(this, shops);
            return shops;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error getting vending machines: {ex.Message}", ex);
            ErrorOccurred?.Invoke(this, $"Failed to get vending machines: {ex.Message}");
            return new List<VendingMachine>();
        }
    }

    private int MarkerTypeOf(object it)
    {
        try
        {
            if (it == null)
            {
                _logger?.LogDebug("MarkerTypeOf: it is null");
                return 0; // Return 0 for null (invalid type)
            }
            
            _logger?.LogDebug($"MarkerTypeOf: START - Called for type: {it.GetType().Name}");
            
            var p = it.GetType().GetProperty("Type",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.IgnoreCase);

            if (p == null)
            {
                _logger?.LogDebug($"MarkerTypeOf: Property 'Type' not found");
                return -1;
            }

            var v = p.GetValue(it);
            if (v == null)
            {
                _logger?.LogDebug($"MarkerTypeOf: Property value is null");
                return -1;
            }
            
            var propType = p.PropertyType;
            var valueType = v.GetType();
            
            _logger?.LogDebug($"MarkerTypeOf: PropertyType={propType.Name} (IsEnum={propType.IsEnum}), ValueType={valueType.Name} (IsEnum={valueType.IsEnum}), Value={v}, Value.ToString()={v.ToString()}");
        
        // Check if it's an enum (including nullable enums)
        Type? enumType = null;
        if (propType.IsEnum)
        {
            enumType = propType;
        }
        else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(propType);
            if (underlyingType != null && underlyingType.IsEnum)
            {
                enumType = underlyingType;
            }
            }
            
            // CRITICAL: Check string representation FIRST before checking if it's an int
            // This prevents enums with value 0 from being incorrectly identified as int 0
            string? s = null;
            
            // Check if the value itself is an enum type (not just the property type)
            if (valueType.IsEnum)
            {
                // For enum values, ToString() returns the enum name (e.g., "VendingMachine")
                s = v.ToString()?.ToLowerInvariant();
                _logger?.LogDebug($"MarkerTypeOf: Value is enum, ToString()='{s}'");
            }
            else if (enumType != null)
            {
                // Property is enum type but value might be boxed - try GetName first
                var enumName = System.Enum.GetName(enumType, v);
                if (!string.IsNullOrWhiteSpace(enumName))
                {
                    s = enumName.ToLowerInvariant();
                    _logger?.LogDebug($"MarkerTypeOf: Enum GetName='{s}'");
                }
                else
                {
                    s = v.ToString()?.ToLowerInvariant();
                    _logger?.LogDebug($"MarkerTypeOf: Enum GetName returned null, ToString()='{s}'");
                }
            }
            else
            {
                // Not an enum, use ToString()
                s = v.ToString()?.ToLowerInvariant();
                _logger?.LogDebug($"MarkerTypeOf: Not enum, ToString()='{s}'");
            }
            
            // Check for keywords in string representation FIRST (before any int checks)
            if (!string.IsNullOrWhiteSpace(s))
            {
                if (s.Contains("vending"))
                {
                    _logger?.LogDebug($"MarkerTypeOf: Found 'vending' in '{s}', returning 3");
                    return 3;
                }
                if (s.Contains("player"))
                {
                    _logger?.LogDebug($"MarkerTypeOf: Found 'player' in '{s}', returning 1");
                    return 1;
                }
                if (s.Contains("cargo"))
                {
                    _logger?.LogDebug($"MarkerTypeOf: Found 'cargo' in '{s}', returning 5");
                    return 5;
                }
                if (s.Contains("ch47") || s.Contains("chinook"))
                {
                    _logger?.LogDebug($"MarkerTypeOf: Found 'ch47'/'chinook' in '{s}', returning 4");
                    return 4;
                }
                if (s.Contains("patrol"))
                {
                    _logger?.LogDebug($"MarkerTypeOf: Found 'patrol' in '{s}', returning 8");
                    return 8;
                }
                if (s.Contains("crate") || s.Contains("locked"))
                {
                    _logger?.LogDebug($"MarkerTypeOf: Found 'crate'/'locked' in '{s}', returning 6");
                    return 6;
                }
                
                // Try parsing as int only if it's not an enum
                if (enumType == null && !valueType.IsEnum && int.TryParse(s, out var parsedInt))
                {
                    _logger?.LogDebug($"MarkerTypeOf: Parsed '{s}' as int: {parsedInt}");
                    return parsedInt;
                }
            }
            
            // If it's an enum and string didn't match keywords, convert to int
            if (enumType != null || valueType.IsEnum)
            {
                try
                {
                    var enumInt = Convert.ToInt32(v);
                    _logger?.LogDebug($"MarkerTypeOf: Enum converted to int: {enumInt}");
                    return enumInt;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug($"MarkerTypeOf: Failed to convert enum to int: {ex.Message}");
                    return -1;
                }
            }
            
            // For non-enums, check if it's directly an int (only after string checks failed)
            if (v is int i)
            {
                _logger?.LogDebug($"MarkerTypeOf: Value is int: {i}");
                return i;
            }
            
            _logger?.LogDebug($"MarkerTypeOf: No match found, returning -1");
            return -1;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"MarkerTypeOf: Exception: {ex.Message}\n{ex.StackTrace}");
            return -1;
        }
    }

    private void ExtractVendingMachines(System.Collections.IEnumerable collection, List<VendingMachine> shops)
    {
        var itemCount = 0;
        var type3Count = 0;
        var withOrdersCount = 0;
        var withCoordsCount = 0;
        var addedCount = 0;

        foreach (var item in collection)
        {
            itemCount++;
            if (item == null) continue;

            // *** HARD FILTER: must really be a vending (type==3) ***
            int typeCode;
            try
            {
                typeCode = MarkerTypeOf(item);
                _logger?.LogDebug($"MarkerTypeOf returned: {typeCode} for item #{itemCount}");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"ExtractVendingMachines: Error in MarkerTypeOf for item #{itemCount}: {ex}");
                typeCode = -1;
            }
            _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount}, type: {typeCode}, item type: {item.GetType().Name}");
            _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} properties: {string.Join(", ", item.GetType().GetProperties().Select(p => $"{p.Name}={p.GetValue(item)?.ToString() ?? "null"}"))}");
            
            if (typeCode != 3)
            {
                _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} skipped - type is {typeCode}, not 3");
                continue; // Must be type 3, no fallback to name check
            }
            
            type3Count++;
            _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} passed type check (type=3)");

            // Check for both sell and buy orders
            var sellOrdersObj = ReadProp<object>(item, "SellOrders", "Orders");
            var buyOrdersObj = ReadProp<object>(item, "BuyOrders", "PurchaseOrders", "Buy");
            
            // If not found directly, check nested Vending/Sales/Shop
            if (sellOrdersObj == null || buyOrdersObj == null)
            {
                _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - Orders not found directly, checking nested Vending/Sales/Shop");
                var vend = ReadProp<object>(item, "Vending", "Sales", "Shop");
                if (vend != null)
                {
                    _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - Found Vending/Sales/Shop, type: {vend.GetType().Name}");
                    if (sellOrdersObj == null)
                        sellOrdersObj = ReadProp<object>(vend, "SellOrders", "Orders");
                    if (buyOrdersObj == null)
                        buyOrdersObj = ReadProp<object>(vend, "BuyOrders", "PurchaseOrders", "Buy");
                }
            }
            
            // Need at least one type of orders
            if (sellOrdersObj == null && buyOrdersObj == null)
            {
                _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} skipped - no SellOrders or BuyOrders found");
                continue; // No orders found, skip this item
            }
            
            if (sellOrdersObj != null) withOrdersCount++;
            if (buyOrdersObj != null) withOrdersCount++;
            _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} has orders - SellOrders: {sellOrdersObj != null}, BuyOrders: {buyOrdersObj != null}");

            // Get coordinates - try multiple property names
            var x = ReadProp<float>(item, "X", "PositionX", "PosX", "Latitude");
            var y = ReadProp<float>(item, "Y", "PositionY", "PosY", "Longitude");
            
            // If X/Y not found, try nested properties
            if (x == 0 && y == 0)
            {
                var pos = ReadProp<object>(item, "Position", "Pos", "Location");
                if (pos != null)
                {
                    x = ReadProp<float>(pos, "X", "PositionX", "Latitude");
                    y = ReadProp<float>(pos, "Y", "PositionY", "Longitude");
                }
            }

            if (x == 0 && y == 0)
            {
                _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} skipped - no valid coordinates (x={x}, y={y})");
                continue; // No valid coordinates
            }
            
            withCoordsCount++;
            _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} has coordinates: x={x}, y={y}");

            // Get ID and name
            var id = ReadProp<uint>(item, "Id", "ID", "EntityId", "VendingMachineId", "Identifier", "Uid", "UID");
            var name = ReadProp<string>(item, "Name", "Label", "Alias", "Token", "Note") ?? "";
            var isActive = ReadProp<bool>(item, "IsActive", "Active", "On", "Enabled");
            var outOfStock = ReadProp<bool>(item, "OutOfStock", "OutOfStockFlag", "Empty", "NoStock");
            
            _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - ID: {id}, Name: {name}, IsActive: {isActive}, OutOfStock: {outOfStock}");

            var vm = new VendingMachine
            {
                Id = id.ToString(),
                Name = name,
                X = x,
                Y = y,
                IsActive = isActive,
                Items = new List<VendingItem>(),
                BuyItems = new List<VendingItem>()
            };

            // Extract sell orders/items
            if (sellOrdersObj is System.Collections.IEnumerable sellOrdersEnum)
            {
                var orderCount = 0;
                foreach (var orderObj in sellOrdersEnum)
                {
                    orderCount++;
                    if (orderObj == null) continue;

                    _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - Processing order #{orderCount}, type: {orderObj.GetType().Name}");
                    _logger?.LogDebug($"ExtractVendingMachines: Order #{orderCount} properties: {string.Join(", ", orderObj.GetType().GetProperties().Select(p => $"{p.Name}={p.GetValue(orderObj)?.ToString() ?? "null"}"))}");

                    // Try different property names for order data
                    // Note: Orders use ItemId (not ItemName) and CostPerItem (not Cost)
                    var itemId = ReadProp<int>(orderObj, "ItemId", "ItemID", "ID");
                    var shortName = ReadProp<string>(orderObj, "ItemName", "Name", "Shortname", "Item", "ItemType", "ItemShortName") ?? "";
                    var itemName = shortName; // Will be resolved later using ItemNameService
                    // If shortName is empty, we'll use ItemId to resolve it later
                    
                    var quantity = ReadProp<int>(orderObj, "Quantity", "Amount", "Count", "ItemCount", "Stock");
                    // Check AmountInStock - this is the key property for determining if shop has stock
                    var amountInStock = ReadProp<int>(orderObj, "AmountInStock", "Stock", "InStock", "Available");
                    var cost = ReadProp<int>(orderObj, "CostPerItem", "Cost", "Price", "CurrencyAmount", "ScrapCost");
                    
                    // Try to get currency item ID first (this is the actual item being used as currency)
                    var currencyItemId = ReadProp<int>(orderObj, "CurrencyItemId", "CurrencyId", "CurrencyItemID", "CurrencyItem");
                    var currencyShortName = ReadProp<string>(orderObj, "CurrencyShortName", "Currency", "CurrencyType", "CurrencyName") ?? "scrap";
                    var currency = currencyShortName;

                    // If we have a currency item ID, resolve the item name
                    string currencyItemName = "Scrap";
                    if (currencyItemId != 0)
                    {
                        var currencyShort = _itemNameService?.GetShortName(currencyItemId);
                        currencyItemName = _itemNameService?.GetItemName(currencyItemId, currencyShort) ?? 
                                          (!string.IsNullOrEmpty(currencyShort) ? currencyShort : $"Item_{currencyItemId}");
                        currency = currencyShort ?? currencyShortName;
                    }
                    else if (!string.IsNullOrEmpty(currencyShortName) && currencyShortName != "scrap")
                    {
                        // Try to resolve currency name from shortName
                        currencyItemName = _itemNameService?.GetItemName(0, currencyShortName) ?? currencyShortName;
                    }

                    // If currency is empty or default, try to determine from cost property name
                    if (string.IsNullOrEmpty(currency) || currency == "scrap")
                    {
                        // Check if there are specific currency properties
                        var scrapCost = ReadProp<int>(orderObj, "ScrapCost", "Scrap");
                        if (scrapCost > 0)
                        {
                            cost = scrapCost;
                            currency = "scrap";
                            currencyItemId = 0; // Scrap doesn't have an item ID
                            currencyItemName = "Scrap";
                        }
                    }

                    // Resolve currency icon path
                    string? currencyIconUrl = null;
                    if (currencyItemId != 0)
                    {
                        var currencyShort = _itemNameService?.GetShortName(currencyItemId);
                        currencyIconUrl = _itemNameService?.GetItemIconPath(currencyItemId, currencyShort);
                    }
                    else if (!string.IsNullOrEmpty(currencyShortName) && currencyShortName != "scrap")
                    {
                        currencyIconUrl = _itemNameService?.GetItemIconPath(0, currencyShortName);
                    }

                    _logger?.LogDebug($"ExtractVendingMachines: Order #{orderCount} - ItemId: {itemId}, ItemName: {itemName}, Quantity: {quantity}, AmountInStock: {amountInStock}, Cost: {cost}, Currency: {currency}");

                    // Add item if it has stock (AmountInStock > 0) OR if we have valid data
                    // The key is AmountInStock - if it's > 0, the shop has stock
                    if (amountInStock > 0 || (!string.IsNullOrEmpty(shortName) && quantity > 0 && cost > 0))
                    {
                        // If shortName is empty, try to get it from ItemId
                        if (string.IsNullOrEmpty(shortName) && itemId != 0)
                        {
                            var resolvedShortName = _itemNameService?.GetShortName(itemId);
                            if (!string.IsNullOrEmpty(resolvedShortName))
                            {
                                shortName = resolvedShortName;
                                System.Diagnostics.Debug.WriteLine($"[ExtractVendingMachines] Resolved ShortName from ItemId: {itemId} -> '{shortName}'");
                            }
                        }
                        
                        // Resolve item name and icon using ItemNameService
                        var resolvedName = _itemNameService?.GetItemName(itemId, shortName) ?? 
                                          (!string.IsNullOrEmpty(shortName) ? shortName : $"Item_{itemId}");
                        
                        System.Diagnostics.Debug.WriteLine($"[ExtractVendingMachines] Resolving icon for ItemId: {itemId}, ShortName: '{shortName ?? "null"}', ItemName: '{resolvedName}'");
                        
                        // Try local icon path first - prioritize local icons, NEVER use URLs
                        var iconPath = _itemNameService?.GetItemIconPath(itemId, shortName);
                        System.Diagnostics.Debug.WriteLine($"[ExtractVendingMachines] GetItemIconPath returned: {iconPath ?? "null"}");
                        
                        // DO NOT fall back to URLs - we only want local icons
                        var finalIconUrl = iconPath;
                        if (string.IsNullOrEmpty(finalIconUrl))
                        {
                            System.Diagnostics.Debug.WriteLine($"[ExtractVendingMachines] ✗✗✗ NO ICON FOUND for ItemId: {itemId}, ShortName: {shortName ?? "null"}");
                            _logger?.LogDebug($"ExtractVendingMachines: No icon found for ItemId: {itemId}, ShortName: {shortName ?? "null"}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ExtractVendingMachines] ✓✓✓ ICON FOUND: {finalIconUrl} for ItemId: {itemId}, ShortName: {shortName ?? "null"}");
                            _logger?.LogDebug($"ExtractVendingMachines: Icon resolved for ItemId: {itemId}, ShortName: {shortName ?? "null"}, IconPath: {finalIconUrl}");
                        }
                        
                        vm.Items.Add(new VendingItem
                        {
                            ItemId = itemId,
                            ShortName = shortName,
                            ItemName = resolvedName,
                            IconUrl = finalIconUrl, // Use local path if available, otherwise URL
                            Quantity = amountInStock > 0 ? amountInStock : quantity, // Use AmountInStock if available
                            Cost = cost,
                            Currency = currency,
                            CurrencyItemId = currencyItemId,
                            CurrencyItemName = currencyItemName,
                            CurrencyIconUrl = currencyIconUrl
                        });
                        System.Diagnostics.Debug.WriteLine($"[ExtractVendingMachines] Added order #{orderCount} - ItemId: {itemId}, Name: {resolvedName}, IconUrl: {finalIconUrl ?? "null"}");
                        _logger?.LogDebug($"ExtractVendingMachines: Added order #{orderCount} to vending machine (ItemId: {itemId}, Name: {resolvedName}, AmountInStock: {amountInStock}, IconUrl: {finalIconUrl ?? "null"})");
                    }
                    else
                    {
                        _logger?.LogDebug($"ExtractVendingMachines: Order #{orderCount} skipped - no stock (AmountInStock: {amountInStock}, ItemName empty: {string.IsNullOrEmpty(itemName)}, Quantity: {quantity}, Cost: {cost})");
                    }
                }
                
                _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - Total sell orders processed: {orderCount}, valid orders: {vm.Items.Count}");
            }
            else
            {
                _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - sellOrdersObj is not IEnumerable");
            }

            // Extract buy orders/items
            if (buyOrdersObj is System.Collections.IEnumerable buyOrdersEnum)
            {
                var buyOrderCount = 0;
                foreach (var orderObj in buyOrdersEnum)
                {
                    buyOrderCount++;
                    if (orderObj == null) continue;

                    _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - Processing buy order #{buyOrderCount}, type: {orderObj.GetType().Name}");

                    // Try different property names for order data
                    var itemId = ReadProp<int>(orderObj, "ItemId", "ItemID", "ID");
                    var shortName = ReadProp<string>(orderObj, "ItemName", "Name", "Shortname", "Item", "ItemType", "ItemShortName") ?? "";
                    var quantity = ReadProp<int>(orderObj, "Quantity", "Amount", "Count", "ItemCount", "Stock");
                    var amountInStock = ReadProp<int>(orderObj, "AmountInStock", "Stock", "InStock", "Available");
                    var cost = ReadProp<int>(orderObj, "CostPerItem", "Cost", "Price", "CurrencyAmount", "ScrapCost");
                    
                    // Try to get currency item ID first (this is the actual item being used as currency)
                    var currencyItemId = ReadProp<int>(orderObj, "CurrencyItemId", "CurrencyId", "CurrencyItemID", "CurrencyItem");
                    var currencyShortName = ReadProp<string>(orderObj, "CurrencyShortName", "Currency", "CurrencyType", "CurrencyName") ?? "scrap";
                    var currency = currencyShortName;

                    // If we have a currency item ID, resolve the item name
                    string currencyItemName = "Scrap";
                    if (currencyItemId != 0)
                    {
                        var currencyShort = _itemNameService?.GetShortName(currencyItemId);
                        currencyItemName = _itemNameService?.GetItemName(currencyItemId, currencyShort) ?? 
                                          (!string.IsNullOrEmpty(currencyShort) ? currencyShort : $"Item_{currencyItemId}");
                        currency = currencyShort ?? currencyShortName;
                    }
                    else if (!string.IsNullOrEmpty(currencyShortName) && currencyShortName != "scrap")
                    {
                        // Try to resolve currency name from shortName
                        currencyItemName = _itemNameService?.GetItemName(0, currencyShortName) ?? currencyShortName;
                    }

                    // If currency is empty or default, try to determine from cost property name
                    if (string.IsNullOrEmpty(currency) || currency == "scrap")
                    {
                        var scrapCost = ReadProp<int>(orderObj, "ScrapCost", "Scrap");
                        if (scrapCost > 0)
                        {
                            cost = scrapCost;
                            currency = "scrap";
                            currencyItemId = 0; // Scrap doesn't have an item ID
                            currencyItemName = "Scrap";
                        }
                    }

                    // Resolve currency icon path
                    string? currencyIconUrl = null;
                    if (currencyItemId != 0)
                    {
                        var currencyShort = _itemNameService?.GetShortName(currencyItemId);
                        currencyIconUrl = _itemNameService?.GetItemIconPath(currencyItemId, currencyShort);
                    }
                    else if (!string.IsNullOrEmpty(currencyShortName) && currencyShortName != "scrap")
                    {
                        currencyIconUrl = _itemNameService?.GetItemIconPath(0, currencyShortName);
                    }

                    // If shortName is empty, try to get it from ItemId
                    if (string.IsNullOrEmpty(shortName) && itemId != 0)
                    {
                        var resolvedShortName = _itemNameService?.GetShortName(itemId);
                        if (!string.IsNullOrEmpty(resolvedShortName))
                        {
                            shortName = resolvedShortName;
                            System.Diagnostics.Debug.WriteLine($"[ExtractVendingMachines] Resolved ShortName from ItemId (buy order): {itemId} -> '{shortName}'");
                        }
                    }
                    
                    // Resolve item name and icon using ItemNameService
                    var resolvedName = _itemNameService?.GetItemName(itemId, shortName) ?? 
                                      (!string.IsNullOrEmpty(shortName) ? shortName : $"Item_{itemId}");
                    
                    // Try local icon path only - DO NOT use URLs
                    var iconPath = _itemNameService?.GetItemIconPath(itemId, shortName);
                    var finalIconUrl = iconPath;
                    if (string.IsNullOrEmpty(finalIconUrl))
                    {
                        _logger?.LogDebug($"ExtractVendingMachines: No icon found for buy order ItemId: {itemId}, ShortName: {shortName ?? "null"}");
                    }
                    else
                    {
                        _logger?.LogDebug($"ExtractVendingMachines: Icon resolved for buy order ItemId: {itemId}, ShortName: {shortName ?? "null"}, IconPath: {finalIconUrl}");
                    }

                    // Add buy order if it has valid data
                    if (amountInStock > 0 || (!string.IsNullOrEmpty(shortName) && quantity > 0 && cost > 0))
                    {
                        vm.BuyItems.Add(new VendingItem
                        {
                            ItemId = itemId,
                            ShortName = shortName,
                            ItemName = resolvedName,
                            IconUrl = finalIconUrl, // Use local path if available, otherwise URL
                            Quantity = amountInStock > 0 ? amountInStock : quantity,
                            Cost = cost,
                            Currency = currency,
                            CurrencyItemId = currencyItemId,
                            CurrencyItemName = currencyItemName,
                            CurrencyIconUrl = currencyIconUrl
                        });
                        _logger?.LogDebug($"ExtractVendingMachines: Added buy order #{buyOrderCount} to vending machine (ItemId: {itemId}, Name: {resolvedName}, IconUrl: {finalIconUrl ?? "null"})");
                    }
                }
                
                _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - Total buy orders processed: {buyOrderCount}, valid orders: {vm.BuyItems.Count}");
            }

            // Update IsActive based on whether shop has stock (sell or buy items)
            // Shop is active if: has items with stock OR OutOfStock is false OR IsActive property is true
            bool hasStock = vm.Items.Count > 0 || vm.BuyItems.Count > 0; // Items are only added if AmountInStock > 0
            vm.IsActive = hasStock || !outOfStock || vm.IsActive;
            
            _logger?.LogDebug($"ExtractVendingMachines: Item #{itemCount} - Final status: Items.Count={vm.Items.Count}, OutOfStock={outOfStock}, IsActive={vm.IsActive}");

            shops.Add(vm);
            addedCount++;
            _logger?.LogInfo($"ExtractVendingMachines: ✓ Added vending machine #{addedCount} - ID: {id}, Name: {name}, Items: {vm.Items.Count}, Coords: ({x}, {y})");
        }
        
        _logger?.LogInfo($"ExtractVendingMachines: Summary - Total items: {itemCount}, Type 3: {type3Count}, With orders: {withOrdersCount}, With coords: {withCoordsCount}, Added: {addedCount}");
    }

    public Task<List<SmartDevice>> GetSmartDevicesAsync()
    {
        // Smart devices would be extracted similarly
        // For now, return empty list
        return Task.FromResult(new List<SmartDevice>());
    }

    public async Task<TeamInfo?> GetTeamInfoAsync()
    {
        if (_api == null) return null;

        try
        {
            var t = _api.GetType();
            var m = t.GetMethod("GetTeamInfoAsync", new[] { typeof(CancellationToken) })
                 ?? t.GetMethod("GetTeamInfoAsync", Type.EmptyTypes)
                 ?? t.GetMethod("GetTeamInfo", Type.EmptyTypes);

            if (m == null)
            {
                _logger?.LogWarning("GetTeamInfoAsync method not found in RustPlusApi");
                return null;
            }

            object? call = m.GetParameters().Length == 1
                ? m.Invoke(_api, new object[] { CancellationToken.None })
                : m.Invoke(_api, Array.Empty<object>());

            if (call is Task task)
            {
                await task.ConfigureAwait(false);
                var result = task.GetType().GetProperty("Result")?.GetValue(task);
                var data = ReadProp<object>(result, "Data") ?? result;

                if (data != null)
                {
                    var teamInfo = new TeamInfo();
                    
                    // Get leader
                    var leaderId = ReadProp<ulong>(data, "LeaderSteamId", "LeaderId", "TeamLeaderSteamId");
                    teamInfo.LeaderSteamId = leaderId;

                    // Get members
                    var members = ReadProp<object>(data, "Members", "TeamMembers", "Players");
                    if (members is System.Collections.IEnumerable membersEnum)
                    {
                        foreach (var member in membersEnum)
                        {
                            if (member == null) continue;

                            var steamId = ReadProp<ulong>(member, "SteamId", "PlayerId", "Id");
                            var name = ReadProp<string>(member, "Name", "DisplayName", "Username");
                            var online = ReadProp<bool>(member, "Online", "IsOnline");
                            var dead = ReadProp<bool>(member, "Dead", "IsDead");
                            
                            // Try to get position
                            var pos = ReadProp<object>(member, "Position", "Pos");
                            var x = ReadProp<double?>(member, "X", "PositionX", "PosX");
                            var y = ReadProp<double?>(member, "Y", "PositionY", "PosY");
                            
                            if (x == null && pos != null)
                            {
                                x = ReadProp<double?>(pos, "X");
                            }
                            if (y == null && pos != null)
                            {
                                y = ReadProp<double?>(pos, "Y");
                            }

                            if (steamId > 0)
                            {
                                teamInfo.Members.Add(new TeamMember
                                {
                                    SteamId = steamId,
                                    Name = name,
                                    Online = online,
                                    Dead = dead,
                                    X = x,
                                    Y = y
                                });
                            }
                        }
                    }

                    TeamInfoUpdated?.Invoke(this, teamInfo);
                    return teamInfo;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error getting team info: {ex.Message}", ex);
            return null;
        }
    }

    public async Task<bool> ToggleSmartDeviceAsync(string deviceId, bool state)
    {
        if (_api == null) return false;

        try
        {
            if (!uint.TryParse(deviceId, out var entityId))
            {
                _logger?.LogError($"Invalid device ID: {deviceId}");
                return false;
            }

            var t = _api.GetType();
            var m = t.GetMethod("SetEntityValueAsync", new[] { typeof(uint), typeof(bool), typeof(CancellationToken) })
                 ?? t.GetMethod("SetEntityValueAsync", new[] { typeof(uint), typeof(bool) })
                 ?? t.GetMethod("ToggleSmartSwitchAsync", new[] { typeof(uint), typeof(bool), typeof(CancellationToken) })
                 ?? t.GetMethod("ToggleSmartSwitchAsync", new[] { typeof(uint), typeof(bool) });

            if (m == null)
            {
                _logger?.LogWarning("ToggleSmartDevice method not found in RustPlusApi");
                return false;
            }

            object? call;
            if (m.GetParameters().Length == 3)
            {
                call = m.Invoke(_api, new object[] { entityId, state, CancellationToken.None });
            }
            else
            {
                call = m.Invoke(_api, new object[] { entityId, state });
            }

            if (call is Task task)
            {
                await task.ConfigureAwait(false);
                var result = task.GetType().GetProperty("Result")?.GetValue(task);
                if (result != null)
                {
                    var success = ReadProp<bool>(result, "IsSuccess", "Success");
                    return success;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error toggling smart device: {ex.Message}", ex);
            ErrorOccurred?.Invoke(this, $"Failed to toggle device: {ex.Message}");
            return false;
        }
    }

    // Helper method to read properties using reflection (like working version)
    private static T ReadProp<T>(object? src, params string[] names)
    {
        if (src == null) return default!;
        
        var t = src.GetType();
        foreach (var n in names)
        {
            var p = t.GetProperty(n, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
            if (p != null)
            {
                var v = p.GetValue(src);
                if (v is T tv) return tv;

                // Handle type conversions
                if (typeof(T) == typeof(int) && v is uint u) return (T)(object)(int)u;
                if (typeof(T) == typeof(uint) && v is int i && i >= 0) return (T)(object)(uint)i;
                if (typeof(T) == typeof(float) && v is double d) return (T)(object)(float)d;
                if (typeof(T) == typeof(float) && v is int i2) return (T)(object)(float)i2;
            }
        }
        return default!;
    }

    private async Task ExtractWorldEventsFromApiAsync()
    {
        if (_api == null) return;

        try
        {
            var t = _api.GetType();
            var m = t.GetMethod("GetMapMarkersAsync", new[] { typeof(CancellationToken) })
                 ?? t.GetMethod("GetMapMarkersAsync", Type.EmptyTypes)
                 ?? t.GetMethod("GetMapMarkers", Type.EmptyTypes);
            if (m == null) return;

            object? call = m.GetParameters().Length == 1 
                ? m.Invoke(_api, new object[] { CancellationToken.None }) 
                : m.Invoke(_api, Array.Empty<object>());

            object? result = call;
            if (call is Task task)
            {
                await task.ConfigureAwait(false);
                result = task.GetType().GetProperty("Result")?.GetValue(task);
            }

            var data = ReadProp<object>(result, "Data") ?? result;
            if (data == null) return;

            var events = new List<WorldEvent>();
            var eventTypes = new[] { 4, 5, 6, 8 }; // Chinook (4), Cargo (5), Crate (6), Heli (8)
            
            // Get markers collection
            var markersProp = ReadProp<object>(data, "Markers", "Marker");
            if (markersProp is System.Collections.IEnumerable markersEnum)
            {
                foreach (var marker in markersEnum)
                {
                    if (marker == null) continue;
                    
                    // Use MarkerTypeOf to get the type code
                    var typeCode = MarkerTypeOf(marker);
                    var x = ReadProp<float>(marker, "X", "PositionX");
                    var y = ReadProp<float>(marker, "Y", "PositionY");
                    var name = ReadProp<string>(marker, "Name", "Label", "Text") ?? "";
                    
                    _logger?.LogDebug($"WorldEvent Check: TypeCode={typeCode}, X={x}, Y={y}, Name='{name}'");
                    
                    // Only process world event types (exclude vending machines (3) and players (1))
                    if (eventTypes.Contains(typeCode))
                    {
                        var eventTypeName = typeCode switch
                        {
                            4 => "Chinook Crate",
                            5 => "Cargo Ship",
                            6 => "Locked Crate",
                            8 => "Patrol Helicopter",
                            _ => "Unknown Event"
                        };
                        
                        _logger?.LogInfo($"✓ World Event Detected: {eventTypeName} at ({x:F0}, {y:F0}) - TypeCode: {typeCode}");
                        
                        events.Add(new WorldEvent
                        {
                            EventType = eventTypeName,
                            X = x,
                            Y = y,
                            SpawnTime = DateTime.Now
                        });
                    }
                    else if (typeCode != 1 && typeCode != 3 && typeCode != -1)
                    {
                        // Log other types for debugging
                        _logger?.LogDebug($"Skipped marker: TypeCode={typeCode} (not a world event type)");
                    }
                }
            }
            
            if (events.Count > 0)
            {
                WorldEventsUpdated?.Invoke(this, events);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error extracting world events: {ex.Message}");
        }
    }

    public void Dispose()
    {
        DisconnectAsync().Wait(1000);
    }
}
