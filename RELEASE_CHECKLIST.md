# Release Checklist

Use this checklist before uploading to GitHub to ensure everything is included.

## Pre-Release Verification

### 1. Build the Package
```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

### 2. Verify Package Contents

Check that `release/RustDesktop-v{version}/` contains:

- [ ] `RustDesktop.exe` (main application)
- [ ] `runtime/node-win-x64/node.exe` (Node.js runtime)
- [ ] `runtime/rustplus-cli.zip` (~25 MB)
- [ ] `Icons/` folder with 1400+ PNG files
- [ ] `PICS/` folder with shop images
- [ ] `rust-item-list.json`
- [ ] `README.txt`
- [ ] `LICENSE`
- [ ] All .NET runtime DLLs (automatically included)

### 3. Verify ZIP File

- [ ] ZIP file exists: `release/RustDesktop-v{version}-win-x64.zip`
- [ ] ZIP size: ~150 MB (compressed)
- [ ] ZIP can be extracted without errors

### 4. Test Package (Optional but Recommended)

1. Extract ZIP to a new folder
2. Run `RustDesktop.exe`
3. Verify:
   - [ ] App starts without errors
   - [ ] Icons load correctly
   - [ ] No missing file errors in logs

## GitHub Release Steps

### 1. Update Version
- [ ] Update version in `src/RustDesktop.App/RustDesktop.App.csproj`
- [ ] Version format: `1.0.0` (no 'v' prefix in .csproj)

### 2. Build and Package
- [ ] Run `publish.ps1`
- [ ] Verify ZIP was created successfully

### 3. Create GitHub Release
- [ ] Go to GitHub repository → Releases → "Create a new release"
- [ ] Tag version: `v{version}` (e.g., `v1.0.0` - **must have 'v' prefix**)
- [ ] Release title: `Rust Desktop v{version}`
- [ ] Release notes: Add changelog
- [ ] Upload ZIP: Drag `release/RustDesktop-v{version}-win-x64.zip`
- [ ] Click "Publish release"

### 4. Verify Release
- [ ] Release appears on GitHub
- [ ] ZIP file is attached and downloadable
- [ ] Release notes are correct
- [ ] Tag matches version (with 'v' prefix)

## What Users Get

When users download from GitHub:

1. **ONE file**: `RustDesktop-v{version}-win-x64.zip`
2. **Extract**: Unzip to any folder
3. **Run**: Double-click `RustDesktop.exe`
4. **Everything works**: No additional downloads or installations needed!

## Package Contents Summary

```
RustDesktop-v{version}/
├── RustDesktop.exe              # Main app (self-contained)
├── runtime/
│   ├── node-win-x64/            # Node.js runtime (~50 MB)
│   │   ├── node.exe
│   │   ├── npm.cmd
│   │   └── [all Node.js files]
│   └── rustplus-cli.zip         # npm package (~25 MB)
├── Icons/                       # 1400+ item icons
├── PICS/                        # Shop images
├── rust-item-list.json          # Item definitions
├── README.txt                   # User guide
├── LICENSE                      # License
└── [.NET runtime DLLs]          # Self-contained runtime
```

**Total**: Everything needed in ONE ZIP file!
