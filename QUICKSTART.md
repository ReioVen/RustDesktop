# Quick Start Guide

## 🚀 Getting Your Rust Desktop App Running

### Step 1: Prerequisites

Make sure you have:
- **.NET 8 SDK** installed ([Download here](https://dotnet.microsoft.com/download))
- **Visual Studio 2022** or **JetBrains Rider** (recommended) or **VS Code**
- **Windows 10/11**

Verify .NET installation:
```bash
dotnet --version
```
Should show: `8.0.x` or higher

### Step 2: Restore Dependencies

Open terminal in project root and run:
```bash
dotnet restore
```

This downloads all NuGet packages (MaterialDesign, CommunityToolkit, etc.)

### Step 3: Build the Project

```bash
dotnet build
```

You should see:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Step 4: Run the Application

```bash
dotnet run --project src/RustDesktop.App/RustDesktop.App.csproj
```

Or open `RustDesktop.sln` in Visual Studio and press F5.

### Step 5: Connect to a Rust Server

To connect, you'll need information from your Rust server:

1. **Open Rust game**
2. **Go to Settings → Rust+**
3. **Pair with Server** (if not already paired)
4. **Note down the following**:
   - Server IP Address
   - Server ID (long number)
   - Player Token (generated when pairing)
   - Your Steam ID (64-bit, can find on Steam profile)

5. **Enter in Rust Desktop app**:
   - Server IP: Your server's IP
   - Port: Usually `28082` (default Rust+ port)
   - Server ID: From Rust+ settings
   - Player Token: From Rust+ settings
   - Steam ID: Your Steam 64-bit ID

6. **Click "Connect"**

### Step 6: Using the App

Once connected:
- **Map View**: Shows your server's map (when implemented)
- **Vending Machines**: Lists all vending machines on server
- **Refresh Buttons**: Manually refresh data
- **Status Indicator**: Shows connection status (green = connected)

## 🛠️ Development Workflow

### Making Changes

1. **Edit code** in your IDE
2. **Build**: `dotnet build`
3. **Run**: `dotnet run --project src/RustDesktop.App/RustDesktop.App.csproj`
4. **Test**: Verify your changes work

### Project Structure

```
src/
├── RustDesktop.Core/        # Edit business logic here
│   ├── Models/              # Data models
│   └── Services/            # API services
│
└── RustDesktop.App/         # Edit UI here
    ├── ViewModels/          # UI logic
    ├── Views/               # XAML UI files
    └── Converters/          # Data binding helpers
```

### Common Tasks

**Add a new feature**:
1. Add model in `Core/Models/`
2. Add service method in `Core/Services/RustPlusService.cs`
3. Add ViewModel property/command in `App/ViewModels/MainViewModel.cs`
4. Add UI in `App/Views/MainWindow.xaml`

**Change UI styling**:
- Edit `App.xaml` for global styles
- Edit `MainWindow.xaml` for specific UI
- Material Design themes in `App.xaml` ResourceDictionary

## 🐛 Troubleshooting

### Build Errors

**Error**: "Package not found"
- **Solution**: Run `dotnet restore`

**Error**: "Target framework not found"
- **Solution**: Install .NET 8 SDK

### Runtime Errors

**Error**: "Cannot connect to server"
- **Check**: Server IP, port, and that Rust+ is enabled on server
- **Check**: Firewall isn't blocking connection
- **Check**: Server ID and Player Token are correct

**Error**: "Material Design theme not found"
- **Solution**: Run `dotnet restore` to download packages

### UI Not Updating

- **Check**: Data binding is correct (property names match)
- **Check**: ViewModel implements `INotifyPropertyChanged` (using `[ObservableProperty]`)
- **Check**: Commands are properly bound

## 📦 Building for Distribution

### Create Release Build

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

Output will be in: `src/RustDesktop.App/bin/Release/net8.0-windows/win-x64/publish/`

### Create Installer (Future)

For commercial distribution, consider:
- **WiX Toolset**: MSI installer
- **Squirrel**: Auto-update framework
- **ClickOnce**: Simple deployment (Visual Studio)

## 🔍 Next Steps

1. **Read ARCHITECTURE.md**: Understand the codebase structure
2. **Read README.md**: Full project documentation
3. **Explore the code**: Start with `MainViewModel.cs` and `MainWindow.xaml`
4. **Implement features**: Follow the patterns already established

## 💡 Tips

- **Use IntelliSense**: Hover over code to see documentation
- **Debug Mode**: Set breakpoints in Visual Studio to step through code
- **Material Design**: Check [MaterialDesignThemes docs](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) for UI components
- **MVVM**: Commands use `[RelayCommand]`, properties use `[ObservableProperty]`

## ❓ Need Help?

- Check `ARCHITECTURE.md` for design decisions
- Check `README.md` for full documentation
- Review code comments in service files
- Search for similar patterns in existing code

---

Happy coding! 🎮










