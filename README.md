# ClawApp

I wrote this because I dont trust my openclaw instance with my whatsapp account XD
Not going to push it much further past a chat app. For speech to text communication and push notifications you will need to grab the changes to openclaw I made [here](https://github.com/smidy/openclaw)

**Cross-platform mobile and desktop client for OpenClaw**

ClawApp is a native application for connecting to OpenClaw AI agents. Built with Avalonia UI, it provides a seamless chat interface across Desktop (Linux/Windows/macOS), Android, and iOS platforms.

---

## Features

- 🔐 **Secure Authentication** — Ed25519 cryptographic authentication
- 💬 **Real-time Chat** — WebSocket-based messaging with content blocks (text, thinking, tool calls, files)
- 📱 **Cross-Platform** — Single codebase for Desktop, Android, iOS
- 🎨 **Modern UI** — Fluent Design with Avalonia UI
- 🔄 **Connection Resilience** — Automatic reconnection with exponential backoff (Polly)
- 🔍 **Message Filtering** — Filter tool results and debug content
- 📝 **Markdown Support** — Rich text formatting in messages

---

## Tech Stack

- **Framework:** [Avalonia UI](https://avaloniaui.net/) (v11.2+)
- **Runtime:** .NET 9.0
- **Architecture:** MVVM with CommunityToolkit.Mvvm
- **Protocol:** OpenClaw WebSocket Protocol
- **Cryptography:** NSec (Ed25519), BouncyCastle
- **Resilience:** Polly for retry/reconnect policies
- **Logging:** Microsoft.Extensions.Logging

---

## Project Structure

```
clawapp/
├── clawapp/                    # Core shared library (ViewModels, Services, Models)
├── clawapp.Desktop/            # Desktop application (Linux/Windows/macOS)
├── clawapp.Android/            # Android application
├── clawapp.iOS/                # iOS application
├── clawapp.Browser/            # Browser/WASM (experimental)
├── clawapp.Tests/              # Unit tests
└── docs/                       # Architecture and protocol documentation
```

---

## Building

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- **For Android:**
  - Android SDK (API 31+)
  - Java JDK 11+ (full JDK, not just JRE)
- **For iOS:**
  - macOS with Xcode
  - Apple Developer account
  
### Desktop

```bash
# Build
dotnet build clawapp.Desktop/clawapp.Desktop.csproj -c Release

# Run
dotnet run --project clawapp.Desktop/clawapp.Desktop.csproj
```

### Android

```bash
# Build APK
dotnet publish clawapp.Android/clawapp.Android.csproj \
  -c Release \
  -f net9.0-android36.0 \
  -p:AndroidSdkDirectory=~/Android/Sdk \
  -p:JavaSdkDirectory=/usr/lib/jvm/msopenjdk-11-amd64

# Output: clawapp.Android/bin/Release/net9.0-android36.0/publish/*.apk

# Install via ADB
adb install clawapp.Android/bin/Release/net9.0-android36.0/publish/com.CompanyName.clawapp-Signed.apk
```

### iOS (Never tested, I dont have a mac.)

```bash
# Build (requires macOS)
dotnet build clawapp.iOS/clawapp.iOS.csproj -c Release
```

---

## Firebase Setup (Push Notifications)

ClawApp uses Firebase Cloud Messaging (FCM) for push notifications on Android.

### 1. Create Firebase Project

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create a new project (or use existing)
3. Add Android app with package name: `com.CompanyName.clawapp`

### 2. Download Configuration

1. In Firebase Console → Project Settings → General
2. Download `google-services.json`
3. Place it in: `clawapp.Android/google-services.json`

### 3. Notification Channel (Android 8.0+)

The app creates a notification channel programmatically in `MainActivity.OnCreate()`. No manual setup required, but you can customize:

- **Channel ID:** `clawapp_default_channel`
- **Channel Name:** "ClawApp Messages"
- **Importance:** High

### 4. Testing Push Notifications

Use Firebase Console → Cloud Messaging → Send test message, or test via the OpenClaw Gateway directly.

### All Platforms

```bash
# Restore dependencies
dotnet restore

# Build everything
dotnet build

# Run tests
dotnet test clawapp.Tests/clawapp.Tests.csproj
```

---

## Configuration

Create `appsettings.json` in the application directory:

```json
{
  "OpenClaw": {
    "GatewayUrl": "ws://localhost:8080",
    "DeviceName": "MyDevice"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

### Key Settings

- **GatewayUrl** — WebSocket URL of OpenClaw Gateway
- **DeviceName** — Friendly name for this device (shown in agent UI)

### Development Configuration

Create `appsettings.Development.json` for local development (gitignored):

```json
{
  "OpenClaw": {
    "GatewayUrl": "ws://localhost:8080",
    "DeviceName": "DevDevice"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Android Signing (Release Builds)

Release APKs require signing. Place keystore in `keystore/`:

```bash
# Example: Create keystore
keytool -genkey -v -keystore clawapp.keystore -alias clawapp \
  -keyalg RSA -keysize 2048 -validity 10000
```

Set environment variables or pass properties:
- `AndroidKeyStore` — Path to keystore
- `AndroidSigningKeyAlias` — Key alias
- `AndroidSigningKeyPass` / `AndroidSigningStorePass` — Passwords

---

## Architecture

### MVVM Pattern

- **Views** — Avalonia XAML (`MainView.axaml`, `MainWindow.axaml`)
- **ViewModels** — `ChatViewModel`, `MessageViewModel` (CommunityToolkit.Mvvm)
- **Services** — `WebSocketService`, `CryptoService`, `ConnectionService`
- **Models** — `Message`, `ContentBlock`, `Device`

### Key Components

#### ChatViewModel
Main view model managing:
- Message collection (`ObservableCollection<MessageViewModel>`)
- Connection state
- User input handling
- Message filtering

#### WebSocketService
Handles WebSocket communication:
- Connect/disconnect/reconnect
- Message send/receive
- Protocol serialization (JSON)
- Automatic reconnection with Polly

#### CryptoService
Manages cryptographic operations:
- Ed25519 key generation and storage
- Message signing
- Device ID derivation (SHA256 of public key)

### Threading

**Critical:** All UI collection modifications must use `Dispatcher.UIThread.InvokeAsync()`:

```csharp
await Dispatcher.UIThread.InvokeAsync(() =>
{
    Messages.Add(new MessageViewModel(message));
}, DispatcherPriority.Render);
```

---

## Development

### Running Tests

```bash
dotnet test clawapp.Tests/clawapp.Tests.csproj
```

### Debugging

**Desktop:**
```bash
dotnet run --project clawapp.Desktop/clawapp.Desktop.csproj --configuration Debug
```

**Android:** Use Visual Studio or Rider with Android device/emulator

### Code Style

- **Nullable Reference Types:** Enabled (`<Nullable>enable</Nullable>`)
- **C# Version:** Latest (`<LangVersion>latest</LangVersion>`)
- **Compiled Bindings:** Enabled for Avalonia

---

## Troubleshooting

### "Metadata file not found" Test Errors

```bash
# Build the main project first
dotnet build clawapp/clawapp.csproj
dotnet test clawapp.Tests/clawapp.Tests.csproj
```

### Push Notifications Not Received

1. Verify `google-services.json` is in `clawapp.Android/`
2. Check package name matches Firebase project
3. Ensure notification channel is created (Android 8.0+)
4. Check Firebase Console logs for delivery status

### Connection Fails Immediately

1. Verify Gateway URL in `appsettings.json`
2. Check firewall (WebSocket port 8080)
3. Ensure device has unique DeviceName

---

## Known Issues

### Android Build Warnings

**Java SDK Path Warning:**
- Cause: JRE installed instead of full JDK
- Solution: Pass `-p:JavaSdkDirectory=/path/to/full/jdk` to build command
- Impact: None (build succeeds)

**16KB Page Size Warning (libsodium, libSkiaSharp):**
- Cause: Native libraries not compiled for Android 16+ page size requirements
- Solution: Wait for upstream package updates
- Impact: None on current Android versions (affects only future Android 16+)

---

## License

MIT

---

## Links

- **OpenClaw:** [github.com/openclaw/openclaw](https://github.com/openclaw/openclaw)
- **Avalonia UI:** [avaloniaui.net](https://avaloniaui.net/)
