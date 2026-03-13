# ControlDeckCore

Firmware for the **ControlDeck** hardware — an ESP32-WROOM-32 based USB volume slider controller.

Part of the [ControlDeck](https://github.com/paaaer/ControlDeck) project.

**Hardware: ESP32-WROOM-32 DevKit** (CP2102 USB-UART bridge, Mini-USB)

---

## Inspiration

This project is inspired by [deej](https://github.com/omriharel/deej) by Omri Harel — a fantastic open-source hardware volume mixer using physical sliders. ControlDeck uses the same hardware layout concept (potentiometers wired to a microcontroller over USB), but replaces the original Arduino Nano with an **ESP32-WROOM-32** and rewrites both the firmware and PC application from scratch with wireless OTA updates, a cleaner protocol, higher ADC resolution, and a modern cross-platform desktop app.

---

## Features

- **1–6 analog sliders** — plug potentiometers into ADC1 pins (GPIO 32,33,34,35,36,39), configure in one header file
- **12-bit resolution** — 4× better than original Arduino-based deej (0–4095 vs 0–1023)
- **EMA noise filter** — exponential moving average smoothing, tunable alpha
- **Deadband suppression** — ignores sub-threshold jitter, keeps serial traffic minimal
- **100 Hz update rate** — low latency, efficient framing
- **OTA firmware updates** — push new firmware wirelessly via PlatformIO
- **WiFiManager** — captive portal on first boot, no hardcoded credentials
- **mDNS** — accessible as `controldeck.local` on your network
- **Simple protocol** — human-readable, easy to parse, documented

---

## Quick Start

### Hardware
See [`docs/hardware.md`](docs/hardware.md) for wiring guide and BOM.

### Firmware
```bash
# 1. Clone
git clone https://github.com/paaaer/ControlDeck
cd ControlDeck/ControlDeckCore

# 2. Install PlatformIO (if needed)
pip install platformio

# 3. Flash (USB)
pio run -t upload

# 4. Monitor
pio device monitor
```

On first boot, connect your phone/laptop to the WiFi AP **"ControlDeckCore"** and enter your network credentials. Future boots connect automatically.

### OTA Update
```bash
pio run -t upload --upload-port controldeck.local
```

---

## Configuration

All settings in [`include/config.h`](include/config.h):

| Setting            | Default          | Description                        |
|--------------------|------------------|------------------------------------|
| `SLIDER_PINS[]`    | `{32,33,34,35,36,39}` | GPIO pins for sliders (ADC1 only) |
| `EMA_ALPHA`        | `0.15`           | Smoothing factor (lower = smoother)|
| `DEADBAND_THRESHOLD` | `8`            | Min change to trigger send         |
| `SEND_INTERVAL_MS` | `10`             | Send rate (10ms = 100Hz)           |
| `OTA_PASSWORD`     | `controldeck`    | Change before deploying!           |
| `MDNS_HOSTNAME`    | `controldeck`    | `controldeck.local`                |

---

## Protocol

See [`docs/protocol.md`](docs/protocol.md) for the full wire protocol specification.

Quick summary:
- Handshake: `CDC2:SLIDERS=4;VERSION=1.0.0;NAME=ControlDeckCore`
- Data: `V:512|780|0|4095`
- Commands: `CMD:PING`, `CMD:INFO`

---

## Project Structure

```
ControlDeckCore/
├── platformio.ini          # Board, framework, dependencies
├── include/
│   ├── config.h            # ← All user settings here
│   ├── sliders.h           # Slider manager interface
│   ├── protocol.h          # Protocol encoder interface
│   └── ota.h               # WiFi + OTA interface
├── src/
│   ├── main.cpp            # setup() / loop() entry point
│   ├── sliders.cpp         # ADC reading, EMA, deadband
│   ├── protocol.cpp        # Frame encoding, command handling
│   └── ota.cpp             # WiFiManager + ArduinoOTA
└── docs/
    ├── protocol.md         # Wire protocol specification
    └── hardware.md         # Wiring, BOM, pinout
```
