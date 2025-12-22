# Rust Desktop - Project Summary for CV

## Project Overview

**Rust Desktop** is a commercial-grade desktop application that provides Rust+ functionality on Windows, allowing players to view maps, monitor vending machines, and control smart devices from their computer. This application replicates and enhances the functionality of the official Rust+ mobile app for desktop platforms, built with modern .NET technologies and professional software architecture patterns.

## Technology Stack

- **Language**: C# (.NET 8.0)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Architecture Pattern**: MVVM (Model-View-ViewModel)
- **Dependency Injection**: Microsoft.Extensions.Hosting
- **UI Library**: Material Design for WPF (MaterialDesignThemes)
- **MVVM Framework**: CommunityToolkit.Mvvm
- **API Integration**: RustPlusApi library (WebSocket-based protocol)
- **JSON Processing**: Newtonsoft.Json
- **Additional Technologies**: Node.js integration for FCM pairing, async/await patterns, event-driven architecture

## Architecture & Design

### Project Structure
- **RustDesktop.Core**: Business logic layer containing models, services, and API communication
- **RustDesktop.App**: Presentation layer with ViewModels, Views (XAML), and UI converters
- **RustDesktop.Tools**: Utility tools for icon management and validation

### Key Design Patterns
- **MVVM Pattern**: Complete separation of concerns between UI and business logic
- **Dependency Injection**: Loose coupling using Microsoft.Extensions.Hosting
- **Service Layer Pattern**: Encapsulated API communication and protocol handling
- **Event-Driven Architecture**: Real-time updates via WebSocket events
- **Repository Pattern**: Configuration and data persistence services

### Technical Implementation Highlights

1. **WebSocket Communication**: Implemented real-time bidirectional communication with Rust game servers using the Rust+ protocol
2. **FCM Pairing Integration**: Integrated Node.js-based FCM (Firebase Cloud Messaging) listener for automatic server pairing
3. **Material Design UI**: Professional, modern interface with dark theme, animations, and accessibility features
4. **Async/Await Patterns**: Non-blocking I/O operations throughout the application
5. **Reflection-Based Data Extraction**: Dynamic data extraction from Rust+ API responses
6. **Configuration Management**: Persistent settings storage with JSON-based configuration service

## Key Features

- **Real-Time Server Connection**: WebSocket-based connection to Rust game servers with automatic reconnection
- **Interactive Map View**: Display and interaction with server maps including zoom and pan capabilities
- **Vending Machine Monitoring**: Real-time tracking and display of vending machine inventory and status
- **Smart Device Control**: Interface for controlling in-game smart devices
- **Server Pairing**: Automated pairing with Rust servers via FCM notifications or mobile app integration
- **Connection Management**: Multiple server support with connection state management
- **Modern UI/UX**: Material Design interface with responsive layouts and intuitive controls

## Technical Challenges & Solutions

1. **Challenge**: Reverse engineering and implementing the Rust+ WebSocket protocol
   - **Solution**: Integrated RustPlusApi library and implemented custom message processing with reflection-based data extraction

2. **Challenge**: Real-time updates without blocking the UI thread
   - **Solution**: Implemented async/await patterns with event-driven architecture, ensuring all network operations run on background threads

3. **Challenge**: Cross-platform integration (Node.js for FCM pairing)
   - **Solution**: Created abstraction layer (IPairingListener) supporting bundled Node.js, system Node.js, or npx fallback

4. **Challenge**: Maintaining stable WebSocket connections
   - **Solution**: Implemented connection state management, automatic reconnection logic, and comprehensive error handling

5. **Challenge**: Professional commercial-grade architecture
   - **Solution**: Applied industry-standard patterns (MVVM, DI, Service Layer) with clean separation of concerns and extensibility

## Project Scale & Complexity

- **Solution Structure**: Multi-project solution with clear separation of concerns
- **Code Organization**: Modular architecture with 3 main projects (Core, App, Tools)
- **Service Layer**: 10+ service interfaces and implementations
- **Data Models**: Multiple domain models (ServerInfo, MapInfo, VendingMachine, SmartDevice, TeamInfo, etc.)
- **UI Components**: Custom WPF controls, value converters, and Material Design integration
- **External Dependencies**: Integration with third-party Rust+ API library, Node.js runtime, and FCM services

## Skills Demonstrated

- **C# Development**: Advanced C# features including async/await, LINQ, reflection, generics
- **WPF Development**: XAML design, data binding, custom controls, styling, Material Design integration
- **MVVM Architecture**: Complete MVVM implementation with ViewModels, Commands, and data binding
- **Dependency Injection**: Microsoft.Extensions.Hosting setup and service registration
- **WebSocket Programming**: Real-time bidirectional communication implementation
- **API Integration**: Third-party library integration and protocol implementation
- **Software Architecture**: Clean architecture, separation of concerns, SOLID principles
- **UI/UX Design**: Material Design implementation, responsive layouts, user experience optimization
- **Async Programming**: Comprehensive use of async/await for I/O operations
- **Configuration Management**: JSON-based settings persistence
- **Cross-Platform Integration**: Node.js process management and integration
- **Error Handling**: Comprehensive exception handling and user feedback
- **Code Organization**: Multi-project solution structure, modular design

## Commercial Considerations

- Built with commercial-grade code quality and architecture
- Extensible design supporting future monetization features
- Professional UI/UX suitable for paid software
- Comprehensive error handling and user feedback
- Settings persistence and configuration management
- Architecture supports license validation, auto-updates, and analytics integration

## Development Approach

- **Modern .NET Practices**: Latest .NET 8 features and patterns
- **Industry Standards**: Following Microsoft recommended practices for WPF and MVVM
- **Documentation**: Comprehensive documentation including architecture decisions, implementation guides, and quick start documentation
- **Maintainability**: Clean code structure with clear separation of concerns
- **Testability**: Architecture designed for unit testing and integration testing

---

**Note for CV**: This project demonstrates full-stack desktop application development skills, modern software architecture, real-time communication, and professional UI/UX design. It showcases the ability to work with complex protocols, integrate multiple technologies, and build commercial-grade software following industry best practices.



