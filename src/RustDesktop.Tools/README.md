# Rust Item Icon Tools

This project contains tools for managing Rust item icons.

## Tools

### 1. Icon Downloader
Downloads Rust item icons from the GitHub Gist or local file and saves them to the Icons folder.

### 2. Icon Copier
Copies existing icons from a source folder and renames them to match the app's naming convention.

## Usage

### Build the tool:
```bash
dotnet build
```

### Download icons from Gist/local file:
```bash
dotnet run
```

Or specify a custom icons folder:
```bash
dotnet run -- "C:\Path\To\Icons\Folder"
```

### Copy existing icons (from C:\Programming\RustDesktop\icons):
```bash
dotnet run -- copy-icons
```

Or specify custom source and target folders:
```bash
dotnet run -- copy-icons "C:\Source\Icons" "C:\Target\Icons"
```

## How it works

1. **Downloads the Gist data**: Fetches the JSON list of items with names and image URLs
2. **Maps display names to short names**: Uses `rust-item-list.json` to map display names (e.g., "Assault Rifle") to short names (e.g., "rifle.ak")
3. **Extracts short names from URLs**: If no mapping is found, extracts the short name from the image URL (e.g., `rifle.ak.png` → `rifle.ak`)
4. **Downloads icons**: Downloads each icon and saves it as `{shortName}.png` (dots replaced with underscores, e.g., `rifle_ak.png`)
5. **Skips existing files**: Won't re-download icons that already exist

## Output

Icons are saved to: `src\RustDesktop.App\Icons\` (or the specified folder)

File naming: `{shortName}.png` where dots are replaced with underscores
- Example: `rifle.ak` → `rifle_ak.png`
- Example: `ammo.pistol` → `ammo_pistol.png`

## Notes

- The tool includes a 100ms delay between downloads to avoid rate limiting
- Existing files are skipped automatically
- If an item can't be mapped to a short name, it will be skipped with a warning












