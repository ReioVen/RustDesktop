using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media.Imaging;
using RustDesktop.Core.Services;

namespace RustDesktop.App.Converters;

public class IconPathConverter : IValueConverter
{
    private static IconValidationService? _iconValidationService;
    private static ILoggingService? _logger;

    public static void Initialize(IconValidationService? iconValidationService, ILoggingService? logger)
    {
        _iconValidationService = iconValidationService;
        _logger = logger;
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Convert called with value: {value ?? "null"}");
        
        if (value == null || value is not string path || string.IsNullOrWhiteSpace(path))
        {
            System.Diagnostics.Debug.WriteLine($"[IconPathConverter] ✗ Value is null or empty");
            return null;
        }

        System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Processing path: {path}");

        try
        {
            // Handle absolute paths directly
            string normalizedPath = path;
            if (Path.IsPathRooted(path))
            {
                // Already an absolute path - use it directly
                normalizedPath = path;
                System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Path is already absolute: {normalizedPath}");
            }
            else if (!path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Try to resolve relative paths relative to the application base directory
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                normalizedPath = Path.Combine(baseDir, path);
                System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Normalized relative path: {path} -> {normalizedPath}");
            }
            
            System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Final normalized path: {normalizedPath}");
            var fileExists = File.Exists(normalizedPath);
            System.Diagnostics.Debug.WriteLine($"[IconPathConverter] File exists: {fileExists}");
            
            // If it's a local file path, validate it exists and is readable
            if (fileExists)
            {
                // Validate it's a valid image file by checking file header
                try
                {
                    var fileInfo = new FileInfo(normalizedPath);
                    if (fileInfo.Length == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"IconPathConverter: Empty file: {normalizedPath}");
                        _iconValidationService?.LogIconDisplayFailure(normalizedPath, errorMessage: "Icon file is empty");
                        _logger?.LogError($"IconPathConverter: Empty icon file: {normalizedPath}");
                        return null;
                    }

                    var header = new byte[8];
                    using (var fs = new FileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (fs.Length < 8)
                        {
                            System.Diagnostics.Debug.WriteLine($"IconPathConverter: File too small: {normalizedPath}");
                            _iconValidationService?.LogIconDisplayFailure(normalizedPath, errorMessage: "Icon file too small");
                            _logger?.LogError($"IconPathConverter: Icon file too small: {normalizedPath}");
                            return null; // File too small
                        }
                        fs.Read(header, 0, 8);
                    }
                    
                    // Check PNG signature: 89 50 4E 47 0D 0A 1A 0A
                    bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
                    // Check JPEG signature: FF D8 FF
                    bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
                    
                    if (!isPng && !isJpeg)
                    {
                        System.Diagnostics.Debug.WriteLine($"IconPathConverter: Invalid image file (not PNG/JPEG): {normalizedPath}");
                        _iconValidationService?.LogIconDisplayFailure(normalizedPath, errorMessage: "Icon file is not a valid PNG/JPEG image");
                        _logger?.LogError($"IconPathConverter: Invalid image file (not PNG/JPEG): {normalizedPath}");
                        return null;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"IconPathConverter: Access denied: {normalizedPath}");
                    _iconValidationService?.LogIconDisplayFailure(normalizedPath, errorMessage: $"Access denied: {ex.Message}");
                    _logger?.LogError($"IconPathConverter: Access denied reading icon file: {normalizedPath}", ex);
                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"IconPathConverter: Error validating file {normalizedPath}: {ex.Message}");
                    _iconValidationService?.LogIconDisplayFailure(normalizedPath, errorMessage: $"Error validating file: {ex.Message}");
                    _logger?.LogError($"IconPathConverter: Error validating icon file: {normalizedPath}", ex);
                    return null;
                }
                
                try
                {
                    // Create a BitmapImage from the file path
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    
                    // Ensure we use file:/// URI format for local files
                    Uri uri;
                    if (!normalizedPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        // Convert Windows path to file:/// URI format
                        uri = new Uri(new Uri("file:///"), normalizedPath);
                    }
                    else
                    {
                        uri = new Uri(normalizedPath, UriKind.Absolute);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Creating BitmapImage with URI: {uri}");
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Original path: {normalizedPath}");
                    bitmap.UriSource = uri;
                    bitmap.DecodePixelWidth = 32;
                    bitmap.DecodePixelHeight = 32;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Freeze for better performance
                    
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] ✓✓✓ SUCCESS - Created BitmapImage from: {normalizedPath} (URI: {uri})");
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] BitmapImage PixelWidth: {bitmap.PixelWidth}, PixelHeight: {bitmap.PixelHeight}");
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] ✗✗✗ ERROR creating BitmapImage from {normalizedPath}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Stack trace: {ex.StackTrace}");
                    
                    // Log error to file
                    _iconValidationService?.LogIconDisplayFailure(normalizedPath, errorMessage: $"Failed to create BitmapImage: {ex.Message}");
                    _logger?.LogError($"IconPathConverter: Failed to create BitmapImage from {normalizedPath}: {ex.Message}", ex);
                    
                    return null;
                }
            }

            // If it's already a URL (http:// or https://), use it directly
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.DecodePixelWidth = 32;
                    bitmap.DecodePixelHeight = 32;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Error creating BitmapImage from URL {path}: {ex.Message}");
                    return null;
                }
            }

            // Try as file path anyway (if it's an absolute path or we can normalize it)
            if (File.Exists(normalizedPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(normalizedPath, UriKind.Absolute);
                    bitmap.DecodePixelWidth = 32;
                    bitmap.DecodePixelHeight = 32;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Found file on fallback path: {normalizedPath}");
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Error creating BitmapImage from fallback path {normalizedPath}: {ex.Message}");
                }
            }
            
            // Last attempt: try the original path if it's rooted
            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.DecodePixelWidth = 32;
                    bitmap.DecodePixelHeight = 32;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IconPathConverter] Error creating BitmapImage from original path {path}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            System.Diagnostics.Debug.WriteLine($"IconPathConverter: Error converting path '{path}': {ex.Message}");
            _iconValidationService?.LogIconDisplayFailure(path, errorMessage: $"Exception during conversion: {ex.Message}");
            _logger?.LogError($"IconPathConverter: Error converting icon path '{path}'", ex);
            return null;
        }

        // If we get here, the icon path was not found or could not be loaded
        if (!string.IsNullOrWhiteSpace(path) && !path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _iconValidationService?.LogIconDisplayFailure(path, errorMessage: "Icon path not found or could not be loaded");
        }
        
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}




