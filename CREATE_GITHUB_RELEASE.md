# How to Create GitHub Release with Downloadable ZIP

## Quick Steps

1. **Build the package** (if not already done):
   ```powershell
   powershell -ExecutionPolicy Bypass -File publish.ps1
   ```

2. **Go to GitHub**:
   - Open your repository: https://github.com/ReioVen/RustDesktop
   - Click **"Releases"** (on the right side)
   - Click **"Create a new release"**

3. **Fill in release details**:
   - **Tag version**: `v1.0.0` (must start with 'v')
   - **Release title**: `Rust Desktop v1.0.0`
   - **Description**: Add your release notes
   - **Attach files**: Drag and drop `release/RustDesktop-v1.0.0-win-x64.zip`
   - Click **"Publish release"**

## What Users Will Download

Users will download **ONE file**: `RustDesktop-v1.0.0-win-x64.zip`

This ZIP contains **EVERYTHING**:
- ✅ RustDesktop.exe (self-contained app)
- ✅ Node.js runtime (complete, no installation needed)
- ✅ rustplus-cli package (auto-extracts on first use)
- ✅ All 1400+ Rust item icons
- ✅ Shop images
- ✅ Item definitions
- ✅ User guide (README.txt)
- ✅ License file

**Total size**: ~150 MB

## After Publishing

Once the release is published:
- Users can go to: `https://github.com/ReioVen/RustDesktop/releases`
- Click on the latest release
- Download `RustDesktop-v1.0.0-win-x64.zip`
- Extract and run `RustDesktop.exe`
- Everything works immediately!

## Auto-Update Configuration

After creating the release, update `src/RustDesktop.App/App.xaml.cs`:
```csharp
var githubOwner = "ReioVen";  // Your GitHub username
var githubRepo = "RustDesktop";  // Your repository name
```

Then users will get automatic update notifications when you publish new releases!
