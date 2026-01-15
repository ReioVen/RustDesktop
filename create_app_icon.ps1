# PowerShell script to create app.ico from C4 icon for application icon
# This will be used for taskbar, window title bar, and tabs

$ErrorActionPreference = "Stop"

$iconsDir = "src\RustDesktop.App\Icons"
$c4PngPath = Join-Path $iconsDir "explosive.timed.png"
$appIcoPath = Join-Path $iconsDir "app.ico"

Write-Host "Creating app.ico from C4 icon..." -ForegroundColor Cyan

if (-not (Test-Path $c4PngPath)) {
    Write-Host "Error: C4 icon not found at: $c4PngPath" -ForegroundColor Red
    Write-Host "Please run download_c4_icon.ps1 first." -ForegroundColor Yellow
    exit 1
}

try {
    Add-Type -AssemblyName System.Drawing
    
    # Load the C4 PNG
    $bitmap = New-Object System.Drawing.Bitmap($c4PngPath)
    
    # Create icon with multiple sizes for best quality
    $sizes = @(16, 32, 48, 64, 128, 256)
    $iconBitmaps = New-Object System.Collections.ArrayList
    
    foreach ($size in $sizes) {
        $resized = New-Object System.Drawing.Bitmap($bitmap, $size, $size)
        [void]$iconBitmaps.Add($resized)
    }
    
    # Create ICO file
    $icoStream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($icoStream)
    
    # ICO header
    $writer.Write([UInt16]0)  # Reserved
    $writer.Write([UInt16]1)  # Type (1 = ICO)
    $writer.Write([UInt16]$iconBitmaps.Count)  # Number of images
    
    $offset = 6 + ($iconBitmaps.Count * 16)  # Header + directory entries
    $imageData = New-Object System.Collections.ArrayList
    
    # Write directory entries
    foreach ($iconBitmap in $iconBitmaps) {
        $width = [Math]::Min($iconBitmap.Width, 255)
        $height = [Math]::Min($iconBitmap.Height, 255)
        $writer.Write([Byte]$width)
        $writer.Write([Byte]$height)
        $writer.Write([Byte]0)  # Color palette
        $writer.Write([Byte]0)  # Reserved
        
        # Convert bitmap to PNG bytes for ICO
        $pngStream = New-Object System.IO.MemoryStream
        $iconBitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $pngStream.ToArray()
        $pngStream.Dispose()
        
        $writer.Write([UInt16]1)  # Planes
        $writer.Write([UInt16]32)  # Bits per pixel
        $writer.Write([UInt32][int]$pngBytes.Length)  # Size
        $writer.Write([UInt32][int]$offset)  # Offset
        
        $offset += $pngBytes.Length
        [void]$imageData.Add($pngBytes)
    }
    
    # Write image data
    foreach ($pngBytes in $imageData) {
        $writer.Write($pngBytes)
    }
    
    # Save to file
    [System.IO.File]::WriteAllBytes($appIcoPath, $icoStream.ToArray())
    $writer.Dispose()
    $icoStream.Dispose()
    
    # Cleanup
    foreach ($iconBitmap in $iconBitmaps) {
        $iconBitmap.Dispose()
    }
    $bitmap.Dispose()
    
    Write-Host "app.ico created successfully!" -ForegroundColor Green
    Write-Host "  Location: $appIcoPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "The C4 icon will now be used for:" -ForegroundColor Green
    Write-Host "  - Taskbar icon" -ForegroundColor Gray
    Write-Host "  - Window title bar" -ForegroundColor Gray
    Write-Host "  - System tray (already configured)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Rebuild the project to see the changes." -ForegroundColor Yellow
}
catch {
    Write-Host "Error creating icon: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "You can manually convert the PNG to ICO using:" -ForegroundColor Yellow
    Write-Host "  https://convertio.co/png-ico/" -ForegroundColor Cyan
    Write-Host "  https://www.icoconverter.com/" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Save it as: $appIcoPath" -ForegroundColor Yellow
    exit 1
}




