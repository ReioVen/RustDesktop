using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

public interface IRustPlusService
{
    Task<bool> ConnectAsync(ServerInfo serverInfo);
    Task DisconnectAsync();
    Task<MapInfo?> GetMapAsync();
    Task<List<VendingMachine>> GetVendingMachinesAsync();
    Task<List<SmartDevice>> GetSmartDevicesAsync();
    Task<bool> ToggleSmartDeviceAsync(string deviceId, bool state);
    Task<TeamInfo?> GetTeamInfoAsync();
    bool IsConnected { get; }
    bool IsAuthenticated { get; }
    event EventHandler<MapInfo>? MapUpdated;
    event EventHandler<List<VendingMachine>>? VendingMachinesUpdated;
    event EventHandler<TeamInfo>? TeamInfoUpdated;
    event EventHandler<string>? ErrorOccurred;
    event EventHandler<List<WorldEvent>>? WorldEventsUpdated;
    event EventHandler<ServerInfo>? ServerInfoUpdated;
}

