# Setting Up Application Icon

To add a custom icon for the system tray and application:

## Option 1: Add Your Own Icon

1. Create or download a `.ico` file (Windows icon format)
2. Place it at: `src/RustDesktop.App/Icons/app.ico`
3. The icon should be at least 16x16, 32x32, and 48x48 pixels (multi-resolution ICO file)

## Option 2: Generate Icon from Image

You can convert a PNG to ICO using online tools:
- https://convertio.co/png-ico/
- https://www.icoconverter.com/
- Or use Visual Studio's built-in icon editor

## Icon Requirements

- **Format**: `.ico` (Windows Icon format)
- **Sizes**: Should include 16x16, 32x32, 48x48, and 256x256 for best quality
- **Location**: `src/RustDesktop.App/Icons/app.ico`
- **Naming**: Must be named `app.ico`

## Current Behavior

If `app.ico` doesn't exist, the app will use the system default application icon as a fallback.

## Testing

After adding the icon:
1. Rebuild the project: `dotnet build`
2. Run the app: `dotnet run --project src/RustDesktop.App/RustDesktop.App.csproj`
3. Check the system tray - you should see your custom icon





