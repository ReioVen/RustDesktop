# PowerShell script to download C4 icon for system tray
# Downloads the explosive.timed (C4) icon from Rust CDN

$ErrorActionPreference = "Stop"

# Create Icons directory if it doesn't exist
$iconsDir = "src\RustDesktop.App\Icons"
if (-not (Test-Path $iconsDir)) {
    New-Item -ItemType Directory -Path $iconsDir -Force | Out-Null
}

$iconUrl = "https://cdn.rusthelp.com/images/public/explosive.timed.png"
$iconPath = Join-Path $iconsDir "explosive.timed.png"
$iconPathAlt = Join-Path $iconsDir "explosive_timed.png"

Write-Host "Downloading C4 icon for system tray..." -ForegroundColor Cyan
Write-Host "URL: $iconUrl" -ForegroundColor Gray

try {
    # Download the icon
    Invoke-WebRequest -Uri $iconUrl -OutFile $iconPath -UseBasicParsing
    
    # Also create underscore version for compatibility
    Copy-Item -Path $iconPath -Destination $iconPathAlt -Force
    
    Write-Host "C4 icon downloaded successfully!" -ForegroundColor Green
    Write-Host "Saved to: $iconPath" -ForegroundColor Gray
    Write-Host "Also saved as: $iconPathAlt" -ForegroundColor Gray
    Write-Host ""
    Write-Host "The system tray will now use the C4 icon!" -ForegroundColor Green
    Write-Host "Rebuild and run the app to see it in the system tray." -ForegroundColor Yellow
}
catch {
    Write-Host "Error downloading icon: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "You can manually download the icon from:" -ForegroundColor Yellow
    Write-Host $iconUrl -ForegroundColor Cyan
    Write-Host "And save it to: $iconPath" -ForegroundColor Yellow
    exit 1
}




