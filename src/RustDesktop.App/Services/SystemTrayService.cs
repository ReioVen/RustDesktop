using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace RustDesktop.App.Services;

/// <summary>
/// Service for managing system tray icon and context menu
/// </summary>
public class SystemTrayService : ISystemTrayService
{
    private NotifyIcon? _notifyIcon;
    private bool _disposed = false;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_notifyIcon != null)
            return;

        _notifyIcon = new NotifyIcon
        {
            Icon = GetApplicationIcon(),
            Text = "Rust Desktop",
            Visible = true
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        
        var showMenuItem = new ToolStripMenuItem("Show");
        showMenuItem.Click += (s, e) => ShowRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(showMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Show()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = true;
        }
    }

    public void Hide()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
        }
    }

    public void UpdateTooltip(string text)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Text = text;
        }
    }

    private Icon GetApplicationIcon()
    {
        // Use BaseDirectory for single-file published apps compatibility
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var iconsDir = Path.Combine(baseDir, "Icons");
        
        // Try app.ico first
        try
        {
            var iconUri = new Uri("pack://application:,,,/RustDesktop.App;component/Icons/app.ico", UriKind.Absolute);
            var streamResourceInfo = Application.GetResourceStream(iconUri);
            if (streamResourceInfo != null)
            {
                using var stream = streamResourceInfo.Stream;
                return new Icon(stream);
            }
        }
        catch
        {
            // Continue to file-based loading
        }
        
        // Try app.ico from file
        try
        {
            var appIconPath = Path.Combine(iconsDir, "app.ico");
            if (File.Exists(appIconPath))
            {
                return new Icon(appIconPath);
            }
        }
        catch
        {
            // Continue to C4 icon
        }
        
        // Try C4 icon (explosive.timed) - this is the cool icon for the app
        try
        {
            // Try different naming patterns for C4 icon
            var c4Paths = new[]
            {
                Path.Combine(iconsDir, "explosive_timed.png"),
                Path.Combine(iconsDir, "explosive.timed.png"),
                Path.Combine(iconsDir, "1248356124.png"), // Item ID
                Path.Combine(iconsDir, "item_1248356124.png")
            };
            
            foreach (var c4Path in c4Paths)
            {
                if (File.Exists(c4Path))
                {
                    // Convert PNG to Icon
                    return PngToIcon(c4Path);
                }
            }
        }
        catch
        {
            // Continue to fallback
        }

        // Fallback: Use system default application icon
        return SystemIcons.Application;
    }
    
    private Icon PngToIcon(string pngPath)
    {
        try
        {
            using var bitmap = new Bitmap(pngPath);
            using var stream = new MemoryStream();
            
            // Create icon from bitmap
            // For system tray, we need 16x16 and 32x32 sizes
            var iconSizes = new[] { 16, 32 };
            var iconBitmaps = new List<Bitmap>();
            
            foreach (var size in iconSizes)
            {
                var resized = new Bitmap(bitmap, size, size);
                iconBitmaps.Add(resized);
            }
            
            // Create multi-resolution icon
            // Note: System.Drawing doesn't have a direct way to create multi-res ICO
            // So we'll use the 32x32 size which works well for system tray
            using var iconBitmap = new Bitmap(bitmap, 32, 32);
            var hIcon = iconBitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            // If conversion fails, return default
            return SystemIcons.Application;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon?.Dispose();
            _disposed = true;
        }
    }
}





