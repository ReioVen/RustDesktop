# Rust+ Pairing Analysis

## Working Version (rustplus-desktop-3.0.1) Approach

### Key Finding: FCM-Based Pairing
The working version uses **Firebase Cloud Messaging (FCM) notifications** for pairing, NOT direct WebSocket connections.

### How It Works:
1. **FCM Registration**: Uses Node.js CLI (`rustplus-cli`) to register for FCM notifications
2. **FCM Listening**: Listens for push notifications containing pairing payload
3. **Pairing Payload**: When you pair via mobile app or in-game, server sends FCM notification with:
   - `ip` (server IP)
   - `port` (Rust+ port)
   - `playerId` (Steam ID)
   - `playerToken` (Player Token)
   - `name` (server name)
   - `type` ("server", "entity", "alarm")

4. **Connection**: After receiving pairing payload, connects using `HandyS11.RustPlusApi` NuGet package:
   ```csharp
   _api = new RustPlus(profile.Host, profile.Port, steamId, playerToken, useProxy);
   await ConnectAsync();
   ```

### Files to Study:
- `PairingListenerRealProcess.cs` - Handles FCM listening and pairing payload parsing
- `RustPlusClientReal.cs` - Handles WebSocket connection after pairing
- Uses `rustplus-cli` Node.js tool for FCM registration/listening

## Our Current Approach

### What We're Doing:
1. **Credential Detection**: Searching registry and game files for stored Server ID and Player Token
2. **Direct WebSocket Connection**: Once credentials found, connect directly to `ws://IP:PORT/`
3. **Authentication**: Send auth message with Server ID, Player Token, and Steam ID

### The Problem:
- We're not finding the stored credentials (even though mobile app is connected)
- We're trying to actively pair via WebSocket (which doesn't work - pairing happens via FCM)

## Solution Options

### Option 1: Implement FCM Listening (Like Working Version)
- **Pros**: Matches working version exactly
- **Cons**: Requires Node.js, Firebase setup, complex

### Option 2: Improve Credential Detection
- **Pros**: Simpler, no external dependencies
- **Cons**: Need to find where mobile app stores credentials

### Option 3: Hybrid Approach
- Listen for in-game pairing by monitoring game's network traffic
- Or intercept pairing when it happens in-game
- Store credentials when found
- Then connect normally

## Next Steps

1. **Improve credential detection** - Check more locations, better parsing
2. **Add manual input option** - If auto-detection fails
3. **Test connection** - Once credentials found, connection should work (code looks correct)

## Key Insight

**The connection code is correct** - once we have Server ID and Player Token, connecting should work. The issue is **finding the stored credentials** after mobile app pairing.










