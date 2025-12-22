using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;

namespace RustDesktop.Core.Services;

public class RustIconService : IRustIconService
{
    private string? _rustInstallPath;
    private readonly ILoggingService? _logger;
#pragma warning disable CS0649 // Field is assigned via reflection in App.xaml.cs
    internal IItemNameService? _itemNameService; // Made internal for reflection access
#pragma warning restore CS0649

    public bool IsRustInstalled => GetRustInstallPath() != null;

    public RustIconService(ILoggingService? logger = null)
    {
        _logger = logger;
    }

    public string? GetRustInstallPath()
    {
        if (_rustInstallPath != null && Directory.Exists(_rustInstallPath))
            return _rustInstallPath;

        // Try to get Steam install path from registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key != null)
            {
                var steamPath = key.GetValue("SteamPath")?.ToString();
                if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                {
                    // Rust is typically in steamapps/common/Rust
                    var rustPath = Path.Combine(steamPath, "steamapps", "common", "Rust");
                    if (Directory.Exists(rustPath))
                    {
                        _rustInstallPath = rustPath;
                        _logger?.LogDebug($"Found Rust installation: {rustPath}");
                        return rustPath;
                    }

                    // Also check steamapps/common/rust (lowercase)
                    rustPath = Path.Combine(steamPath, "steamapps", "common", "rust");
                    if (Directory.Exists(rustPath))
                    {
                        _rustInstallPath = rustPath;
                        _logger?.LogDebug($"Found Rust installation: {rustPath}");
                        return rustPath;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug($"Error finding Rust installation: {ex.Message}");
        }

        // Try common alternative Steam library locations
        var commonPaths = new[]
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\Rust",
            @"C:\Program Files\Steam\steamapps\common\Rust",
            @"D:\Steam\steamapps\common\Rust",
            @"E:\Steam\steamapps\common\Rust",
            @"F:\Steam\steamapps\common\Rust"
        };

        foreach (var path in commonPaths)
        {
            if (Directory.Exists(path))
            {
                _rustInstallPath = path;
                _logger?.LogDebug($"Found Rust installation: {path}");
                return path;
            }
        }

        return null;
    }

    public string? GetItemIconPath(int itemId, string? shortName = null)
    {
        System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] START - ItemId: {itemId}, ShortName: {shortName ?? "null"}");
        
        // ONLY check application's local Icons folder - don't check Rust installation
        var appIconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons");
        System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Checking Icons folder: {appIconsPath}");
        System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
        
        if (!Directory.Exists(appIconsPath))
        {
            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] ✗ Icons folder does not exist: {appIconsPath}");
            _logger?.LogDebug($"Icons folder not found: {appIconsPath}");
            return null;
        }

        System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] ✓ Icons folder exists");
        
        // Count files in Icons folder for debugging
        try
        {
            var fileCount = Directory.GetFiles(appIconsPath, "*.png", SearchOption.TopDirectoryOnly).Length;
            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Icons folder contains {fileCount} PNG files");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Error counting files: {ex.Message}");
        }

        var possiblePaths = new List<string>();

        // Try by item ID first
        if (itemId != 0)
        {
            possiblePaths.Add(Path.Combine(appIconsPath, $"{itemId}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"item_{itemId}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{itemId}.jpg"));
            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Added {possiblePaths.Count} paths for ItemId: {itemId}");
        }

        // Try by short name - convert dots and hyphens to underscores (icon files use underscores)
        if (!string.IsNullOrWhiteSpace(shortName))
        {
            // Get item name for full name matching (for blueprint fragments, etc.)
            var itemName = _itemNameService?.GetItemName(itemId, shortName) ?? $"Item_{itemId}";
            
            // Convert "ammo.pistol" to "ammo_pistol" and "basic-blueprint-fragment" to "basic_blueprint_fragment" to match icon file naming convention
            var safeName = shortName.Replace(".", "_").Replace("-", "_");
            var safeNameLower = safeName.ToLowerInvariant();
            var shortNameLower = shortName.ToLowerInvariant();
            
            // Also create a single-word version (no separators) - e.g., "rifle.body" -> "riflebody", "basic-blueprint-fragment" -> "basicblueprintfragment"
            // This is the SAME METHOD that worked for blueprint fragments
            var singleWord = shortName.Replace(".", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
            
            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] ShortName: '{shortName}' -> safeName: '{safeName}' -> safeNameLower: '{safeNameLower}' -> singleWord: '{singleWord}'");
            
            // PRIORITY 1: Try single-word format FIRST (same method as blueprint fragments - this is what works!)
            // This matches: basicblueprintfragment.png, horsesaddlesingle.png, piebear.png, etc.
            if (!string.IsNullOrEmpty(singleWord))
            {
                possiblePaths.Add(Path.Combine(appIconsPath, $"{singleWord}.png"));
                possiblePaths.Add(Path.Combine(appIconsPath, $"{singleWord}.jpg"));
                System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Added single-word paths (PRIORITY 1): '{singleWord}.png'");
            }
            
            // PRIORITY 2: Try exact shortName with dots (for electric.furnace, etc.)
            possiblePaths.Add(Path.Combine(appIconsPath, $"{shortName}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{shortNameLower}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{shortName}.jpg"));
            
            // PRIORITY 3: Try full item name in lowercase (for blueprint fragments: "basic blueprint fragment.png")
            if (!string.IsNullOrWhiteSpace(itemName) && itemName != $"Item_{itemId}")
            {
                var fullNameLower = itemName.ToLowerInvariant();
                possiblePaths.Add(Path.Combine(appIconsPath, $"{fullNameLower}.png"));
                possiblePaths.Add(Path.Combine(appIconsPath, $"{fullNameLower}.jpg"));
                System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Added full item name paths: '{fullNameLower}.png'");
            }
            
            // PRIORITY 4: Direct match with underscores (most common format)
            possiblePaths.Add(Path.Combine(appIconsPath, $"{safeName}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{safeNameLower}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{safeName}.jpg"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{safeNameLower}.jpg"));
            
            // PRIORITY 5: Try with icon_ prefix
            possiblePaths.Add(Path.Combine(appIconsPath, $"icon_{safeName}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"icon_{safeNameLower}.png"));
            
            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Added {possiblePaths.Count} total paths to check");
        }

        // Check all specific paths first
        System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Checking {possiblePaths.Count} specific paths...");
        foreach (var path in possiblePaths)
        {
            var exists = File.Exists(path);
            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Checking: {Path.GetFileName(path)} -> exists: {exists}");
            if (exists)
            {
                System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] ✓✓✓ FOUND icon at: {path}");
                _logger?.LogDebug($"Found icon at: {path}");
                return path;
            }
        }

        // If not found by exact match, do a broader case-insensitive search
        if (!string.IsNullOrWhiteSpace(shortName))
        {
            var safeName = shortName.Replace(".", "_").Replace("-", "_").ToLowerInvariant();
            var shortNameLower = shortName.ToLowerInvariant();
            var singleWord = shortName.Replace(".", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
            var searchPatterns = new[] { "*.png", "*.jpg", "*.jpeg" };
            
            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] No exact match found, doing broad search for: safeName='{safeName}', singleWord='{singleWord}'");
            
            foreach (var pattern in searchPatterns)
            {
                try
                {
                    var iconFiles = Directory.GetFiles(appIconsPath, pattern, SearchOption.TopDirectoryOnly);
                    System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Found {iconFiles.Length} files matching '{pattern}'");
                    
                    // Show first 10 files for debugging
                    foreach (var iconFile in iconFiles.Take(10))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(iconFile).ToLowerInvariant();
                        System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath]   Sample file: {Path.GetFileName(iconFile)} (name: '{fileName}')");
                    }
                    
                    foreach (var iconFile in iconFiles)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(iconFile).ToLowerInvariant();
                        
                        // Match by short name (exact matches first, prioritize singleWord for blueprint fragments)
                        // Prioritize exact matches to avoid false positives
                        bool matches = fileName == singleWord ||
                            fileName == safeName || 
                            fileName == shortNameLower ||
                            fileName == $"icon_{singleWord}" ||
                            fileName == $"icon_{safeName}" ||
                            // Only use Contains for singleWord to avoid too broad matches
                            (singleWord.Length > 5 && fileName.Contains(singleWord));
                            
                        if (matches)
                        {
                            System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] ✓✓✓ MATCHED in broad search: {iconFile} (fileName: '{fileName}', matched: safeName='{safeName}' or singleWord='{singleWord}')");
                            _logger?.LogDebug($"Found icon by short name in app Icons folder (broad search): {iconFile}");
                            return iconFile;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] Error during broad search: {ex.Message}");
                    _logger?.LogDebug($"Error doing broad search in app Icons folder: {ex.Message}");
                }
            }
        }

        System.Diagnostics.Debug.WriteLine($"[RustIconService.GetItemIconPath] ✗✗✗ NO ICON FOUND for ItemId: {itemId}, ShortName: {shortName ?? "null"}");
        _logger?.LogDebug($"Icon not found for ItemId: {itemId}, ShortName: {shortName}");
        return null;
    }

    public string? GetItemIconPath(string? shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName))
            return null;

        return GetItemIconPath(0, shortName);
    }
}




