using System.Net.Http;
using System.Text.Json;
using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

/// <summary>
/// Service to query Rust server for Rust+ information
/// </summary>
public class ServerInfoService : IServerInfoService
{
    private readonly HttpClient _httpClient;
    private readonly ILoggingService? _logger;

    public ServerInfoService(ILoggingService? logger = null)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _logger = logger;
    }

    public async Task<ServerInfo?> GetServerInfoAsync(string ipAddress, int port)
    {
        try
        {
            _logger?.LogDebug($"GetServerInfoAsync: Querying server {ipAddress}:{port}");

            // Try HTTP endpoint first (some servers expose Rust+ info via HTTP)
            var serverId = await GetServerIdAsync(ipAddress, port);
            
            if (!string.IsNullOrEmpty(serverId))
            {
                _logger?.LogInfo($"GetServerInfoAsync: Found Server ID: {serverId}");
                return new ServerInfo
                {
                    IpAddress = ipAddress,
                    Port = port,
                    ServerId = serverId,
                    // Player token not needed for initial connection
                    PlayerToken = string.Empty,
                    SteamId = string.Empty
                };
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"GetServerInfoAsync: Error querying server", ex);
        }

        return null;
    }

    public Task<string?> GetServerIdAsync(string ipAddress, int port)
    {
        try
        {
            // Rust+ doesn't typically expose HTTP endpoints, but we can try
            // The Server ID is usually obtained during WebSocket handshake
            // For now, we'll skip HTTP query and get it from WebSocket connection
            _logger?.LogDebug($"GetServerIdAsync: Rust+ typically provides Server ID via WebSocket, skipping HTTP query");
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"GetServerIdAsync: Error", ex);
        }

        return Task.FromResult<string?>(null);
    }
}

