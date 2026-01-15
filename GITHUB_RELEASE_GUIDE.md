# GitHub Release Guide - Complete Package

This guide ensures your package includes **EVERYTHING** users need in **ONE ZIP file**.

## ✅ What's Included in the ZIP

The `publish.ps1` script automatically bundles:

1. **RustDesktop.exe** (73 MB)
   - Self-contained .NET 8 application
   - No .NET installation required
   - All dependencies embedded

2. **Node.js Runtime** (~50 MB)
   - Location: `runtime/node-win-x64/`
   - Complete Node.js v24.12.0 LTS
   - Includes `node.exe`, `npm.cmd`, `npx.cmd`
   - All Node.js DLLs and modules

3. **rustplus-cli Package** (~25 MB)
   - Location: `runtime/rustplus-cli.zip`
   - Auto-extracts on first use
   - Contains `@liamcottle/rustplus.js` npm package
   - All dependencies included

4. **Icons Folder** (~10 MB)
   - 1400+ Rust item icons (PNG files)
   - All icons needed for the app

5. **PICS Folder**
   - ActiveShop.png
   - InactiveShop.png

6. **Configuration Files**
   - `rust-item-list.json` (item definitions)
   - `README.txt` (user guide)
   - `LICENSE` (MIT License)

7. **.NET Runtime Files** (embedded in exe)
   - All .NET 8 runtime DLLs
   - Self-contained, no installation needed

**Total Package Size**: ~150 MB (compressed ZIP)

## 📦 Creating a GitHub Release

### Step 1: Update Version

Edit `src/RustDesktop.App/RustDesktop.App.csproj`:
```xml
<Version>1.0.1</Version>
<AssemblyVersion>1.0.1.0</AssemblyVersion>
<FileVersion>1.0.1.0</FileVersion>
```

### Step 2: Build Package

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

This creates:
- `release/RustDesktop-v1.0.1/` (unpacked folder)
- `release/RustDesktop-v1.0.1-win-x64.zip` (ZIP file for GitHub)

### Step 3: Verify Package

Check that the ZIP contains:
- ✅ `RustDesktop.exe`
- ✅ `runtime/node-win-x64/node.exe`
- ✅ `runtime/rustplus-cli.zip`
- ✅ `Icons/` folder (1400+ files)
- ✅ `PICS/` folder
- ✅ `rust-item-list.json`
- ✅ `README.txt`
- ✅ `LICENSE`

### Step 4: Create GitHub Release

1. Go to your GitHub repository
2. Click **"Releases"** → **"Create a new release"**
3. **Tag version**: `v1.0.1` (must start with 'v', matches version in .csproj)
4. **Release title**: `Rust Desktop v1.0.1`
5. **Description**: Add your release notes/changelog
6. **Attach files**: Drag and drop `release/RustDesktop-v1.0.1-win-x64.zip`
7. Click **"Publish release"**

## 👥 User Experience

### What Users Do:

1. **Download ONE file** from GitHub Releases
   - File: `RustDesktop-v1.0.1-win-x64.zip`
   - Size: ~150 MB

2. **Extract the ZIP**
   - Extract to any folder (e.g., `C:\RustDesktop\`)
   - No installation needed

3. **Run the app**
   - Double-click `RustDesktop.exe`
   - Everything works immediately!

### What Happens on First Run:

1. App starts (no .NET installation needed)
2. rustplus-cli auto-extracts (one-time, ~2-3 seconds)
3. User connects Steam
4. User pairs server (Node.js already included!)
5. Everything works!

## 🔄 Auto-Update System

Once configured, users get automatic updates:

1. **On startup**: App checks GitHub Releases API
2. **If update available**: User is notified
3. **User downloads**: Update ZIP is downloaded
4. **On restart**: Update is automatically installed

### Configure Auto-Update

Edit `src/RustDesktop.App/App.xaml.cs` (around line 255):
```csharp
var githubOwner = "YOUR_GITHUB_USERNAME";
var githubRepo = "YOUR_REPO_NAME";
```

## ✅ Verification Checklist

Before uploading to GitHub, verify:

- [ ] Version updated in `.csproj`
- [ ] `publish.ps1` completed successfully
- [ ] ZIP file exists: `release/RustDesktop-v{version}-win-x64.zip`
- [ ] ZIP size: ~150 MB
- [ ] ZIP contains all required files (see Step 3 above)
- [ ] GitHub release tag matches version (with 'v' prefix)
- [ ] ZIP file attached to GitHub release

## 📝 Release Notes Template

```
## Rust Desktop v{version}

### New Features
- Feature 1
- Feature 2

### Bug Fixes
- Fixed issue 1
- Fixed issue 2

### Improvements
- Improvement 1
- Improvement 2
```

## 🎯 Summary

**ONE ZIP file contains EVERYTHING:**
- ✅ Application (self-contained)
- ✅ Node.js runtime
- ✅ rustplus-cli package
- ✅ All icons
- ✅ All dependencies
- ✅ Documentation

**Users download ONE file, extract, and run - that's it!**
