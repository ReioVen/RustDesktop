# PowerShell script: Publish Rust Desktop and create/update GitHub release
# 1. Runs publish.ps1 to build the package
# 2. Creates a GitHub release and uploads the ZIP (if gh CLI is installed)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Rust Desktop - Publish & GitHub Release" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

# Read version from project file
$projectFile = "src/RustDesktop.App/RustDesktop.App.csproj"
$version = "1.0.0"
if (Test-Path $projectFile) {
    $projectContent = Get-Content $projectFile -Raw
    if ($projectContent -match '<Version>([^<]+)</Version>') {
        $version = $matches[1]
    }
}

$tag = "v$version"
$zipName = "RustDesktop-v$version-win-x64.zip"
$zipPath = Join-Path $scriptDir "release\$zipName"

# --- Step 1: Run publish script ---
Write-Host "[Step 1/2] Running publish.ps1..." -ForegroundColor Yellow
Write-Host ""

& "$scriptDir\publish.ps1"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed! Aborting." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $zipPath)) {
    Write-Host "ZIP not found: $zipPath" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[Step 2/2] GitHub Release..." -ForegroundColor Yellow

# --- Step 2: GitHub release (gh CLI or manual) ---
$gh = Get-Command gh -ErrorAction SilentlyContinue
$repo = "ReioVen/RustDesktop"   # Change if your repo is different

if ($gh) {
    Write-Host "  Using GitHub CLI (gh) to create release." -ForegroundColor Gray
    Write-Host "  Tag: $tag | File: $zipName" -ForegroundColor Gray
    Write-Host ""

    # Check if tag already exists
    $tagExists = $false
    try {
        gh release view $tag --repo $repo 2>$null
        $tagExists = $LASTEXITCODE -eq 0
    } catch {}

    if ($tagExists) {
        Write-Host "  Release $tag already exists. Uploading asset only..." -ForegroundColor Gray
        gh release upload $tag $zipPath --repo $repo --clobber
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Asset uploaded to existing release: $tag" -ForegroundColor Green
        } else {
            Write-Host "[WARN] Upload failed. You can upload manually." -ForegroundColor Yellow
        }
    } else {
        $releaseNotes = @"
## Rust Desktop $tag

### Features
- Interactive map with vending machines
- Server selection when multiple servers detected
- Pair Server uses selected server; pairing data cleared on disconnect
- Raid alerts and world event notifications
- System tray and auto-update support

### Installation
1. Download **$zipName**
2. Extract to any folder
3. Run **RustDesktop.exe**
"@
        Write-Host "  Creating new release: $tag" -ForegroundColor Gray
        gh release create $tag $zipPath --repo $repo --title "Rust Desktop $tag" --notes $releaseNotes
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Release created: https://github.com/$repo/releases/tag/$tag" -ForegroundColor Green
        } else {
            Write-Host "[WARN] Release create failed. See instructions below for manual upload." -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "  GitHub CLI (gh) not found. Manual steps:" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  1. Open: https://github.com/$repo/releases/new" -ForegroundColor White
    Write-Host "  2. Tag: $tag" -ForegroundColor White
    Write-Host "  3. Title: Rust Desktop $tag" -ForegroundColor White
    Write-Host "  4. Attach: $zipPath" -ForegroundColor White
    Write-Host "  5. Publish release" -ForegroundColor White
    Write-Host ""
    Write-Host "  Or install gh: winget install GitHub.cli" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Done! Package: $zipPath" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
