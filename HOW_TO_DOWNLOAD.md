# How Users Download and Use the App

## ❌ DO NOT Clone the Repository

Users should **NOT** download the whole repository or clone it. They only need **ONE ZIP file**.

## ✅ What Users Should Do

### Step 1: Go to GitHub Releases
Visit: https://github.com/ReioVen/RustDesktop/releases

### Step 2: Download the ZIP File
- Look for the latest release (e.g., "Rust Desktop v1.0.0")
- Download **ONLY** this file: `RustDesktop-v1.0.0-win-x64.zip`
- **DO NOT** click "Source code (zip)" - that's the wrong file!

### Step 3: Extract the ZIP
- Right-click the downloaded ZIP file
- Select "Extract All..." or use 7-Zip/WinRAR
- Extract to any folder (e.g., `C:\RustDesktop\`)

### Step 4: Run the App
- Open the extracted folder
- Double-click **`RustDesktop.exe`**
- That's it! Everything is included.

## What's Inside the ZIP

The ZIP file contains **EVERYTHING** needed:
- ✅ RustDesktop.exe (main app)
- ✅ Node.js runtime (no installation needed)
- ✅ rustplus-cli package (auto-extracts)
- ✅ All icons and images
- ✅ All configuration files

**Users do NOT need to:**
- ❌ Install .NET
- ❌ Install Node.js
- ❌ Clone the repository
- ❌ Build anything
- ❌ Download anything else

## For Developers: How to Upload the ZIP

If you're the developer and need to upload the ZIP:

1. **Build the package** (if not done):
   ```powershell
   powershell -ExecutionPolicy Bypass -File publish.ps1
   ```

2. **Go to GitHub Releases**:
   - Visit: https://github.com/ReioVen/RustDesktop/releases/new

3. **Create Release**:
   - Tag: `v1.0.0`
   - Title: `Rust Desktop v1.0.0`
   - Upload: `release/RustDesktop-v1.0-win-x64.zip`
   - Click "Publish release"

4. **Done!** Users can now download the ZIP from the release page.

## File Size

- **ZIP file**: ~147 MB (compressed)
- **Extracted**: ~200 MB
- **Everything included**: No additional downloads needed!

## Troubleshooting

**"I don't see a ZIP file on GitHub"**
- The developer needs to create a Release and upload it first
- Check: https://github.com/ReioVen/RustDesktop/releases

**"I see 'Source code (zip)' but not the app ZIP"**
- That's the wrong file! Wait for the developer to create a Release
- The app ZIP will be in the "Assets" section of a Release

**"Can I just clone the repo?"**
- No! The repo is source code, not a ready-to-run app
- You need the compiled ZIP from a Release
