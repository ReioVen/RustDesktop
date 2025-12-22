using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RustDesktop.Tools;

class Program
{
    // To run the icon copier instead, use: dotnet run -- copy-icons
    private static readonly HttpClient HttpClient = new HttpClient();
    private const string LocalDataFile = @"C:\Programming\RustDesktop\icons\Rust Item Names + Image URLs";
    
    static async Task Main(string[] args)
    {
        // Check if user wants to copy existing icons instead
        if (args.Length > 0 && args[0].Equals("copy-icons", StringComparison.OrdinalIgnoreCase))
        {
            CopyIcons.Run(args.Skip(1).ToArray());
            return;
        }

        // Check if user wants to verify icons
        if (args.Length > 0 && args[0].Equals("verify-icons", StringComparison.OrdinalIgnoreCase))
        {
            VerifyIcons.Run(args.Skip(1).ToArray());
            return;
        }

        // Check if user wants to validate icons
        if (args.Length > 0 && args[0].Equals("validate-icons", StringComparison.OrdinalIgnoreCase))
        {
            ValidateIcons.Run(args.Skip(1).ToArray());
            return;
        }

        // Check if user wants to fix/match icons
        if (args.Length > 0 && args[0].Equals("fix-icons", StringComparison.OrdinalIgnoreCase))
        {
            FixIcons.Run(args.Skip(1).ToArray());
            return;
        }

        Console.WriteLine("Rust Item Icon Downloader");
        Console.WriteLine("==========================");
        Console.WriteLine();

        var iconsFolder = args.Length > 0 ? args[0] : Path.Combine("..", "..", "RustDesktop.App", "Icons");
        iconsFolder = Path.GetFullPath(iconsFolder);
        
        if (!Directory.Exists(iconsFolder))
        {
            Directory.CreateDirectory(iconsFolder);
            Console.WriteLine($"Created icons folder: {iconsFolder}");
        }

        Console.WriteLine($"Icons will be saved to: {iconsFolder}");
        Console.WriteLine();

        // Load existing item mappings to map display names to short names
        var nameToShortName = LoadItemMappings();
        Console.WriteLine($"Loaded {nameToShortName.Count} item mappings");
        Console.WriteLine();

        // Load item data from local file
        Console.WriteLine("Loading item list from local file...");
        var items = LoadLocalData();
        if (items.Count == 0)
        {
            Console.WriteLine("⚠ No items found in local file, trying to download from Gist...");
            items = await DownloadGistData();
        }
        Console.WriteLine($"Found {items.Count} items");
        Console.WriteLine();

        // Download icons
        Console.WriteLine("Downloading icons...");
        int downloaded = 0;
        int skipped = 0;
        int failed = 0;

        foreach (var item in items)
        {
            try
            {
                var shortName = GetShortName(item.Name, nameToShortName);
                
                // If no mapping found, try to extract from image URL
                // rustlabs.com/img/items180/rifle.ak.png -> rifle.ak
                if (string.IsNullOrEmpty(shortName) && !string.IsNullOrEmpty(item.Image))
                {
                    var urlParts = item.Image.Split('/');
                    var imageFileName = urlParts.LastOrDefault();
                    if (!string.IsNullOrEmpty(imageFileName) && imageFileName.EndsWith(".png"))
                    {
                        shortName = imageFileName.Substring(0, imageFileName.Length - 4);
                    }
                }

                if (string.IsNullOrEmpty(shortName))
                {
                    Console.WriteLine($"⚠ Skipping '{item.Name}' - no short name found");
                    skipped++;
                    continue;
                }

                var fileName = $"{shortName.Replace(".", "_")}.png";
                var filePath = Path.Combine(iconsFolder, fileName);

                // Skip if already exists
                if (File.Exists(filePath))
                {
                    Console.WriteLine($"⊘ Skipping '{item.Name}' - {fileName} already exists");
                    skipped++;
                    continue;
                }

                // Download icon
                Console.Write($"↓ Downloading '{item.Name}' -> {fileName}... ");
                var imageData = await HttpClient.GetByteArrayAsync(item.Image);
                await File.WriteAllBytesAsync(filePath, imageData);
                Console.WriteLine("✓");
                downloaded++;

                // Small delay to avoid rate limiting
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("==========================");
        Console.WriteLine($"Download complete!");
        Console.WriteLine($"  Downloaded: {downloaded}");
        Console.WriteLine($"  Skipped: {skipped}");
        Console.WriteLine($"  Failed: {failed}");
        Console.WriteLine($"Total: {items.Count}");
    }

    private static List<GistItem> LoadLocalData()
    {
        var items = new List<GistItem>();
        
        if (!File.Exists(LocalDataFile))
        {
            Console.WriteLine($"⚠ Local data file not found: {LocalDataFile}");
            return items;
        }

        try
        {
            var json = File.ReadAllText(LocalDataFile);
            items = JsonSerializer.Deserialize<List<GistItem>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<GistItem>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Error reading local data file: {ex.Message}");
        }

        return items;
    }

    private static async Task<List<GistItem>> DownloadGistData()
    {
        const string GistUrl = "https://gist.githubusercontent.com/Bonfire/9b803c4b7c18b20c1c49e0fa78bd400e/raw";
        var json = await HttpClient.GetStringAsync(GistUrl);
        var items = JsonSerializer.Deserialize<List<GistItem>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return items ?? new List<GistItem>();
    }

    private static Dictionary<string, string> LoadItemMappings()
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Try to load from rust-item-list.json
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rust-item-list.json"),
            Path.Combine("..", "..", "rustplus-desktop-3.0.1", "rustplus-desktop-3.0.1", "RustPlusDesktop", "rust-item-list.json"),
            Path.Combine("..", "..", "..", "rustplus-desktop-3.0.1", "rustplus-desktop-3.0.1", "RustPlusDesktop", "rust-item-list.json"),
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

    private static string? GetShortName(string displayName, Dictionary<string, string> mappings)
    {
        if (mappings.TryGetValue(displayName, out var shortName))
        {
            return shortName;
        }

        // Try case-insensitive match
        var match = mappings.FirstOrDefault(kvp => 
            string.Equals(kvp.Key, displayName, StringComparison.OrdinalIgnoreCase));
        
        return match.Value;
    }

    private static string? InferShortName(string displayName)
    {
        // Try to extract short name from the image URL
        // rustlabs.com URLs often contain the short name in the path
        // e.g., https://rustlabs.com/img/items180/rifle.ak.png -> rifle.ak
        return null; // We'll extract from URL in the main loop instead
    }
}

class GistItem
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
}






