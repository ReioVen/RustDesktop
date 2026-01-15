# Upload ZIP File to GitHub Release - Step by Step

## Current Status

✅ **ZIP file is ready**: `release/RustDesktop-v1.0-win-x64.zip` (146.75 MB)
✅ **Code is pushed to GitHub**: All source code is on GitHub
❌ **ZIP file NOT on GitHub yet**: You need to create a Release and upload it

## What's Inside the ZIP

The ZIP file (`RustDesktop-v1.0-win-x64.zip`) contains **EVERYTHING** users need:

```
RustDesktop-v1.0/
├── RustDesktop.exe              # Main app (73 MB, self-contained)
├── runtime/
│   ├── node-win-x64/            # Node.js v24.12.0 (~50 MB)
│   │   ├── node.exe
│   │   ├── npm.cmd
│   │   └── [all Node.js files]
│   └── rustplus-cli.zip         # npm package (~25 MB)
├── Icons/                       # 1400+ Rust item icons
├── PICS/                        # Shop marker images
├── rust-item-list.json          # Item definitions
├── README.txt                   # User guide
└── LICENSE                      # License file
```

**Total**: ~150 MB compressed - **ONE file with everything!**

## How to Upload to GitHub (5 Steps)

### Step 1: Go to Your Repository
Open: https://github.com/ReioVen/RustDesktop

### Step 2: Click "Releases"
- Look on the right side of the page
- Click **"Releases"** (or go to: https://github.com/ReioVen/RustDesktop/releases)

### Step 3: Create New Release
- Click **"Create a new release"** button (or "Draft a new release")

### Step 4: Fill in Release Details
- **Tag version**: Type `v1.0.0` (must start with 'v')
  - Click "Create new tag: v1.0.0 on publish"
- **Release title**: `Rust Desktop v1.0.0`
- **Description**: Add release notes, for example:
  ```
  ## Rust Desktop v1.0.0
  
  First release! Everything included in one ZIP file.
  
  ### Features
  - Interactive map with vending machines
  - Shop search and filtering
  - Raid alerts and world event notifications
  - Auto-update system
  - System tray integration
  
  ### Installation
  1. Download RustDesktop-v1.0.0-win-x64.zip
  2. Extract to any folder
  3. Run RustDesktop.exe
  4. Everything works - no installation needed!
  ```

### Step 5: Upload the ZIP File
- Scroll down to **"Attach binaries"** section
- **Drag and drop** the file: `C:\Programming\RustDesktop\release\RustDesktop-v1.0-win-x64.zip`
- OR click "selecting them" and browse to the file
- Click **"Publish release"**

## After Publishing

Users can now:
1. Go to: https://github.com/ReioVen/RustDesktop/releases
2. See the latest release
3. Download: `RustDesktop-v1.0.0-win-x64.zip`
4. Extract and run - everything works!

## Auto-Update

Once the release is published, the app will automatically:
- Check for updates on startup
- Notify users when new versions are available
- Allow users to download and install updates seamlessly

## Quick Command Reference

**Build package**:
```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

**ZIP location**:
```
C:\Programming\RustDesktop\release\RustDesktop-v1.0-win-x64.zip
```

**GitHub Release URL**:
```
https://github.com/ReioVen/RustDesktop/releases/new
```
