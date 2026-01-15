using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RustDesktop.App.ViewModels;
using RustDesktop.App.Services;
using RustDesktop.Core.Services;
using RustDesktop.Core.Models;

namespace RustDesktop.App.Views;

public partial class MainWindow : Window
{
    private readonly IconValidationService? _iconValidationService;
    private readonly ILoggingService? _logger;
    private readonly ISystemTrayService? _systemTrayService;
    private bool _isClosing;

    public MainWindow(MainViewModel viewModel, IconValidationService? iconValidationService = null, ILoggingService? logger = null, ISystemTrayService? systemTrayService = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _iconValidationService = iconValidationService;
        _logger = logger;
        _systemTrayService = systemTrayService;
        
        // Set window icon programmatically (required for published builds)
        SetWindowIcon();
        
        // Handle window state changes
        StateChanged += MainWindow_StateChanged;
    }
    
    private void SetWindowIcon()
    {
        // For published single-file apps, we need to load from file system
        // The icon is copied to output directory, so we can load it directly
        
        try
        {
            // Get the base directory (works for both development and published builds)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var iconPath = Path.Combine(baseDir, "Icons", "app.ico");
            
            if (File.Exists(iconPath))
            {
                using var icon = new Icon(iconPath);
                Icon = ConvertIconToImageSource(icon);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load app.ico: {ex.Message}", ex);
        }
        
        // Fallback: Try C4 PNG and convert to icon
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var c4Paths = new[]
            {
                Path.Combine(baseDir, "Icons", "explosive.timed.png"),
                Path.Combine(baseDir, "Icons", "explosive_timed.png")
            };
            
            foreach (var c4Path in c4Paths)
            {
                if (File.Exists(c4Path))
                {
                    // Convert PNG directly to ImageSource
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(c4Path, UriKind.Absolute);
                    bitmapImage.DecodePixelWidth = 32;
                    bitmapImage.DecodePixelHeight = 32;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    Icon = bitmapImage;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load C4 icon: {ex.Message}", ex);
        }
        
        // If all fails, the window will use the default icon
        // This is fine - the app will still work
    }
    
    private ImageSource ConvertIconToImageSource(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            // Note: hBitmap needs to be deleted, but we can't do that here easily
            // The OS will clean it up when the ImageSource is garbage collected
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            // Hide window when minimized
            Hide();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosing)
        {
            // Cancel the close and minimize to tray instead
            e.Cancel = true;
            Hide();
            
            // Update system tray tooltip
            _systemTrayService?.UpdateTooltip("Rust Desktop - Click to show");
        }
        else
        {
            base.OnClosing(e);
        }
    }

    /// <summary>
    /// Show the window and bring it to front
    /// </summary>
    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    /// <summary>
    /// Called when user wants to actually exit the application
    /// </summary>
    public void ForceClose()
    {
        _isClosing = true;
        Close();
    }

    private void ToggleLogs_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ShowLogs = !vm.ShowLogs;
        }
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var logText = vm.GetAllLogsText();
            if (!string.IsNullOrEmpty(logText))
            {
                Clipboard.SetText(logText);
                MessageBox.Show("Logs copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ClearLogs();
        }
    }

    private void Image_ImageFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
    {
        // Log image loading failures to help debug icon display issues
        if (sender is System.Windows.Controls.Image img)
        {
            var error = e.ErrorException?.Message ?? "Unknown error";
            System.Diagnostics.Debug.WriteLine($"[Image_ImageFailed] ✗✗✗ IMAGE LOAD FAILED: {error}");
            System.Diagnostics.Debug.WriteLine($"[Image_ImageFailed] Current Source: {img.Source}");
            
            string? iconPath = null;
            string? itemName = null;
            int? itemId = null;
            string? shortName = null;
            
            if (img.DataContext is VendingItem item)
            {
                itemName = item.ItemName;
                itemId = item.ItemId;
                shortName = item.ShortName;
                iconPath = item.IconUrl;
                System.Diagnostics.Debug.WriteLine($"[Image_ImageFailed] Item: {item.ItemName} (ItemId: {item.ItemId}), IconUrl: {item.IconUrl}");
            }
            
            // Get icon path from source if available
            if (string.IsNullOrEmpty(iconPath) && img.Source is System.Windows.Media.Imaging.BitmapImage bmp && bmp.UriSource != null)
            {
                iconPath = bmp.UriSource.LocalPath;
            }
            
            // Log to file
            _iconValidationService?.LogIconDisplayFailure(
                iconPath ?? "unknown", 
                itemName, 
                itemId, 
                shortName, 
                $"Image load failed: {error}");
            _logger?.LogError($"Image load failed for icon: {iconPath ?? "unknown"} (ItemId: {itemId}, ShortName: {shortName ?? "null"}): {error}", e.ErrorException);
            
            img.Source = null;
            img.Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    private void BitmapImage_DownloadFailed(object sender, System.Windows.Media.ExceptionEventArgs e)
    {
        // Log bitmap download failures to help debug icon display issues
        var error = e.ErrorException?.Message ?? "Unknown error";
        System.Diagnostics.Debug.WriteLine($"[BitmapImage_DownloadFailed] ✗✗✗ BITMAP DOWNLOAD FAILED: {error}");
        
        string? iconPath = null;
        if (sender is System.Windows.Media.Imaging.BitmapImage bmp)
        {
            System.Diagnostics.Debug.WriteLine($"[BitmapImage_DownloadFailed] Failed URI: {bmp.UriSource}");
            iconPath = bmp.UriSource?.LocalPath ?? bmp.UriSource?.ToString();
        }
        
        // Log to file
        _iconValidationService?.LogIconDisplayFailure(
            iconPath ?? "unknown",
            errorMessage: $"Bitmap download failed: {error}");
        _logger?.LogError($"Bitmap download failed for icon: {iconPath ?? "unknown"}: {error}", e.ErrorException);
    }

    private void Image_Loaded(object sender, RoutedEventArgs e)
    {
        // Log when images are loaded to help debug icon display issues
        if (sender is System.Windows.Controls.Image img)
        {
            System.Diagnostics.Debug.WriteLine($"[Image_Loaded] Image loaded. Source: {img.Source}, Visibility: {img.Visibility}");
            if (img.DataContext is RustDesktop.Core.Models.VendingItem item)
            {
                System.Diagnostics.Debug.WriteLine($"[Image_Loaded] Item: {item.ItemName} (ItemId: {item.ItemId}), IconUrl: {item.IconUrl}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Image_Loaded] ⚠⚠⚠ DataContext is not VendingItem! Type: {img.DataContext?.GetType().Name ?? "null"}");
            }
        }
    }
}

