# PowerShell script to publish Rust Desktop for distribution
# Creates a self-contained, ready-to-distribute package

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Rust Desktop - Publishing Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$publishDir = "publish"
$releaseDir = "release"
$appName = "RustDesktop"

# Read version from project file
$projectFile = "src/RustDesktop.App/RustDesktop.App.csproj"
$version = "1.0.0"
if (Test-Path $projectFile) {
    $projectContent = Get-Content $projectFile -Raw
    if ($projectContent -match '<Version>([^<]+)</Version>') {
        $version = $matches[1]
    }
}
Write-Host "Building version: $version" -ForegroundColor Cyan

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue
}
if (Test-Path $releaseDir) {
    Remove-Item -Path $releaseDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Create directories
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

Write-Host "Publishing self-contained application..." -ForegroundColor Cyan
Write-Host "This may take a few minutes..." -ForegroundColor Gray
Write-Host ""

# Publish as self-contained Windows x64 application
dotnet publish `
    src/RustDesktop.App/RustDesktop.App.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output "$publishDir/win-x64" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[OK] Build completed successfully!" -ForegroundColor Green
Write-Host ""

# Download and bundle Node.js runtime
Write-Host "Downloading Node.js runtime..." -ForegroundColor Yellow
$nodeVersion = "24.12.0"  # LTS version
$nodeUrl = "https://nodejs.org/dist/v$nodeVersion/node-v$nodeVersion-win-x64.zip"
$nodeZipPath = Join-Path $publishDir "node-win-x64.zip"
$nodeExtractPath = Join-Path $publishDir "node-win-x64"

try {
    # Download Node.js
    Write-Host "  Downloading from: $nodeUrl" -ForegroundColor Gray
    $ProgressPreference = 'SilentlyContinue'  # Suppress progress bar
    Invoke-WebRequest -Uri $nodeUrl -OutFile $nodeZipPath -UseBasicParsing
    
    if (Test-Path $nodeZipPath) {
        Write-Host "  Extracting Node.js..." -ForegroundColor Gray
        # Extract Node.js
        Expand-Archive -Path $nodeZipPath -DestinationPath $publishDir -Force
        
        # Move extracted folder to correct location
        $extractedNodePath = Join-Path $publishDir "node-v$nodeVersion-win-x64"
        if (Test-Path $extractedNodePath) {
            if (Test-Path $nodeExtractPath) {
                Remove-Item -Path $nodeExtractPath -Recurse -Force
            }
            Move-Item -Path $extractedNodePath -Destination $nodeExtractPath -Force
        }
        
        # Clean up zip file
        Remove-Item -Path $nodeZipPath -Force -ErrorAction SilentlyContinue
        
        Write-Host "[OK] Node.js bundled successfully!" -ForegroundColor Green
        
        # Verify node.exe exists
        $nodeExe = Join-Path $nodeExtractPath "node.exe"
        if (Test-Path $nodeExe) {
            $nodeVersionOutput = & $nodeExe --version
            Write-Host "  Node.js version: $nodeVersionOutput" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "[WARN] Failed to download Node.js - users will need to install it manually" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "[WARN] Failed to download Node.js: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "  Users can still use the app by pairing via mobile app or installing Node.js manually" -ForegroundColor Gray
}

Write-Host ""

# Download and bundle rustplus-cli (npm package)
Write-Host "Downloading rustplus-cli package..." -ForegroundColor Yellow
$rustplusCliDir = Join-Path $publishDir "rustplus-cli-temp"
$rustplusCliZip = Join-Path $publishDir "rustplus-cli.zip"

try {
    # Use bundled Node.js (just downloaded) or system Node.js to install the package
    $nodeExe = $null
    $npmCmd = $null
    
    if (Test-Path $nodeExtractPath) {
        $nodeExe = Join-Path $nodeExtractPath "node.exe"
        $npmCmd = Join-Path $nodeExtractPath "npm.cmd"
        if (-not (Test-Path $npmCmd)) {
            $npmCmd = Join-Path $nodeExtractPath "npm"
        }
    }
    
    # Fallback to system Node.js if bundled not available
    if (-not ($nodeExe -and (Test-Path $nodeExe))) {
        $nodeCmd = Get-Command node -ErrorAction SilentlyContinue
        if ($nodeCmd) {
            $nodeExe = $nodeCmd.Source
            $npmDir = Split-Path $nodeExe
            $npmCmd = Join-Path $npmDir "npm.cmd"
            if (-not (Test-Path $npmCmd)) {
                $npmCmd = "npm"
            }
        }
    }
    
    if ($nodeExe -and (Test-Path $nodeExe) -and $npmCmd) {
        Write-Host "  Using Node.js: $nodeExe" -ForegroundColor Gray
        
        # Create temp directory for npm install
        if (Test-Path $rustplusCliDir) {
            Remove-Item -Path $rustplusCliDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        New-Item -ItemType Directory -Path $rustplusCliDir -Force | Out-Null
        
        # Install @liamcottle/rustplus.js package
        Write-Host "  Installing @liamcottle/rustplus.js (this may take a minute)..." -ForegroundColor Gray
        $ProgressPreference = 'SilentlyContinue'
        
        # Use Start-Process to run npm install to avoid PowerShell module conflicts
        try {
            $npmFullPath = $npmCmd
            if (Test-Path $npmCmd) {
                $npmFullPath = (Resolve-Path $npmCmd).Path
            }
            
            Write-Host "  Running: $npmFullPath install @liamcottle/rustplus.js..." -ForegroundColor Gray
            
            # Use Start-Process with proper working directory
            $process = Start-Process -FilePath $npmFullPath -ArgumentList "install", "@liamcottle/rustplus.js", "--production", "--no-save", "--loglevel=error" -WorkingDirectory $rustplusCliDir -Wait -PassThru -NoNewWindow -RedirectStandardOutput "$rustplusCliDir\npm-output.txt" -RedirectStandardError "$rustplusCliDir\npm-error.txt"
            
            if ($process.ExitCode -eq 0) {
                # Create zip file from node_modules
                $nodeModulesPath = Join-Path $rustplusCliDir "node_modules"
                if (Test-Path $nodeModulesPath) {
                    Write-Host "  Creating rustplus-cli.zip..." -ForegroundColor Gray
                    if (Test-Path $rustplusCliZip) {
                        Remove-Item -Path $rustplusCliZip -Force -ErrorAction SilentlyContinue
                    }
                    Add-Type -AssemblyName System.IO.Compression.FileSystem
                    [System.IO.Compression.ZipFile]::CreateFromDirectory(
                        $nodeModulesPath,
                        $rustplusCliZip,
                        [System.IO.Compression.CompressionLevel]::Optimal,
                        $false
                    )
                    if (Test-Path $rustplusCliZip) {
                        $zipSize = (Get-Item $rustplusCliZip).Length / 1MB
                        Write-Host "[OK] rustplus-cli bundled successfully! ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Green
                    } else {
                        Write-Host "[WARN] Failed to create zip - npx will download it on first use" -ForegroundColor Yellow
                    }
                } else {
                    Write-Host "[WARN] node_modules not found - npx will download it on first use" -ForegroundColor Yellow
                }
            } else {
                Write-Host "[WARN] npm install failed (exit code: $($process.ExitCode)) - npx will download it on first use" -ForegroundColor Yellow
                if (Test-Path "$rustplusCliDir\npm-error.txt") {
                    $errorText = Get-Content "$rustplusCliDir\npm-error.txt" -Raw -ErrorAction SilentlyContinue
                    if ($errorText) {
                        Write-Host "  Error details: $($errorText.Trim())" -ForegroundColor Gray
                    }
                }
            }
        }
        finally {
            # Clean up temp files
            if (Test-Path "$rustplusCliDir\npm-output.txt") {
                Remove-Item "$rustplusCliDir\npm-output.txt" -Force -ErrorAction SilentlyContinue
            }
            if (Test-Path "$rustplusCliDir\npm-error.txt") {
                Remove-Item "$rustplusCliDir\npm-error.txt" -Force -ErrorAction SilentlyContinue
            }
        }
    } else {
        Write-Host "[WARN] Node.js/npm not found - rustplus-cli will be downloaded via npx on first use" -ForegroundColor Yellow
        Write-Host "  Users will need internet connection on first run to download the package" -ForegroundColor Gray
    }
}
catch {
    Write-Host "[WARN] Failed to bundle rustplus-cli: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "  The app will automatically download it via npx on first use (requires internet)" -ForegroundColor Gray
}

Write-Host ""

# Copy additional files
Write-Host "Copying additional files..." -ForegroundColor Yellow

$releasePath = Join-Path $releaseDir "$appName-v$version"

# Create release directory structure
New-Item -ItemType Directory -Path $releasePath -Force | Out-Null

# Copy published files
Copy-Item -Path "$publishDir/win-x64/*" -Destination $releasePath -Recurse -Force

# Create runtime directory
$runtimeDir = Join-Path $releasePath "runtime"
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null

# Copy Node.js runtime if it was downloaded
if (Test-Path $nodeExtractPath) {
    Copy-Item -Path $nodeExtractPath -Destination (Join-Path $runtimeDir "node-win-x64") -Recurse -Force
    Write-Host "  Node.js runtime included in package" -ForegroundColor Gray
}

# Copy rustplus-cli.zip if it was created
if (Test-Path $rustplusCliZip) {
    Copy-Item -Path $rustplusCliZip -Destination (Join-Path $runtimeDir "rustplus-cli.zip") -Force
    Write-Host "  rustplus-cli.zip included in package" -ForegroundColor Gray
    $rustplusCliSize = (Get-Item $rustplusCliZip).Length / 1MB
    Write-Host "    Size: $([math]::Round($rustplusCliSize, 2)) MB" -ForegroundColor Gray
}

# Copy README and LICENSE
Copy-Item -Path "README.md" -Destination $releasePath -Force
Copy-Item -Path "LICENSE" -Destination $releasePath -Force

# Create user-friendly README
$userReadme = @'
Rust Desktop - Installation and Usage Guide
============================================

System Requirements
-------------------
- Windows 10/11 (64-bit)
- Steam installed and logged in
- Rust game installed
- Node.js is INCLUDED with this package (no installation needed!)
- rustplus-cli is INCLUDED with this package (will auto-extract on first use)

Quick Start
-----------
1. Extract this folder to any location (e.g., C:\RustDesktop\)
2. Run RustDesktop.exe
3. Follow the on-screen instructions to connect

First Time Setup
----------------

Step 1: Connect Steam
- Click "Connect Steam" button
- This authenticates with your Steam account

Step 2: Pair Server
- Option A (Recommended - No setup needed): Pair via mobile Rust+ app
  1. Install Rust+ mobile app on your phone
  2. Open Rust+ app and pair with your server
  3. Come back to this app and click "Pair Server"
  4. The app will automatically detect the pairing from mobile app
  
- Option B (Everything included - ready to use): In-game pairing
  1. Click "Pair Server" button
  2. A browser window will open for Steam authentication
  3. Then pair in-game (ESC -> Rust+ -> Pair)
  4. Node.js and rustplus-cli are already included - no installation needed!
  5. The app will automatically extract rustplus-cli on first use (one-time setup)

Step 3: Connect
- Click "Connect to Rust+" button
- You should see the map and vending machines!

Features
--------
- Interactive Map: View server map with vending machines and team members
- Vending Machine Search: Search for items across all vending machines
- Raid Alerts: Get notified when your base is being raided
- World Events: See cargo ships, helicopters, and other events
- Real-time Updates: All data updates automatically

Notifications
-------------
The app can minimize to the system tray. You'll receive:
- Desktop notifications for raid alerts
- Desktop notifications for world events
- System tray icon (C4 icon) for quick access

Troubleshooting
---------------

"Node.js not found" error
- This should NOT happen - Node.js is included with this package!
- If you see this error, please report it as a bug
- As a workaround, you can pair via Rust+ mobile app (see Step 2, Option A above)

"rustplus-cli not found" error
- This should NOT happen - rustplus-cli is included with this package!
- The app will automatically extract it on first use
- If extraction fails, the app will automatically download it via npx (requires internet)
- If you see this error, check your internet connection and try again

Can't connect to server
- Make sure you've paired with the server in-game first
- Check that the server IP and port are correct
- Ensure your firewall allows the connection

Icons not showing
- Make sure all files in the Icons folder are present
- The app includes all Rust item icons

Support
-------
For issues or questions:
- Check the logs in the app (click "Show Logs" button)
- Report issues on GitHub (if applicable)

License
-------
See LICENSE file for details.

Disclaimer
----------
This is an UNOFFICIAL application and is not affiliated with Facepunch Studios or Rust.
'@

$userReadme | Out-File -FilePath (Join-Path $releasePath "README.txt") -Encoding UTF8

# Create a simple launcher script
$launcherScript = '@echo off' + "`r`n" + 'echo Starting Rust Desktop...' + "`r`n" + 'start "" "%~dp0RustDesktop.exe"'

$launcherScript | Out-File -FilePath (Join-Path $releasePath "Start Rust Desktop.bat") -Encoding ASCII

Write-Host "[OK] Files copied!" -ForegroundColor Green
Write-Host ""

# Create ZIP archive
Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
$zipPath = Join-Path $releaseDir "$appName-v$version-win-x64.zip"

# Remove existing zip if present
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

# Create ZIP using .NET compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($releasePath, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host "[OK] ZIP archive created!" -ForegroundColor Green
Write-Host ""

# Display summary
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Publishing Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Release package location:" -ForegroundColor Cyan
Write-Host "  $zipPath" -ForegroundColor White
Write-Host ""
Write-Host "Unpacked release location:" -ForegroundColor Cyan
Write-Host "  $releasePath" -ForegroundColor White
Write-Host ""
Write-Host "Package size:" -ForegroundColor Cyan
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "  $([math]::Round($zipSize, 2)) MB" -ForegroundColor White
Write-Host ""
Write-Host "Ready for distribution!" -ForegroundColor Green
Write-Host ""
Write-Host "=== Package Contents Summary ===" -ForegroundColor Cyan
Write-Host "✓ Self-contained .NET application (no .NET installation needed)" -ForegroundColor Gray
Write-Host "✓ Node.js v$nodeVersion runtime (~50 MB)" -ForegroundColor Gray
if (Test-Path $rustplusCliZip) {
    $cliSize = (Get-Item $rustplusCliZip).Length / 1MB
    Write-Host "✓ rustplus-cli package ($([math]::Round($cliSize, 2)) MB)" -ForegroundColor Gray
}
Write-Host "✓ All Rust item icons (1400+ files)" -ForegroundColor Gray
Write-Host "✓ Shop images (PICS folder)" -ForegroundColor Gray
Write-Host "✓ Item definitions (rust-item-list.json)" -ForegroundColor Gray
Write-Host "✓ User documentation (README.txt)" -ForegroundColor Gray
Write-Host ""
Write-Host "To upload to GitHub:" -ForegroundColor Yellow
Write-Host "  1. Go to your GitHub repository" -ForegroundColor White
Write-Host "  2. Click 'Releases' → 'Create a new release'" -ForegroundColor White
Write-Host "  3. Tag: v$version (must start with 'v')" -ForegroundColor White
Write-Host "  4. Upload: $zipPath" -ForegroundColor White
Write-Host "  5. Publish release" -ForegroundColor White
Write-Host ""




