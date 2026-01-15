using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;

namespace RustDesktop.Tools;

public static class ValidateIcons
{
    public static void Run(string[] args)
    {
        Console.WriteLine("Icon File Validator");
        Console.WriteLine("==================");
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

        var iconFiles = Directory.GetFiles(iconsFolder, "*.png", SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Found {iconFiles.Length} icon files");
        Console.WriteLine();

        int valid = 0;
        int invalid = 0;
        int corrupted = 0;
        var invalidFiles = new System.Collections.Generic.List<string>();
        var corruptedFiles = new System.Collections.Generic.List<string>();

        foreach (var iconFile in iconFiles)
        {
            try
            {
                var fileName = Path.GetFileName(iconFile);
                
                // Check file size
                var fileInfo = new FileInfo(iconFile);
                if (fileInfo.Length == 0)
                {
                    corrupted++;
                    corruptedFiles.Add($"{fileName} - empty file");
                    continue;
                }

                // Try to load as image to validate it's a valid PNG
                try
                {
                    using (var img = Image.FromFile(iconFile))
                    {
                        // Verify it's actually a PNG
                        if (img.RawFormat != ImageFormat.Png && img.RawFormat != ImageFormat.MemoryBmp)
                        {
                            // Sometimes valid PNGs load as MemoryBmp, so check the file header
                            var header = new byte[8];
                            using (var fs = new FileStream(iconFile, FileMode.Open, FileAccess.Read))
                            {
                                fs.Read(header, 0, 8);
                            }
                            
                            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
                            if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
                            {
                                invalid++;
                                invalidFiles.Add($"{fileName} - not a valid PNG (header: {BitConverter.ToString(header)})");
                                continue;
                            }
                        }
                        
                        valid++;
                    }
                }
                catch (Exception ex)
                {
                    corrupted++;
                    corruptedFiles.Add($"{fileName} - {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                invalid++;
                invalidFiles.Add($"{Path.GetFileName(iconFile)} - {ex.Message}");
            }
        }

        Console.WriteLine("Validation Results:");
        Console.WriteLine($"  ✓ Valid: {valid}");
        Console.WriteLine($"  ✗ Invalid: {invalid}");
        Console.WriteLine($"  ⚠ Corrupted: {corrupted}");
        Console.WriteLine();

        if (invalidFiles.Count > 0)
        {
            Console.WriteLine("Invalid Files:");
            Console.WriteLine("--------------");
            foreach (var file in invalidFiles.Take(20))
            {
                Console.WriteLine($"  - {file}");
            }
            if (invalidFiles.Count > 20)
            {
                Console.WriteLine($"  ... and {invalidFiles.Count - 20} more");
            }
            Console.WriteLine();
        }

        if (corruptedFiles.Count > 0)
        {
            Console.WriteLine("Corrupted Files:");
            Console.WriteLine("----------------");
            foreach (var file in corruptedFiles.Take(20))
            {
                Console.WriteLine($"  - {file}");
            }
            if (corruptedFiles.Count > 20)
            {
                Console.WriteLine($"  ... and {corruptedFiles.Count - 20} more");
            }
            Console.WriteLine();
        }

        // Check for problematic file names
        Console.WriteLine("Checking for problematic file names...");
        var problematicNames = new System.Collections.Generic.List<string>();
        
        foreach (var iconFile in iconFiles)
        {
            var fileName = Path.GetFileName(iconFile);
            
            // Check for URL-encoded characters
            if (fileName.Contains("%"))
            {
                problematicNames.Add($"{fileName} - contains URL encoding");
            }
            
            // Check for spaces
            if (fileName.Contains(" "))
            {
                problematicNames.Add($"{fileName} - contains spaces");
            }
        }

        if (problematicNames.Count > 0)
        {
            Console.WriteLine($"Found {problematicNames.Count} files with problematic names:");
            foreach (var name in problematicNames)
            {
                Console.WriteLine($"  ⚠ {name}");
            }
            
            // Fix problematic names
            Console.WriteLine();
            Console.Write("Fix problematic file names? (y/n): ");
            var response = Console.ReadLine();
            if (response?.Trim().ToLower() == "y")
            {
                int fixedCount = 0;
                foreach (var iconFile in iconFiles)
                {
                    var fileName = Path.GetFileName(iconFile);
                    var newFileName = fileName;
                    bool needsRename = false;
                    
                    // Fix URL encoding
                    if (fileName.Contains("%"))
                    {
                        newFileName = WebUtility.UrlDecode(fileName);
                        needsRename = true;
                    }
                    
                    // Fix spaces
                    if (newFileName.Contains(" "))
                    {
                        newFileName = newFileName.Replace(" ", "_");
                        needsRename = true;
                    }
                    
                    if (needsRename && newFileName != fileName)
                    {
                        try
                        {
                            var newPath = Path.Combine(iconsFolder, newFileName);
                            if (!File.Exists(newPath))
                            {
                                File.Move(iconFile, newPath);
                                Console.WriteLine($"  ✓ Renamed: {fileName} -> {newFileName}");
                                fixedCount++;
                            }
                            else
                            {
                                Console.WriteLine($"  ⊘ Skipped: {fileName} (target exists)");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ✗ Failed to rename {fileName}: {ex.Message}");
                        }
                    }
                }
                Console.WriteLine($"Fixed {fixedCount} file names");
            }
        }
        else
        {
            Console.WriteLine("✓ No problematic file names found");
        }

        Console.WriteLine();
        Console.WriteLine("==================");
        
        if (invalid == 0 && corrupted == 0 && problematicNames.Count == 0)
        {
            Console.WriteLine("✓ All icons are valid!");
        }
        else
        {
            Console.WriteLine($"⚠ Found {invalid} invalid, {corrupted} corrupted files, and {problematicNames.Count} problematic names");
            Console.WriteLine();
            Console.WriteLine("Recommendation: Remove corrupted/invalid files to prevent BitmapImage errors");
        }
    }
}












