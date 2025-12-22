# Why Our Pairing Doesn't Work vs Working Version

## Key Differences

### Working Version (rustplus-desktop-3.0.1)

1. **Pairing Method: FCM (Firebase Cloud Messaging)**
   - Uses a Node.js process (`rustplus-cli`) that:
     - Runs `fcm-register` - Opens browser for Steam login, registers with Google FCM
     - Runs `fcm-listen` - Listens for FCM notifications from Google
   - When you pair in-game (ESC → Rust+ → Pair), Rust sends an FCM notification
   - The Node.js process receives the FCM notification containing:
     - Server ID
     - Player Token
     - Server IP/Port
     - Steam ID
   - This info is then used to connect

2. **Connection Method: RustPlusApi Library**
   - Uses NuGet package: `RustPlusApi` (version 1.3.0)
   - This library handles all WebSocket protocol details
   - Just needs: Server IP, Port, Steam ID, Player Token

### Our Current Implementation

1. **Pairing Method: Direct WebSocket Connection** ❌
   - Tries to connect to WebSocket and wait for pairing messages
   - **This doesn't work** because pairing info comes through FCM, not WebSocket
   - WebSocket is only used AFTER pairing is complete

2. **Connection Method: Custom WebSocket Implementation**
   - We implement the WebSocket protocol ourselves
   - This is complex and error-prone

## Why Our Approach Fails

1. **Pairing doesn't happen through WebSocket**
   - When you pair in-game, Rust sends pairing info to Google FCM
   - FCM then delivers it to registered clients (mobile app, or our Node.js listener)
   - The WebSocket endpoint doesn't send pairing info - it only accepts connections AFTER pairing

2. **Our WebSocket connection attempts fail because:**
   - We don't have valid Server ID + Player Token yet (they come from FCM)
   - The server rejects connections without proper credentials
   - We're trying to get credentials from the wrong source

## Solution Options

### Option 1: Use RustPlusApi Library (Recommended)
- Add `RustPlusApi` NuGet package
- Replace our custom WebSocket implementation
- For pairing: Users must pair via mobile Rust+ app first, then credentials are stored locally
- Our app reads the stored credentials and connects

### Option 2: Integrate FCM Listener (Complex)
- Bundle Node.js runtime and `rustplus-cli` tool
- Run FCM registration and listening process
- Parse FCM notifications for pairing info
- This is what the working version does

### Option 3: Read Pairing from Mobile App Storage
- Mobile Rust+ app stores pairing info somewhere
- We could try to read it from:
  - Android: `/data/data/com.corrodinggames.rustplus/files/`
  - iOS: App sandbox
  - But this requires root/jailbreak access

## Recommended Approach

**Use RustPlusApi + Document Mobile Pairing Requirement**

1. Add `RustPlusApi` NuGet package
2. Update `RustPlusService` to use the library
3. Update `PairingService` to:
   - Check for existing pairing credentials (from mobile app)
   - If not found, show instructions to pair via mobile app first
   - Once paired via mobile app, credentials are stored and we can connect

This is simpler than implementing FCM ourselves and works reliably.







