namespace RustDesktop.Core.Models;

public class ServerInfo
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } // Rust+ port
    public int GamePort { get; set; } // Game port (for reference)
    public string ServerId { get; set; } = string.Empty;
    public string PlayerToken { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}

