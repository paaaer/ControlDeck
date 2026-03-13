# ControlDeck

Desktop application for the ControlDeck hardware volume mixer.  
Written in **C# / .NET 8 / WinForms**.

Part of the [ControlDeck](https://github.com/paaaer/ControlDeck) project.  
Inspired by [deej](https://github.com/omriharel/deej) by Omri Harel.

---

## Features

- Runs in the **system tray** — zero screen footprint when minimised
- **Auto-detects** ControlDeckCore (scans serial ports for the CDC2 handshake)
- Maps each slider to: **master**, **mic**, **system sounds**, or **any app** (`chrome.exe`, `discord.exe`, `spotify.exe` …)
- Live **slider preview bars** in the configure window
- **Portable** — single `.exe`, no installer, config saved next to the executable
- **Instant startup** — ~10MB exe, no runtime required (self-contained)
- Prevents duplicate instances (mutex check on launch)

---

## Requirements

- Windows 10 or 11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download) — only needed to build from source
- A ControlDeckCore device connected via USB

---

## Quick Start

### Run from source

```bash
git clone https://github.com/paaaer/ControlDeck
cd ControlDeck/ControlDeck
dotnet run
```

### Build portable single exe

```bash
cd ControlDeck/ControlDeck
dotnet publish -c Release
# Output: bin/Release/net8.0-windows/win-x64/publish/ControlDeck.exe
```

---

## Usage

1. Plug in your ControlDeckCore via USB (Mini-USB, CP2102 bridge)
2. Launch `ControlDeck.exe` — appears in the system tray as a **green circle** when connected, **grey** when searching
3. **Double-click** the tray icon to open Configure
4. Assign each slider a target from the dropdown
5. Click **Save**

### Slider targets

| Target | Controls |
|--------|----------|
| `master` | System master output volume |
| `mic` | Default microphone input level |
| `system` | Windows system sounds |
| `chrome.exe` | Any running app by process name |
| `unassigned` | Does nothing |

App names are case-insensitive. Both `chrome` and `chrome.exe` work.

---

## Configuration

Stored as `controldeck_config.json` next to `ControlDeck.exe`:

```json
{
  "sliders": [
    { "index": 0, "target": "master" },
    { "index": 1, "target": "discord.exe" },
    { "index": 2, "target": "chrome.exe" },
    { "index": 3, "target": "spotify.exe" },
    { "index": 4, "target": "mic" },
    { "index": 5, "target": "unassigned" }
  ],
  "device": {
    "port": "auto",
    "baud": 115200
  }
}
```

Set `"port"` to `"COM3"` (or whichever port) to skip auto-detection.

---

## Project Structure

```
ControlDeck/
├── ControlDeck.csproj       # .NET 8 WinForms, NAudio dependency
├── Program.cs               # Entry point, single-instance mutex
├── AppConfig.cs             # JSON config model, load/save
├── Device/
│   ├── Protocol.cs          # CDC2 parser (handshake + frames)
│   ├── Detector.cs          # Serial port scanner
│   ├── SerialReader.cs      # Background reader thread
│   └── DeviceManager.cs     # Reconnect logic, UI-thread marshalling
├── Audio/
│   └── AudioController.cs   # NAudio Core Audio — master, mic, per-app
└── UI/
    ├── TrayApplicationContext.cs  # Tray icon + app controller
    ├── TrayIcon.cs                # Programmatic icon (no .ico file needed)
    ├── ConfigForm.cs              # Slider assignment + live preview
    └── AboutForm.cs               # About dialog
```

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| NAudio  | 2.2.1   | Windows Core Audio API — master, mic, per-app session volume |

`System.IO.Ports` and `System.Text.Json` are part of .NET 8 — no extra packages needed.
