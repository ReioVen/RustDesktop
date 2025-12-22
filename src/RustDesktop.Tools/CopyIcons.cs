using System;
using System.IO;
using System.Linq;

namespace RustDesktop.Tools;

public static class CopyIcons
{
    private const string SourceFolder = @"C:\Programming\RustDesktop\icons";
    private const string TargetFolder = @"src\RustDesktop.App\Icons";

    public static void Run(string[] args)
    {
        Console.WriteLine("Rust Icon Copier & Renamer");
        Console.WriteLine("===========================");
        Console.WriteLine();

        var sourcePath = args.Length > 0 ? args[0] : SourceFolder;
        var targetPath = args.Length > 1 ? args[1] : Path.Combine("..", "..", TargetFolder);
        targetPath = Path.GetFullPath(targetPath);

        if (!Directory.Exists(sourcePath))
        {
            Console.WriteLine($"❌ Source folder not found: {sourcePath}");
            return;
        }

        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
            Console.WriteLine($"Created target folder: {targetPath}");
        }

        Console.WriteLine($"Source: {sourcePath}");
        Console.WriteLine($"Target: {targetPath}");
        Console.WriteLine();

        var iconFiles = Directory.GetFiles(sourcePath, "*.png", SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Found {iconFiles.Length} icon files");
        Console.WriteLine();

        int copied = 0;
        int skipped = 0;
        int failed = 0;

        foreach (var sourceFile in iconFiles)
        {
            try
            {
                var fileName = Path.GetFileName(sourceFile);
                var extension = Path.GetExtension(fileName);
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                
                // Convert dots to underscores in the name only: ammo.pistol.png -> ammo_pistol.png
                var targetFileName = nameWithoutExtension.Replace(".", "_") + extension;
                var targetFile = Path.Combine(targetPath, targetFileName);

                // Skip if already exists and is newer or same size
                if (File.Exists(targetFile))
                {
                    var sourceInfo = new FileInfo(sourceFile);
                    var targetInfo = new FileInfo(targetFile);
                    
                    if (targetInfo.Length == sourceInfo.Length && 
                        targetInfo.LastWriteTime >= sourceInfo.LastWriteTime)
                    {
                        skipped++;
                        continue;
                    }
                }

                File.Copy(sourceFile, targetFile, overwrite: true);
                copied++;
                
                if (copied % 50 == 0)
                {
                    Console.Write($"Copied {copied} files...\r");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to copy {Path.GetFileName(sourceFile)}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("===========================");
        Console.WriteLine($"Copy complete!");
        Console.WriteLine($"  Copied: {copied}");
        Console.WriteLine($"  Skipped: {skipped}");
        Console.WriteLine($"  Failed: {failed}");
        Console.WriteLine($"Total: {iconFiles.Length}");
    }
}







