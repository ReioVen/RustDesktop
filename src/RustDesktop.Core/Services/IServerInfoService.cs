using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

public interface IServerInfoService
{
    Task<ServerInfo?> GetServerInfoAsync(string ipAddress, int port);
    Task<string?> GetServerIdAsync(string ipAddress, int port);
}










