using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

/// <summary>
/// Service for pairing with Rust+ server to get Server ID and Player Token
/// </summary>
public interface IPairingService
{
    /// <summary>
    /// Check if we have paired credentials (Server ID + Player Token)
    /// </summary>
    Task<bool> IsPairedAsync();
    
    /// <summary>
    /// Authenticate with Steam - opens browser and receives callback
    /// </summary>
    Task<bool> AuthenticateWithSteamAsync();
    
    /// <summary>
    /// Check if Steam is authenticated
    /// </summary>
    Task<bool> IsSteamAuthenticatedAsync();
    
    /// <summary>
    /// Get the authenticated Steam ID
    /// </summary>
    string? GetSteamId();
    
    /// <summary>
    /// Start pairing with a Rust server - listens for pairing messages and extracts credentials
    /// </summary>
    Task<ServerInfo?> PairWithServerAsync(string ipAddress, int gamePort, string steamId);
    
    /// <summary>
    /// Store pairing credentials for a server
    /// </summary>
    Task<bool> SavePairingAsync(ServerInfo serverInfo);
    
    /// <summary>
    /// Clear stored pairing credentials
    /// </summary>
    void ClearPairing();
    
    /// <summary>
    /// Event fired when pairing is successful
    /// </summary>
    event EventHandler<ServerInfo>? PairingSuccessful;
    
    /// <summary>
    /// Event fired when pairing fails
    /// </summary>
    event EventHandler<string>? PairingFailed;
    
    /// <summary>
    /// Event fired when Steam authentication is successful
    /// </summary>
    event EventHandler<string>? SteamAuthenticated;
    
    /// <summary>
    /// Event fired when Steam authentication fails
    /// </summary>
    event EventHandler<string>? SteamAuthenticationFailed;
}
