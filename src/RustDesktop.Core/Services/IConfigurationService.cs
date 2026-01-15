using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

public interface IConfigurationService
{
    List<ServerInfo> GetSavedServers();
    void SaveServer(ServerInfo serverInfo);
    void DeleteServer(string serverId);
    ServerInfo? GetLastConnectedServer();
    void SetLastConnectedServer(ServerInfo serverInfo);
}
















