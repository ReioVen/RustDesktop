using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

/// <summary>
/// Service for validating icon availability and logging errors
/// </summary>
public class IconValidationService
{
    private readonly IItemNameService? _itemNameService;
    private readonly ILoggingService? _logger;
    private readonly string _iconErrorLogPath;
    private readonly HashSet<string> _loggedErrors = new(); // Track already logged errors to avoid duplicates

    public IconValidationService(IItemNameService? itemNameService = null, ILoggingService? logger = null)
    {
        _itemNameService = itemNameService;
        _logger = logger;
        
        // Create icon error log file in AppData
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RustDesktop"
        );
        Directory.CreateDirectory(appDataPath);
        _iconErrorLogPath = Path.Combine(appDataPath, "icon-errors.log");
    }

    /// <summary>
    /// Tests if an icon can be found for the given item
    /// </summary>
    public IconTestResult TestIcon(int itemId, string? shortName = null, string? itemName = null)
    {
        var result = new IconTestResult
        {
            ItemId = itemId,
            ShortName = shortName,
            ItemName = itemName ?? _itemNameService?.GetItemName(itemId, shortName) ?? $"Item_{itemId}",
            Timestamp = DateTime.Now
        };

        try
        {
            // Try to get icon path
            var iconPath = _itemNameService?.GetItemIconPath(itemId, shortName);
            
            if (string.IsNullOrEmpty(iconPath))
            {
                result.Found = false;
                result.ErrorMessage = "Icon path not found";
                result.SearchedPaths = GetSearchedPaths(itemId, shortName);
                LogIconError(result);
                return result;
            }

            // Check if file exists
            if (!File.Exists(iconPath))
            {
                result.Found = false;
                result.ErrorMessage = $"Icon file does not exist: {iconPath}";
                result.IconPath = iconPath;
                LogIconError(result);
                return result;
            }

            // Validate file is readable and valid image
            try
            {
                var fileInfo = new FileInfo(iconPath);
                if (fileInfo.Length == 0)
                {
                    result.Found = false;
                    result.ErrorMessage = $"Icon file is empty: {iconPath}";
                    result.IconPath = iconPath;
                    LogIconError(result);
                    return result;
                }

                // Check file header for valid image format
                var header = new byte[8];
                using (var fs = new FileStream(iconPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length < 8)
                    {
                        result.Found = false;
                        result.ErrorMessage = $"Icon file too small: {iconPath}";
                        result.IconPath = iconPath;
                        LogIconError(result);
                        return result;
                    }
                    fs.Read(header, 0, 8);
                }

                // Check PNG signature: 89 50 4E 47 0D 0A 1A 0A
                bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
                // Check JPEG signature: FF D8 FF
                bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

                if (!isPng && !isJpeg)
                {
                    result.Found = false;
                    result.ErrorMessage = $"Icon file is not a valid PNG/JPEG image: {iconPath}";
                    result.IconPath = iconPath;
                    LogIconError(result);
                    return result;
                }

                // Icon is valid
                result.Found = true;
                result.IconPath = iconPath;
                return result;
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Found = false;
                result.ErrorMessage = $"Access denied reading icon file: {iconPath} - {ex.Message}";
                result.IconPath = iconPath;
                LogIconError(result);
                return result;
            }
            catch (Exception ex)
            {
                result.Found = false;
                result.ErrorMessage = $"Error validating icon file: {iconPath} - {ex.Message}";
                result.IconPath = iconPath;
                LogIconError(result);
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Found = false;
            result.ErrorMessage = $"Exception during icon test: {ex.Message}";
            LogIconError(result);
            return result;
        }
    }

    /// <summary>
    /// Tests all icons for items in vending machines
    /// </summary>
    public IconTestSummary TestAllIcons(List<VendingMachine> vendingMachines)
    {
        var summary = new IconTestSummary
        {
            Timestamp = DateTime.Now,
            TotalItems = 0,
            IconsFound = 0,
            IconsMissing = 0,
            Errors = new List<IconTestResult>()
        };

        foreach (var machine in vendingMachines)
        {
            // Test sell items
            foreach (var item in machine.Items)
            {
                summary.TotalItems++;
                var result = TestIcon(item.ItemId, item.ShortName, item.ItemName);
                if (result.Found)
                {
                    summary.IconsFound++;
                }
                else
                {
                    summary.IconsMissing++;
                    summary.Errors.Add(result);
                }
            }

            // Test buy items
            foreach (var item in machine.BuyItems)
            {
                summary.TotalItems++;
                var result = TestIcon(item.ItemId, item.ShortName, item.ItemName);
                if (result.Found)
                {
                    summary.IconsFound++;
                }
                else
                {
                    summary.IconsMissing++;
                    summary.Errors.Add(result);
                }
            }
        }

        // Log summary to app (this will show in the UI)
        var summaryMessage = $"📊 Icon Test: {summary.IconsFound}/{summary.TotalItems} found, {summary.IconsMissing} missing";
        if (summary.IconsMissing > 0)
        {
            _logger?.LogWarning(summaryMessage);
        }
        else
        {
            _logger?.LogInfo(summaryMessage);
        }
        WriteToErrorLog($"\n=== Icon Test Summary: {summary.IconsFound}/{summary.TotalItems} icons found, {summary.IconsMissing} missing ===\n");

        return summary;
    }

    /// <summary>
    /// Logs icon display failure (called when image fails to load in UI)
    /// </summary>
    public void LogIconDisplayFailure(string iconPath, string? itemName = null, int? itemId = null, string? shortName = null, string? errorMessage = null)
    {
        var errorKey = $"{itemId}_{shortName}_{iconPath}";
        if (_loggedErrors.Contains(errorKey))
        {
            return; // Already logged this error
        }
        _loggedErrors.Add(errorKey);

        var result = new IconTestResult
        {
            ItemId = itemId ?? 0,
            ShortName = shortName,
            ItemName = itemName ?? $"Item_{itemId}",
            IconPath = iconPath,
            Found = false,
            ErrorMessage = errorMessage ?? "Failed to display icon in UI",
            Timestamp = DateTime.Now
        };

        LogIconError(result);
    }

    private void LogIconError(IconTestResult result)
    {
        var errorKey = $"{result.ItemId}_{result.ShortName}";
        if (_loggedErrors.Contains(errorKey))
        {
            return; // Already logged this error
        }
        _loggedErrors.Add(errorKey);

        // Create a concise error message for app display
        var shortMessage = $"❌ Icon missing: {result.ItemName} (ID: {result.ItemId})";
        if (!string.IsNullOrEmpty(result.ShortName))
        {
            shortMessage += $" - {result.ShortName}";
        }
        shortMessage += $" - {result.ErrorMessage}";
        
        // Log to app (this will show in the UI)
        _logger?.LogError(shortMessage);
        
        // Also write detailed info to file
        var detailedMessage = $"[ICON ERROR] ItemId: {result.ItemId}, ShortName: {result.ShortName ?? "null"}, ItemName: {result.ItemName}, Error: {result.ErrorMessage}";
        if (!string.IsNullOrEmpty(result.IconPath))
        {
            detailedMessage += $", IconPath: {result.IconPath}";
        }
        if (result.SearchedPaths != null && result.SearchedPaths.Count > 0)
        {
            detailedMessage += $", SearchedPaths: {string.Join(", ", result.SearchedPaths)}";
        }
        WriteToErrorLog($"[{result.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {detailedMessage}");
    }

    private void WriteToErrorLog(string message)
    {
        try
        {
            File.AppendAllText(_iconErrorLogPath, message + Environment.NewLine);
        }
        catch
        {
            // Ignore file write errors
        }
    }

    private List<string> GetSearchedPaths(int itemId, string? shortName)
    {
        var paths = new List<string>();
        var appIconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons");
        
        if (!Directory.Exists(appIconsPath))
        {
            return paths;
        }

        // Add paths that would be searched
        if (itemId != 0)
        {
            paths.Add(Path.Combine(appIconsPath, $"{itemId}.png"));
            paths.Add(Path.Combine(appIconsPath, $"item_{itemId}.png"));
            paths.Add(Path.Combine(appIconsPath, $"{itemId}.jpg"));
        }

        if (!string.IsNullOrWhiteSpace(shortName))
        {
            var safeName = shortName.Replace(".", "_").Replace("-", "_");
            var safeNameLower = safeName.ToLowerInvariant();
            var singleWord = shortName.Replace(".", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
            
            paths.Add(Path.Combine(appIconsPath, $"{safeName}.png"));
            paths.Add(Path.Combine(appIconsPath, $"{safeNameLower}.png"));
            paths.Add(Path.Combine(appIconsPath, $"{singleWord}.png"));
            paths.Add(Path.Combine(appIconsPath, $"{shortName}.png"));
            paths.Add(Path.Combine(appIconsPath, $"icon_{safeNameLower}.png"));
        }

        return paths;
    }
}

public class IconTestResult
{
    public int ItemId { get; set; }
    public string? ShortName { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public bool Found { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? SearchedPaths { get; set; }
    public DateTime Timestamp { get; set; }
}

public class IconTestSummary
{
    public DateTime Timestamp { get; set; }
    public int TotalItems { get; set; }
    public int IconsFound { get; set; }
    public int IconsMissing { get; set; }
    public List<IconTestResult> Errors { get; set; } = new();
}


