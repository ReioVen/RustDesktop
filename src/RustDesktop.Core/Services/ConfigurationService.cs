using System.Text.Json;
using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private readonly string _serversPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RustDesktop"
        );
        
        Directory.CreateDirectory(appDataPath);
        
        _configPath = Path.Combine(appDataPath, "config.json");
        _serversPath = Path.Combine(appDataPath, "servers.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public List<ServerInfo> GetSavedServers()
    {
        if (!File.Exists(_serversPath))
            return new List<ServerInfo>();

        try
        {
            var json = File.ReadAllText(_serversPath);
            var servers = JsonSerializer.Deserialize<List<ServerInfo>>(json, _jsonOptions);
            return servers ?? new List<ServerInfo>();
        }
        catch
        {
            return new List<ServerInfo>();
        }
    }

    public void SaveServer(ServerInfo serverInfo)
    {
        var servers = GetSavedServers();
        var existing = servers.FirstOrDefault(s => s.ServerId == serverInfo.ServerId);
        
        if (existing != null)
        {
            servers.Remove(existing);
        }
        
        servers.Add(serverInfo);
        
        var json = JsonSerializer.Serialize(servers, _jsonOptions);
        File.WriteAllText(_serversPath, json);
    }

    public void DeleteServer(string serverId)
    {
        var servers = GetSavedServers();
        servers.RemoveAll(s => s.ServerId == serverId);
        
        var json = JsonSerializer.Serialize(servers, _jsonOptions);
        File.WriteAllText(_serversPath, json);
    }

    public ServerInfo? GetLastConnectedServer()
    {
        if (!File.Exists(_configPath))
            return null;

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<Config>(json, _jsonOptions);
            
            if (config?.LastServerId == null)
                return null;

            var servers = GetSavedServers();
            return servers.FirstOrDefault(s => s.ServerId == config.LastServerId);
        }
        catch
        {
            return null;
        }
    }

    public void SetLastConnectedServer(ServerInfo serverInfo)
    {
        var config = new Config
        {
            LastServerId = serverInfo.ServerId
        };

        var json = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(_configPath, json);
    }

    private class Config
    {
        public string? LastServerId { get; set; }
    }
}











