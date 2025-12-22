using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

public interface IRustDataService
{
    Task<ServerInfo?> GetPairedServerAsync();
    Task<string?> GetSteamIdAsync();
    List<ServerInfo> GetAllPairedServers();
    Task<ServerInfo?> GetActiveServerFromGameAsync();
}

