using System;

namespace RustDesktop.App.Services;

/// <summary>
/// Service for managing system tray icon and context menu
/// </summary>
public interface ISystemTrayService : IDisposable
{
    /// <summary>
    /// Initialize the system tray icon
    /// </summary>
    void Initialize();

    /// <summary>
    /// Show the system tray icon
    /// </summary>
    void Show();

    /// <summary>
    /// Hide the system tray icon
    /// </summary>
    void Hide();

    /// <summary>
    /// Update the tooltip text
    /// </summary>
    void UpdateTooltip(string text);

    /// <summary>
    /// Event fired when user clicks "Show" from tray menu
    /// </summary>
    event EventHandler? ShowRequested;

    /// <summary>
    /// Event fired when user clicks "Exit" from tray menu
    /// </summary>
    event EventHandler? ExitRequested;
}





