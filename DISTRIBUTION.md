# Distribution Guide

This guide explains how to package and distribute Rust Desktop to end users.

## Quick Package

Run the publish script:

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

This will create:
- `release/RustDesktop-v1.0-win-x64.zip` - Ready-to-distribute ZIP file
- `release/RustDesktop-v1.0/` - Unpacked release folder

## What's Included

The package includes:
- ✅ Self-contained executable (no .NET runtime required)
- ✅ All Rust item icons (1400+ icons)
- ✅ Application icon (C4 icon)
- ✅ All dependencies bundled
- ✅ User-friendly README
- ✅ License file

## Distribution Methods

### Option 1: ZIP File (Recommended)
1. Run `publish.ps1`
2. Share `release/RustDesktop-v1.0-win-x64.zip`
3. Users extract and run `RustDesktop.exe`

### Option 2: Installer (Future)
You can create an MSI installer using:
- WiX Toolset
- Inno Setup
- Advanced Installer

### Option 3: Portable Version
The current package is already portable - users can extract and run from anywhere.

## System Requirements for End Users

- Windows 10/11 (64-bit)
- Steam installed
- Rust game installed
- Node.js (optional, for FCM pairing)

## File Structure

```
RustDesktop-v1.0/
├── RustDesktop.exe          # Main application
├── RustDesktop.dll          # Core library
├── Icons/                   # All Rust item icons
│   ├── *.png               # Item icons
│   └── app.ico             # Application icon
├── PICS/                    # Shop images
├── rust-item-list.json      # Item definitions
├── README.txt               # User guide
├── LICENSE                  # License file
└── [.NET runtime files]     # Self-contained runtime
```

## Testing Before Distribution

1. **Test on clean machine:**
   - Extract ZIP to a new folder
   - Run `RustDesktop.exe`
   - Verify all features work

2. **Check file sizes:**
   - Ensure all icons are included
   - Verify executable is not corrupted

3. **Test notifications:**
   - Minimize to system tray
   - Test raid alerts
   - Test world events

## Version Numbering

Update version in:
- `src/RustDesktop.App/RustDesktop.App.csproj` (AssemblyVersion)
- `publish.ps1` (version in paths)
- `README.md` (if applicable)

## Security Considerations

- Code signing (optional but recommended)
- Antivirus whitelisting may be needed
- Firewall rules for Node.js (if bundled)

## Updates

For future updates:
1. Update version number
2. Run `publish.ps1`
3. Create changelog
4. Distribute new ZIP

## Support Files

Include with distribution:
- README.txt (user guide)
- LICENSE (legal)
- CHANGELOG.md (if applicable)




