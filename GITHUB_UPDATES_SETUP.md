# GitHub Releases Auto-Update Setup

This guide explains how to set up automatic updates using GitHub Releases.

## How It Works

The app automatically checks GitHub Releases API for new versions and allows users to download and install updates seamlessly.

## Setup Instructions

### 1. Configure GitHub Repository

1. Open `src/RustDesktop.App/App.xaml.cs`
2. Find the update check code (around line 255)
3. Replace the placeholders:
   ```csharp
   var githubOwner = "YOUR_GITHUB_USERNAME"; // Your GitHub username
   var githubRepo = "YOUR_REPO_NAME";        // Your repository name
   ```

### 2. Create a GitHub Release

When you want to release an update:

1. **Update Version Number**
   - Edit `src/RustDesktop.App/RustDesktop.App.csproj`
   - Update the `<Version>` tag (e.g., `1.0.1`)
   - Update `<AssemblyVersion>` and `<FileVersion>` to match

2. **Build and Package**
   ```powershell
   powershell -ExecutionPolicy Bypass -File publish.ps1
   ```
   This creates `release/RustDesktop-v1.0.1-win-x64.zip`

3. **Create GitHub Release**
   - Go to your GitHub repository
   - Click "Releases" → "Create a new release"
   - Tag version: `v1.0.1` (must match version in .csproj, with 'v' prefix)
   - Release title: `Rust Desktop v1.0.1`
   - Release notes: Add your changelog
   - Attach the ZIP file: `RustDesktop-v1.0.1-win-x64.zip`
   - Click "Publish release"

### 3. Version Format

- **GitHub Tag**: Must start with 'v' (e.g., `v1.0.1`)
- **Project File**: No 'v' prefix (e.g., `1.0.1`)
- **ZIP Filename**: Should match version (e.g., `RustDesktop-v1.0.1-win-x64.zip`)

The app automatically strips the 'v' prefix when comparing versions.

## How Users Get Updates

1. **Automatic Check**: On app startup, it checks GitHub Releases API
2. **Update Notification**: If a newer version is found, user is prompted
3. **Download**: User can choose to download the update
4. **Installation**: Update is installed automatically on next app restart

## GitHub API Endpoint

The app uses:
```
GET https://api.github.com/repos/{owner}/{repo}/releases/latest
```

This returns the latest non-prerelease version.

## Release Asset Requirements

- **Filename**: Must contain `win-x64` and end with `.zip`
- **Example**: `RustDesktop-v1.0.1-win-x64.zip`
- **Format**: Standard ZIP file created by `publish.ps1`

## Example Release Workflow

```bash
# 1. Update version in .csproj
# 2. Build and package
powershell -ExecutionPolicy Bypass -File publish.ps1

# 3. Create GitHub release via web UI or GitHub CLI:
gh release create v1.0.1 release/RustDesktop-v1.0.1-win-x64.zip \
  --title "Rust Desktop v1.0.1" \
  --notes "Bug fixes and improvements"
```

## Testing

1. Create a test release with version `v1.0.2` (higher than current)
2. Upload the ZIP file
3. Run the app - it should detect the update
4. Download and restart to apply

## Complete Package Contents

The ZIP file created by `publish.ps1` includes **EVERYTHING** needed:

✅ **RustDesktop.exe** - Self-contained .NET application (no .NET installation needed)
✅ **Node.js v24.12.0** - Complete runtime in `runtime/node-win-x64/` (~50 MB)
✅ **rustplus-cli.zip** - npm package bundle (~25 MB, auto-extracts on first use)
✅ **Icons/** - All Rust item icons (1400+ PNG files)
✅ **PICS/** - Shop marker images
✅ **rust-item-list.json** - Item definitions
✅ **README.txt** - User guide
✅ **LICENSE** - License file
✅ **All .NET runtime files** - Self-contained, no installation needed

**Total Package Size**: ~150 MB (compressed ZIP)

## Uploading to GitHub Release

1. **Build the package:**
   ```powershell
   powershell -ExecutionPolicy Bypass -File publish.ps1
   ```

2. **Verify the ZIP exists:**
   - Location: `release/RustDesktop-v{version}-win-x64.zip`
   - Check size: Should be ~150 MB

3. **Create GitHub Release:**
   - Go to your GitHub repository
   - Click "Releases" → "Create a new release"
   - Tag: `v{version}` (e.g., `v1.0.0`)
   - Title: `Rust Desktop v{version}`
   - Description: Add release notes
   - **Drag and drop** the ZIP file: `release/RustDesktop-v{version}-win-x64.zip`
   - Click "Publish release"

4. **Users download ONE file:**
   - Users click "Download" on the GitHub release
   - They get the ZIP file
   - Extract and run `RustDesktop.exe`
   - **Everything works - no additional downloads needed!**

## Notes

- Prereleases are automatically skipped
- Only the latest release is checked
- Updates are optional (user can decline)
- Installation happens on next restart (not immediately)
- **The ZIP contains everything - users don't need to install anything else**
