# Auto-Update API Example

This document explains how to set up auto-updates. The app now supports **GitHub Releases** (recommended) or custom API endpoints.

## GitHub Releases (Recommended)

See `GITHUB_UPDATES_SETUP.md` for the recommended GitHub Releases setup.

## Custom API Endpoint (Alternative)

If you prefer a custom endpoint instead of GitHub Releases:

## Update Check Endpoint

The app will check for updates by making a GET request to your update URL (configured in `App.xaml.cs`).

### Expected Response Format (JSON)

```json
{
  "version": "1.0.1",
  "downloadUrl": "https://yourwebsite.com/downloads/RustDesktop-v1.0.1-win-x64.zip",
  "releaseNotes": "Bug fixes and performance improvements\n- Fixed shop refresh issue\n- Improved update system",
  "isMandatory": false,
  "fileSize": 153600000
}
```

### Field Descriptions

- **version**: The new version number (e.g., "1.0.1")
- **downloadUrl**: Direct download link to the ZIP file
- **releaseNotes**: Release notes to show to users (supports newlines with `\n`)
- **isMandatory**: Whether the update is mandatory (currently not enforced, but reserved for future use)
- **fileSize**: Size of the update file in bytes (optional, for progress tracking)

## Example Implementation

### PHP Example

```php
<?php
// update-check.php
header('Content-Type: application/json');

$currentVersion = "1.0.0"; // Current version in the app
$latestVersion = "1.0.1";  // Latest available version

if (version_compare($latestVersion, $currentVersion, '>')) {
    echo json_encode([
        'version' => $latestVersion,
        'downloadUrl' => 'https://yourwebsite.com/downloads/RustDesktop-v' . $latestVersion . '-win-x64.zip',
        'releaseNotes' => "Version $latestVersion\n\n- Bug fixes\n- Performance improvements",
        'isMandatory' => false,
        'fileSize' => filesize('downloads/RustDesktop-v' . $latestVersion . '-win-x64.zip')
    ]);
} else {
    http_response_code(204); // No content - no update available
}
?>
```

### Node.js/Express Example

```javascript
app.get('/api/updates/check', (req, res) => {
    const currentVersion = '1.0.0';
    const latestVersion = '1.0.1';
    
    if (compareVersions(latestVersion, currentVersion) > 0) {
        res.json({
            version: latestVersion,
            downloadUrl: `https://yourwebsite.com/downloads/RustDesktop-v${latestVersion}-win-x64.zip`,
            releaseNotes: `Version ${latestVersion}\n\n- Bug fixes\n- Performance improvements`,
            isMandatory: false,
            fileSize: 153600000
        });
    } else {
        res.status(204).send(); // No update available
    }
});
```

## How It Works

1. **On Startup**: The app checks for updates in the background (non-blocking)
2. **Update Available**: User is prompted to download the update
3. **Download**: Update ZIP is downloaded to temp directory
4. **Installation**: On next app restart, the update is automatically installed
5. **File Replacement**: Old files are replaced with new ones from the ZIP

## Security Considerations

- Always serve updates over HTTPS
- Consider adding version verification/signing
- Validate file size matches expected size
- Consider rate limiting the update endpoint

## Testing

1. Deploy a new version with a higher version number
2. Update your endpoint to return the new version
3. Run the app - it should detect the update
4. Download and restart to apply
