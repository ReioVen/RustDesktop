using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

public interface IActiveSessionService
{
    Task<ServerInfo?> GetActiveServerSessionAsync();
    bool IsRustRunning();
    Task<string?> GetCurrentServerIpAsync();
    Task<int?> GetCurrentServerRustPlusPortAsync();
}
















