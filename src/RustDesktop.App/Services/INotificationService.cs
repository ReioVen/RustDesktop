using System;

namespace RustDesktop.App.Services;

/// <summary>
/// Service for displaying Windows toast notifications
/// </summary>
public interface INotificationService : IDisposable
{
    /// <summary>
    /// Show a toast notification
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="iconPath">Optional path to icon image</param>
    void ShowNotification(string title, string message, string? iconPath = null);

    /// <summary>
    /// Show a toast notification with custom actions
    /// </summary>
    void ShowNotification(string title, string message, string? iconPath, Action? onClick);
}





