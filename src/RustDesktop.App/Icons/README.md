# Item Icons Directory

Place all Rust item icon images in this folder. Icons will be automatically packaged with the application.

## Icon Naming Convention

Icons should be named using one of these formats:

1. **By Item ID**: `{itemId}.png` (e.g., `-2097376851.png`)
2. **By Item ID with prefix**: `item_{itemId}.png` (e.g., `item_-2097376851.png`)
3. **By Short Name**: `{shortName}.png` (e.g., `ammo.pistol.png` or `ammo_pistol.png`)
4. **By Short Name with prefix**: `icon_{shortName}.png` (e.g., `icon_ammo_pistol.png`)

## Supported Formats

- PNG (recommended)
- JPG/JPEG

## Where to Get Icons

1. Extract from Rust game installation (Unity asset bundles)
2. Download from Rust item databases/APIs
3. Use icons from rust-item-list.json URLs (download and save locally)

## Example

If you have an item with:
- Item ID: `-2097376851`
- Short Name: `ammo.pistol`

You can name the icon file as:
- `-2097376851.png`
- `item_-2097376851.png`
- `ammo_pistol.png`
- `icon_ammo_pistol.png`

The application will automatically find and use the icon regardless of which naming convention you use.







