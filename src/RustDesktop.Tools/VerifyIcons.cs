using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RustDesktop.Tools;

public static class VerifyIcons
{
    public static void Run(string[] args)
    {
        Console.WriteLine("Rust Icon Verification Tool");
        Console.WriteLine("============================");
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
        Console.WriteLine($"Loaded {itemMappings.Count} item mappings from rust-item-list.json");
        Console.WriteLine();

        // Get all icon files
        var iconFiles = Directory.GetFiles(iconsFolder, "*.png", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"Found {iconFiles.Count} icon files");
        Console.WriteLine();

        // Verify icons against item mappings
        int matched = 0;
        int missing = 0;
        var missingIcons = new List<string>();
        var extraIcons = new HashSet<string>(iconFiles, StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in itemMappings)
        {
            var shortName = mapping.Value;
            var expectedIconName = shortName.Replace(".", "_");
            
            if (iconFiles.Contains(expectedIconName))
            {
                matched++;
                extraIcons.Remove(expectedIconName);
            }
            else
            {
                missing++;
                missingIcons.Add($"{mapping.Key} ({shortName} -> {expectedIconName}.png)");
            }
        }

        // Report results
        Console.WriteLine("============================");
        Console.WriteLine("Verification Results:");
        Console.WriteLine($"  ✓ Matched: {matched}");
        Console.WriteLine($"  ✗ Missing: {missing}");
        Console.WriteLine($"  ? Extra icons (not in item list): {extraIcons.Count}");
        Console.WriteLine();

        if (missing > 0)
        {
            Console.WriteLine("Missing Icons:");
            Console.WriteLine("-------------");
            foreach (var missingIcon in missingIcons.Take(20))
            {
                Console.WriteLine($"  - {missingIcon}");
            }
            if (missingIcons.Count > 20)
            {
                Console.WriteLine($"  ... and {missingIcons.Count - 20} more");
            }
            Console.WriteLine();
        }

        if (extraIcons.Count > 0)
        {
            Console.WriteLine("Extra Icons (not in item list):");
            Console.WriteLine("-------------------------------");
            foreach (var extraIcon in extraIcons.Take(20))
            {
                Console.WriteLine($"  + {extraIcon}.png");
            }
            if (extraIcons.Count > 20)
            {
                Console.WriteLine($"  ... and {extraIcons.Count - 20} more");
            }
            Console.WriteLine();
        }

        // Check for common naming issues
        Console.WriteLine("Checking for naming issues...");
        var namingIssues = new List<string>();
        
        foreach (var iconFile in iconFiles)
        {
            // Check if icon name looks like it might be incorrectly named
            if (iconFile.Contains("__")) // Double underscores
            {
                namingIssues.Add($"{iconFile}.png - contains double underscores");
            }
            if (iconFile.StartsWith("_") || iconFile.EndsWith("_"))
            {
                namingIssues.Add($"{iconFile}.png - starts or ends with underscore");
            }
        }

        if (namingIssues.Count > 0)
        {
            Console.WriteLine($"Found {namingIssues.Count} potential naming issues:");
            foreach (var issue in namingIssues.Take(10))
            {
                Console.WriteLine($"  ⚠ {issue}");
            }
            if (namingIssues.Count > 10)
            {
                Console.WriteLine($"  ... and {namingIssues.Count - 10} more");
            }
        }
        else
        {
            Console.WriteLine("✓ No obvious naming issues found");
        }

        // Generate a detailed report
        var reportPath = Path.Combine(iconsFolder, "icon_verification_report.txt");
        using (var writer = new StreamWriter(reportPath))
        {
            writer.WriteLine("Rust Icon Verification Report");
            writer.WriteLine("=============================");
            writer.WriteLine($"Generated: {DateTime.Now}");
            writer.WriteLine();
            writer.WriteLine($"Total items in rust-item-list.json: {itemMappings.Count}");
            writer.WriteLine($"Total icon files found: {iconFiles.Count}");
            writer.WriteLine($"Matched icons: {matched}");
            writer.WriteLine($"Missing icons: {missing}");
            writer.WriteLine($"Extra icons: {extraIcons.Count}");
            writer.WriteLine();
            
            if (missingIcons.Count > 0)
            {
                writer.WriteLine("MISSING ICONS:");
                writer.WriteLine("==============");
                foreach (var missingIcon in missingIcons)
                {
                    writer.WriteLine(missingIcon);
                }
                writer.WriteLine();
            }
            
            if (extraIcons.Count > 0)
            {
                writer.WriteLine("EXTRA ICONS (not in item list):");
                writer.WriteLine("================================");
                foreach (var extraIcon in extraIcons.OrderBy(x => x))
                {
                    writer.WriteLine($"{extraIcon}.png");
                }
                writer.WriteLine();
            }
            
            if (namingIssues.Count > 0)
            {
                writer.WriteLine("NAMING ISSUES:");
                writer.WriteLine("==============");
                foreach (var issue in namingIssues)
                {
                    writer.WriteLine(issue);
                }
            }
        }
        
        Console.WriteLine();
        Console.WriteLine($"📄 Detailed report saved to: {reportPath}");
        Console.WriteLine();
        Console.WriteLine("============================");
        
        if (missing == 0 && namingIssues.Count == 0)
        {
            Console.WriteLine("✓ All icons verified successfully!");
        }
        else
        {
            var coveragePercent = (matched * 100.0 / itemMappings.Count);
            Console.WriteLine($"⚠ Verification complete:");
            Console.WriteLine($"   Coverage: {coveragePercent:F1}% ({matched}/{itemMappings.Count} items have icons)");
            Console.WriteLine($"   Missing: {missing} icons");
            Console.WriteLine($"   Naming issues: {namingIssues.Count}");
            Console.WriteLine();
            Console.WriteLine("Note: Missing icons are items in rust-item-list.json that don't have corresponding icon files.");
            Console.WriteLine("      This is normal if the Gist data doesn't include all items.");
        }
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
                    Console.WriteLine($"Loaded mappings from: {fullPath}");
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












