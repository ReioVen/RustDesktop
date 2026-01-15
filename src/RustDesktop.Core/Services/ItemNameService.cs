using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace RustDesktop.Core.Services;

public class ItemNameService : IItemNameService
{
    private readonly Dictionary<int, string> _idToShortName = new();
    private readonly Dictionary<string, string> _shortNameToDisplayName = new();
    private readonly Dictionary<int, string> _idToIconUrl = new();
    private readonly Dictionary<string, string> _shortNameToIconUrl = new();
    private readonly IRustIconService? _rustIconService;
    private bool _isLoaded = false;

    public bool IsLoaded => _isLoaded;

    public ItemNameService(IRustIconService? rustIconService = null)
    {
        _rustIconService = rustIconService;
        try
        {
            LoadItemMappings();
        }
        catch
        {
            // Silently fail - item names will fall back to formatted short names or Item_ID
            // This prevents startup crashes if file access fails
        }
    }


    public void LoadItemMappings()
    {
        if (_isLoaded) return;

        _idToShortName.Clear();
        _shortNameToDisplayName.Clear();

        // Try to find the item list JSON file
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rust-item-list.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rust_items.json"),
            Path.Combine(@"C:\Programming\RustDesktop\rustplus-desktop-3.0.1\rustplus-desktop-3.0.1\RustPlusDesktop", "rust-item-list.json"),
            Path.Combine(@"C:\Programming\RustDesktop\rustplus-desktop-3.0.1\rustplus-desktop-3.0.1\RustPlusDesktop", "rust_items.json")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    if (TryLoadFromJson(json))
                    {
                        _isLoaded = true;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue to next path
                    // Silently continue - this prevents startup crashes
                    System.Diagnostics.Debug.WriteLine($"Failed to load item mappings from {path}: {ex.Message}");
                }
            }
        }
    }

    private bool TryLoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            
            // Try rust-item-list.json format (array)
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    try
                    {
                        if (el.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                        {
                            var id = idProp.GetInt32();
                            var shortName = el.TryGetProperty("shortName", out var snProp) ? snProp.GetString() : null;
                            var displayName = el.TryGetProperty("displayName", out var dnProp) ? dnProp.GetString() : null;
                            var iconUrl = el.TryGetProperty("iconUrl", out var iconProp) ? iconProp.GetString() : null;

                            if (!string.IsNullOrWhiteSpace(shortName))
                            {
                                _idToShortName[id] = shortName;
                                if (!string.IsNullOrWhiteSpace(displayName))
                                {
                                    _shortNameToDisplayName[shortName] = displayName;
                                }
                                if (!string.IsNullOrWhiteSpace(iconUrl))
                                {
                                    _idToIconUrl[id] = iconUrl;
                                    _shortNameToIconUrl[shortName] = iconUrl;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid entries, continue processing
                        continue;
                    }
                }
                return _idToShortName.Count > 0;
            }
            
            // Try rust_items.json format (object with id_to_short and short_to_nice)
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    if (doc.RootElement.TryGetProperty("id_to_short", out var idToShort) && idToShort.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in idToShort.EnumerateObject())
                        {
                            try
                            {
                                if (int.TryParse(prop.Name, out var id))
                                {
                                    var shortName = prop.Value.GetString();
                                    if (!string.IsNullOrWhiteSpace(shortName))
                                    {
                                        _idToShortName[id] = shortName;
                                    }
                                }
                            }
                            catch
                            {
                                // Skip invalid entries
                                continue;
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("short_to_nice", out var shortToNice) && shortToNice.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in shortToNice.EnumerateObject())
                        {
                            try
                            {
                                var displayName = prop.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(prop.Name))
                                {
                                    _shortNameToDisplayName[prop.Name] = displayName;
                                }
                            }
                            catch
                            {
                                // Skip invalid entries
                                continue;
                            }
                        }
                    }
                }
                catch
                {
                    // If object parsing fails, return what we have so far
                }

                return _idToShortName.Count > 0 || _shortNameToDisplayName.Count > 0;
            }
        }
        catch (JsonException)
        {
            // Invalid JSON format
            return false;
        }
        catch (Exception)
        {
            // Any other error
            return false;
        }

        return false;
    }

    public string? GetShortName(int itemId)
    {
        if (_idToShortName.TryGetValue(itemId, out var shortName) && !string.IsNullOrWhiteSpace(shortName))
        {
            return shortName;
        }
        return null;
    }

    public string GetItemName(int itemId, string? shortName = null)
    {
        try
        {
            // If we have a short name, try to get display name from it
            if (!string.IsNullOrWhiteSpace(shortName))
            {
                if (_shortNameToDisplayName.TryGetValue(shortName, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
                // If no display name, return formatted short name
                return FormatShortName(shortName);
            }

            // Try to get short name from ID
            if (_idToShortName.TryGetValue(itemId, out var sn) && !string.IsNullOrWhiteSpace(sn))
            {
                // Try to get display name
                if (_shortNameToDisplayName.TryGetValue(sn, out var dn) && !string.IsNullOrWhiteSpace(dn))
                {
                    return dn;
                }
                return FormatShortName(sn);
            }
        }
        catch
        {
            // Fall through to fallback
        }

        // Fallback to Item ID
        return $"Item_{itemId}";
    }

    public string GetItemName(string? shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName))
            return "Unknown Item";

        try
        {
            if (_shortNameToDisplayName.TryGetValue(shortName, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }
        }
        catch
        {
            // Fall through to formatting
        }

        return FormatShortName(shortName);
    }

    private static string FormatShortName(string shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName))
            return "Unknown Item";

        // Convert "item.name.here" to "Item Name Here"
        return string.Join(" ", shortName.Split('.')
            .Select(word => 
            {
                if (word.Length == 0) return "";
                if (word.Length == 1) return char.ToUpperInvariant(word[0]).ToString();
                return char.ToUpperInvariant(word[0]) + word[1..];
            }));
    }

    public string? GetItemIconUrl(int itemId, string? shortName = null)
    {
        // If we have a short name, try to get icon URL from it
        if (!string.IsNullOrWhiteSpace(shortName))
        {
            if (_shortNameToIconUrl.TryGetValue(shortName, out var url))
            {
                return url;
            }
        }

        // Try to get icon URL from ID
        if (_idToIconUrl.TryGetValue(itemId, out var iconUrl))
        {
            return iconUrl;
        }

        // Try to get short name from ID and then icon URL
        if (_idToShortName.TryGetValue(itemId, out var sn))
        {
            if (_shortNameToIconUrl.TryGetValue(sn, out var url2))
            {
                return url2;
            }
        }

        return null;
    }

    public string? GetItemIconUrl(string? shortName)
    {
        if (string.IsNullOrWhiteSpace(shortName))
            return null;

        if (_shortNameToIconUrl.TryGetValue(shortName, out var iconUrl))
        {
            return iconUrl;
        }

        return null;
    }

    public string? GetItemIconPath(int itemId, string? shortName = null)
    {
        System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] START - ItemId: {itemId}, ShortName: {shortName ?? "null"}");
        
        // ONLY check app's Icons folder - don't check Rust installation
        var appIconsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons");
        System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Checking Icons folder: {appIconsPath}");
        
        if (!Directory.Exists(appIconsPath))
        {
            System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Icons folder does not exist: {appIconsPath}");
            return null;
        }

        System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Icons folder exists, proceeding with search");
        
        var possiblePaths = new List<string>();
        
        // Try by item ID first
        if (itemId != 0)
        {
            possiblePaths.Add(Path.Combine(appIconsPath, $"{itemId}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"item_{itemId}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{itemId}.jpg"));
            System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Added {possiblePaths.Count} paths for ItemId: {itemId}");
        }
        
        // Try by short name (convert dots and hyphens to underscores to match icon file naming)
        if (!string.IsNullOrWhiteSpace(shortName))
        {
            // Get item name for full name matching (for blueprint fragments, etc.)
            var itemName = GetItemName(itemId, shortName);
            
            // Convert "ammo.pistol" to "ammo_pistol" and "basic-blueprint-fragment" to "basic_blueprint_fragment" to match icon file names
            var safeName = shortName.Replace(".", "_").Replace("-", "_");
            var safeNameLower = safeName.ToLowerInvariant();
            
            // Also create a single-word version (no separators) - e.g., "rifle.body" -> "riflebody", "basic-blueprint-fragment" -> "basicblueprintfragment"
            // This is the SAME METHOD that worked for blueprint fragments
            var singleWord = shortName.Replace(".", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
            
            System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] ShortName: '{shortName}' -> safeName: '{safeName}' -> safeNameLower: '{safeNameLower}' -> singleWord: '{singleWord}'");
            
            // PRIORITY 1: Try single-word format FIRST (same method as blueprint fragments - this is what works!)
            // This matches: basicblueprintfragment.png, horsesaddlesingle.png, piebear.png, etc.
            if (!string.IsNullOrEmpty(singleWord))
            {
                possiblePaths.Add(Path.Combine(appIconsPath, $"{singleWord}.png"));
                possiblePaths.Add(Path.Combine(appIconsPath, $"{singleWord}.jpg"));
                System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Added single-word paths (PRIORITY 1): '{singleWord}.png'");
            }
            
            // PRIORITY 2: Try exact shortName with dots (for electric.furnace, etc.)
            possiblePaths.Add(Path.Combine(appIconsPath, $"{shortName}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{shortName.ToLowerInvariant()}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{shortName}.jpg"));
            
            // PRIORITY 3: Try full item name in lowercase (for blueprint fragments: "basic blueprint fragment.png")
            if (!string.IsNullOrWhiteSpace(itemName) && itemName != $"Item_{itemId}")
            {
                var fullNameLower = itemName.ToLowerInvariant();
                possiblePaths.Add(Path.Combine(appIconsPath, $"{fullNameLower}.png"));
                possiblePaths.Add(Path.Combine(appIconsPath, $"{fullNameLower}.jpg"));
                System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Added full item name paths: '{fullNameLower}.png'");
            }
            
            // PRIORITY 4: Direct match with underscores (most common format)
            possiblePaths.Add(Path.Combine(appIconsPath, $"{safeName}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{safeNameLower}.png"));
            
            // PRIORITY 5: Try with icon_ prefix
            possiblePaths.Add(Path.Combine(appIconsPath, $"icon_{safeName}.png"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"icon_{safeNameLower}.png"));
            
            // PRIORITY 6: Try JPG variants
            possiblePaths.Add(Path.Combine(appIconsPath, $"{safeName}.jpg"));
            possiblePaths.Add(Path.Combine(appIconsPath, $"{safeNameLower}.jpg"));
            
            System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Added {possiblePaths.Count} total paths to check");
        }
        
        // Check paths in order
        System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Checking {possiblePaths.Count} specific paths...");
        foreach (var path in possiblePaths)
        {
            System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Checking path: {path} (exists: {File.Exists(path)})");
            if (File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] ✓ FOUND icon at: {path}");
                return path;
            }
        }

        // If not found by exact match, do a broader search (case-insensitive)
        if (!string.IsNullOrWhiteSpace(shortName))
        {
            var safeName = shortName.Replace(".", "_").Replace("-", "_").ToLowerInvariant();
            var singleWord = shortName.Replace(".", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
            var shortNameLower = shortName.ToLowerInvariant();
            var searchPatterns = new[] { "*.png", "*.jpg", "*.jpeg" };
            
            System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] No exact match found, doing broad search for: safeName='{safeName}', singleWord='{singleWord}'");
            
            foreach (var pattern in searchPatterns)
            {
                try
                {
                    var iconFiles = Directory.GetFiles(appIconsPath, pattern, SearchOption.TopDirectoryOnly);
                    System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Found {iconFiles.Length} files matching pattern '{pattern}'");
                    
                    foreach (var iconFile in iconFiles)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(iconFile).ToLowerInvariant();
                        
                        // Match by short name (exact matches first, then contains)
                        // Prioritize exact matches to avoid false positives
                        if (fileName == singleWord ||
                            fileName == safeName || 
                            fileName == shortNameLower ||
                            fileName == $"icon_{singleWord}" ||
                            fileName == $"icon_{safeName}" ||
                            // Only use Contains for singleWord to avoid too broad matches
                            (singleWord.Length > 5 && fileName.Contains(singleWord)))
                        {
                            System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] ✓ MATCHED in broad search: '{fileName}' matched '{safeName}' or '{singleWord}' -> {iconFile}");
                            return iconFile;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] Error during broad search: {ex.Message}");
                    // Continue to next pattern
                }
            }
        }

        System.Diagnostics.Debug.WriteLine($"[ItemNameService.GetItemIconPath] ✗ NO ICON FOUND for ItemId: {itemId}, ShortName: {shortName ?? "null"}");
        return null;
    }

    public string? GetItemIconPath(string? shortName)
    {
        return GetItemIconPath(0, shortName);
    }
}




