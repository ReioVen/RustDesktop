using System.Windows;
using System.Windows.Controls;
using RustDesktop.App.ViewModels;
using RustDesktop.Core.Services;
using RustDesktop.Core.Models;

namespace RustDesktop.App.Views;

public partial class MainWindow : Window
{
    private readonly IconValidationService? _iconValidationService;
    private readonly ILoggingService? _logger;

    public MainWindow(MainViewModel viewModel, IconValidationService? iconValidationService = null, ILoggingService? logger = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _iconValidationService = iconValidationService;
        _logger = logger;
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

