# Rust Desktop - Unofficial Rust+ Desktop Client

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=c-sharp)](https://docs.microsoft.com/dotnet/csharp/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)](https://docs.microsoft.com/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A modern desktop application that provides Rust+ functionality on Windows, allowing players to view maps, monitor vending machines, and control smart devices from their computer. Built with C# and WPF using modern .NET 8 and Material Design principles.

## 🎯 Project Overview

This application replicates and enhances the functionality of the official Rust+ mobile app for desktop platforms. Built with C# and WPF, it provides a beautiful Material Design interface for interacting with Rust servers.

## 🏗️ Architecture & Thought Process

### **Technology Stack Decisions**

1. **C# with WPF (.NET 8)**
   - **Why**: Native Windows integration, excellent performance, rich UI capabilities
   - **Alternative considered**: Electron (rejected due to higher memory usage and less native feel)

2. **Material Design for WPF**
   - **Why**: Modern, professional UI that users expect in commercial software
   - Provides consistent theming, animations, and accessibility features

3. **MVVM Pattern (Model-View-ViewModel)**
   - **Why**: Separation of concerns, testability, maintainability
   - Uses CommunityToolkit.Mvvm for modern MVVM implementation

4. **Dependency Injection (Microsoft.Extensions.Hosting)**
   - **Why**: Loose coupling, easier testing, better code organization
   - Industry standard for commercial applications

### **Project Structure**

```
RustDesktop/
├── src/
│   ├── RustDesktop.Core/          # Business logic & API layer
│   │   ├── Models/                 # Data models (ServerInfo, MapInfo, etc.)
│   │   └── Services/               # Rust+ API communication
│   │
│   └── RustDesktop.App/            # WPF UI application
│       ├── ViewModels/             # MVVM ViewModels
│       ├── Views/                  # XAML UI definitions
│       └── Converters/             # Value converters for data binding
│
└── RustDesktop.sln                 # Visual Studio solution
```

### **Key Components Explained**

#### 1. **RustPlusService** (`Core/Services/RustPlusService.cs`)
   - **Purpose**: Handles WebSocket communication with Rust servers
   - **How it works**:
     - Connects via WebSocket to Rust server
     - Sends authentication with Server ID, Player Token, Steam ID
     - Listens for real-time updates (map changes, vending machine updates)
     - Implements event-driven architecture for UI updates

#### 2. **MainViewModel** (`App/ViewModels/MainViewModel.cs`)
   - **Purpose**: Business logic for the main window
   - **Responsibilities**:
     - Manages connection state
     - Handles user commands (Connect, Disconnect, Refresh)
     - Updates UI through data binding
     - Coordinates between UI and service layer

#### 3. **Models** (`Core/Models/`)
   - **ServerInfo**: Connection details (IP, port, tokens)
   - **MapInfo**: Map data with markers and image
   - **VendingMachine**: Vending machine data with items
   - **SmartDevice**: Smart device information and controls

### **How Rust+ Integration Works**

1. **Authentication Flow**:
   ```
   User enters credentials → MainViewModel → RustPlusService → WebSocket connection
   → Server validates → Connection established → Real-time data stream begins
   ```

2. **Data Flow**:
   ```
   Rust Server → WebSocket → RustPlusService → Events → MainViewModel → UI Updates
   ```

3. **Protocol Understanding**:
   - Rust+ uses WebSocket protocol (not HTTP REST)
   - Messages are JSON-formatted
   - Server pushes updates automatically
   - Client can request specific data (map, vending machines)

### **Commercial Considerations**

#### **What Makes This Commercial-Grade**

1. **Professional Architecture**
   - Clean separation of concerns
   - Dependency injection for testability
   - Event-driven updates for responsiveness

2. **User Experience**
   - Material Design for modern look
   - Real-time updates without manual refresh
   - Clear status indicators
   - Error handling and user feedback

3. **Extensibility**
   - Easy to add new features (smart devices, team chat, etc.)
   - Modular design allows feature additions
   - Service layer can be swapped/tested independently

#### **Potential Revenue Features** (Future)
- Server favorites/bookmarks
- Multiple server connections
- Historical data tracking
- Advanced map markers
- Custom themes
- Push notifications
- Export/import configurations

### **Technical Challenges & Solutions**

1. **Challenge**: Reverse engineering Rust+ protocol
   - **Solution**: Research existing community libraries, implement WebSocket handler with extensible message processing

2. **Challenge**: Real-time updates without blocking UI
   - **Solution**: Async/await pattern, event-driven architecture, background WebSocket listener

3. **Challenge**: Map image rendering
   - **Solution**: WPF Image control with byte array conversion, proper scaling and markers overlay

4. **Challenge**: Maintaining connection stability
   - **Solution**: Automatic reconnection logic, error handling, connection state management

### **Development Roadmap**

#### **Phase 1: Foundation** ✅ (Current)
- [x] Project structure
- [x] MVVM architecture
- [x] Basic UI with Material Design
- [x] Rust+ service skeleton

#### **Phase 2: Core Features** (Next)
- [ ] Complete Rust+ protocol implementation
- [ ] Map rendering with markers
- [ ] Vending machine list with details
- [ ] Smart device controls

#### **Phase 3: Polish** (Future)
- [ ] Settings persistence
- [ ] Multiple server support
- [ ] Advanced filtering/search
- [ ] Export functionality

#### **Phase 4: Commercial Features** (Future)
- [ ] License validation
- [ ] Auto-update system
- [ ] Analytics (privacy-respecting)
- [ ] Customer support integration

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or JetBrains Rider
- Windows 10/11

### Building
```bash
dotnet restore
dotnet build
```

### Running
```bash
dotnet run --project src/RustDesktop.App/RustDesktop.App.csproj
```

## 📝 Configuration

To connect to a Rust server, you'll need:
- **Server IP**: The IP address of your Rust server
- **Port**: Usually 28082 (Rust+ default)
- **Server ID**: Found in Rust server settings
- **Player Token**: Generated when pairing with server in-game
- **Steam ID**: Your Steam 64-bit ID

## ⚠️ Important Notes

1. **Unofficial Application**: This is not affiliated with Facepunch Studios or Rust
2. **API Stability**: Rust+ protocol may change, requiring updates
3. **Server Compatibility**: Not all servers may support Rust+ features
4. **Legal**: Ensure compliance with Rust's Terms of Service

## 🔧 Technical Details

### Dependencies
- **MaterialDesignThemes**: Modern UI components
- **CommunityToolkit.Mvvm**: MVVM helpers
- **Microsoft.Extensions.Hosting**: Dependency injection
- **System.Net.WebSockets.Client**: WebSocket communication

### Framework
- .NET 8.0 (Windows)
- WPF (Windows Presentation Foundation)
- MVVM Pattern

## 📸 Screenshots

_Coming soon - Screenshots of the application will be added here_

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## ⚠️ Disclaimer

This is an **unofficial** application and is not affiliated with, endorsed by, or associated with Facepunch Studios or Rust. This project is for educational and personal use. Use at your own risk and ensure compliance with Rust's Terms of Service.

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to check the [issues page](../../issues) if you want to contribute.

## 🙏 Acknowledgments

- [RustPlusApi](https://github.com/HandyS11/RustPlusApi) - Rust+ protocol implementation library
- [Material Design In XAML Toolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) - Material Design components for WPF
- Facepunch Studios for creating Rust and the Rust+ protocol

---

**Built with ❤️ for the Rust community**











