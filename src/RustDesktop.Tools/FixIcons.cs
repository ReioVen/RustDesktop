using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RustDesktop.Tools;

public static class FixIcons
{
    public static void Run(string[] args)
    {
        Console.WriteLine("Icon Fixer & Matcher");
        Console.WriteLine("=====================");
        Console.WriteLine();

        var iconsFolder = args.Length > 0 ? args[0] : Path.Combine("..", "..", "src", "RustDesktop.App", "Icons");
        iconsFolder = Path.GetFullPath(iconsFolder);

        if (!Directory.Exists(iconsFolder))
        {
            Console.WriteLine($"❌ Icons folder not found: {iconsFolder}");
            return;
        }

        Console.WriteLine($"Icons folder: {iconsFolder}");
        Console.WriteLine();

        // Load item mappings
        var itemMappings = LoadItemMappings();
        Console.WriteLine($"Loaded {itemMappings.Count} item mappings");
        Console.WriteLine();

        // Get all icon files
        var iconFiles = Directory.GetFiles(iconsFolder, "*.png", SearchOption.TopDirectoryOnly)
            .Select(f => new FileInfo(f))
            .ToList();

        Console.WriteLine($"Found {iconFiles.Count} icon files");
        Console.WriteLine();

        // Create reverse mapping: shortName -> displayName
        var shortNameToDisplayName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in itemMappings)
        {
            if (!shortNameToDisplayName.ContainsKey(mapping.Value))
            {
                shortNameToDisplayName[mapping.Value] = mapping.Key;
            }
        }

        // Analyze each icon file
        int matched = 0;
        int unmatched = 0;
        var unmatchedFiles = new List<(FileInfo file, string reason)>();

        foreach (var iconFile in iconFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(iconFile.Name);
            
            // Try to match by converting underscore back to dot
            var shortNameWithDots = fileName.Replace("_", ".");
            
            // Check if it matches an item
            bool found = false;
            string? matchedItem = null;
            
            // Try exact match
            if (shortNameToDisplayName.TryGetValue(shortNameWithDots, out var displayName))
            {
                found = true;
                matchedItem = displayName;
            }
            else
            {
                // Try case-insensitive match
                var match = shortNameToDisplayName.FirstOrDefault(kvp => 
                    string.Equals(kvp.Key, shortNameWithDots, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match.Value))
                {
                    found = true;
                    matchedItem = match.Value;
                }
            }

            if (found)
            {
                matched++;
                if (matched % 100 == 0)
                {
                    Console.Write($"Matched {matched} icons...\r");
                }
            }
            else
            {
                unmatched++;
                unmatchedFiles.Add((iconFile, $"No matching item found for shortName: {shortNameWithDots}"));
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Matching Results:");
        Console.WriteLine($"  ✓ Matched: {matched}");
        Console.WriteLine($"  ✗ Unmatched: {unmatched}");
        Console.WriteLine();

        if (unmatchedFiles.Count > 0)
        {
            Console.WriteLine("Unmatched Icons (first 20):");
            foreach (var (file, reason) in unmatchedFiles.Take(20))
            {
                Console.WriteLine($"  - {file.Name}: {reason}");
            }
            if (unmatchedFiles.Count > 20)
            {
                Console.WriteLine($"  ... and {unmatchedFiles.Count - 20} more");
            }
        }

        // Check for items that should have icons but don't
        var missingIcons = new List<string>();
        foreach (var mapping in itemMappings)
        {
            var expectedIconName = mapping.Value.Replace(".", "_") + ".png";
            var iconPath = Path.Combine(iconsFolder, expectedIconName);
            if (!File.Exists(iconPath))
            {
                missingIcons.Add($"{mapping.Key} ({mapping.Value} -> {expectedIconName})");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Items without icons: {missingIcons.Count}");
        if (missingIcons.Count > 0 && missingIcons.Count <= 20)
        {
            Console.WriteLine("Missing icons (first 20):");
            foreach (var missing in missingIcons.Take(20))
            {
                Console.WriteLine($"  - {missing}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=====================");
        Console.WriteLine($"Summary:");
        Console.WriteLine($"  Total icons: {iconFiles.Count}");
        Console.WriteLine($"  Matched to items: {matched}");
        Console.WriteLine($"  Unmatched icons: {unmatched}");
        Console.WriteLine($"  Items without icons: {missingIcons.Count}");
        Console.WriteLine();
        Console.WriteLine("✓ Icon analysis complete!");
    }

    private static Dictionary<string, string> LoadItemMappings()
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rust-item-list.json"),
            Path.Combine("..", "..", "rustplus-desktop-3.0.1", "rustplus-desktop-3.0.1", "RustPlusDesktop", "rust-item-list.json"),
            Path.Combine("..", "..", "..", "rustplus-desktop-3.0.1", "rustplus-desktop-3.0.1", "RustPlusDesktop", "rust-item-list.json"),
            Path.Combine("..", "..", "..", "..", "rustplus-desktop-3.0.1", "rustplus-desktop-3.0.1", "RustPlusDesktop", "rust-item-list.json"),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                try
                {
                    var json = File.ReadAllText(fullPath);
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var displayName = el.TryGetProperty("displayName", out var dnProp) ? dnProp.GetString() : null;
                            var shortName = el.TryGetProperty("shortName", out var snProp) ? snProp.GetString() : null;
                            
                            if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(shortName))
                            {
                                mappings[displayName] = shortName;
                            }
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not load mappings from {fullPath}: {ex.Message}");
                }
            }
        }

        return mappings;
    }
}






