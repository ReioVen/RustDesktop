using System;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using Notifications.Wpf.Core;

namespace RustDesktop.App.Services;

/// <summary>
/// Service for displaying Windows toast notifications
/// Shows modern toast notifications in the bottom-right corner of the screen
/// </summary>
public class NotificationService : INotificationService, IDisposable
{
    private readonly NotificationManager _notificationManager;
    private bool _disposed = false;

    public NotificationService()
    {
        // Initialize notification manager - defaults to bottom-right corner
        _notificationManager = new NotificationManager();
    }

    public void ShowNotification(string title, string message, string? iconPath = null)
    {
        ShowNotification(title, message, iconPath, null);
    }

    public void ShowNotification(string title, string message, string? iconPath, Action? onClick)
    {
        try
        {
            // Determine notification type based on title
            var notificationType = NotificationType.Information;
            if (title.Contains("🚨") || title.Contains("Raid"))
            {
                notificationType = NotificationType.Warning;
            }
            else if (title.Contains("🌍") || title.Contains("World Event"))
            {
                notificationType = NotificationType.Success;
            }

            var notificationContent = new NotificationContent
            {
                Title = title,
                Message = message,
                Type = notificationType
            };

            // Show notification on UI thread
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await _notificationManager.ShowAsync(notificationContent);
                    
                    // Play notification sound
                    PlayNotificationSound();
                    
                    // Handle click action if provided
                    if (onClick != null)
                    {
                        // Note: Notifications.Wpf.Core doesn't directly support click callbacks
                        // You would need to extend the library or handle it differently
                        // For now, the notification will just display
                    }
                }
                catch
                {
                    // Fallback to system sound if notification fails
                    PlayNotificationSound();
                }
            });
        }
        catch (Exception)
        {
            // Fallback to system sounds if toast fails
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch
            {
                // Silent failure
            }
        }
    }

    private void PlayNotificationSound()
    {
        try
        {
            // Play Windows default notification sound
            SystemSounds.Asterisk.Play();
        }
        catch
        {
            // If system sound fails, try alternative
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch
            {
                // If all sounds fail, continue silently
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // NotificationManager doesn't require explicit disposal
            _disposed = true;
        }
    }
}





