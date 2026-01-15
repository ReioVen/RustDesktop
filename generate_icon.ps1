# PowerShell script to generate a simple Rust-themed application icon
# This creates a basic multi-resolution ICO file

$ErrorActionPreference = "Stop"

# Create Icons directory if it doesn't exist
$iconsDir = "src\RustDesktop.App\Icons"
if (-not (Test-Path $iconsDir)) {
    New-Item -ItemType Directory -Path $iconsDir -Force | Out-Null
}

$iconPath = Join-Path $iconsDir "app.ico"

Write-Host "Generating Rust Desktop icon..." -ForegroundColor Cyan

try {
    # Create a simple icon using .NET
    Add-Type -AssemblyName System.Drawing
    
    # Create icon with multiple sizes
    $sizes = @(16, 32, 48, 64, 128, 256)
    $bitmaps = New-Object System.Collections.ArrayList
    
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
        
        # Draw background (rust/orange color)
        $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 100, 50))
        $graphics.FillEllipse($bgBrush, 2, 2, $size - 4, $size - 4)
        
        # Draw inner circle (darker)
        $innerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 70, 30))
        $graphics.FillEllipse($innerBrush, $size * 0.2, $size * 0.2, $size * 0.6, $size * 0.6)
        
        # Draw "R" letter for Rust
        if ($size -ge 32) {
            $font = New-Object System.Drawing.Font("Arial", $size * 0.4, [System.Drawing.FontStyle]::Bold)
            $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
            $format = New-Object System.Drawing.StringFormat
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
            $graphics.DrawString("R", $font, $textBrush, $size / 2, $size / 2, $format)
        }
        
        $graphics.Dispose()
        [void]$bitmaps.Add($bitmap)
    }
    
    # Save as ICO file - use Icon.Save method if available, otherwise create manually
    # For simplicity, save the largest bitmap as a temporary PNG and convert
    $largestBitmap = $bitmaps[$bitmaps.Count - 1]
    
    # Create icon from bitmaps using Icon constructor
    $iconStream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($iconStream)
    
    # ICO header
    $writer.Write([UInt16]0)  # Reserved
    $writer.Write([UInt16]1)  # Type (1 = ICO)
    $writer.Write([UInt16]$bitmaps.Count)  # Number of images
    
    $offset = 6 + ($bitmaps.Count * 16)  # Header + directory entries
    $imageData = New-Object System.Collections.ArrayList
    
    # Write directory entries and collect image data
    foreach ($bitmap in $bitmaps) {
        $width = [Math]::Min($bitmap.Width, 255)
        $height = [Math]::Min($bitmap.Height, 255)
        $writer.Write([Byte]$width)
        $writer.Write([Byte]$height)
        $writer.Write([Byte]0)  # Color palette
        $writer.Write([Byte]0)  # Reserved
        
        # Convert bitmap to PNG bytes for ICO
        $pngStream = New-Object System.IO.MemoryStream
        $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
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
    [System.IO.File]::WriteAllBytes($iconPath, $iconStream.ToArray())
    $writer.Dispose()
    $iconStream.Dispose()
    
    # Cleanup
    foreach ($bitmap in $bitmaps) {
        $bitmap.Dispose()
    }
    
    Write-Host "Icon created successfully at: $iconPath" -ForegroundColor Green
    Write-Host "The icon will be used for the application and system tray." -ForegroundColor Gray
}
catch {
    Write-Host "Error creating icon: $_" -ForegroundColor Red
    Write-Host "You can manually add an icon file at: $iconPath" -ForegroundColor Yellow
    Write-Host "Or use an online converter to convert a PNG to ICO format." -ForegroundColor Yellow
}




