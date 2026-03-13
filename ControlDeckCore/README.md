# ControlDeckCore

Firmware for the ControlDeck hardware — an ESP32-WROOM-32 based USB volume slider controller.

Part of the [ControlDeck](https://github.com/paaaer/ControlDeck) project.
Inspired by [deej](https://github.com/omriharel/deej) by Omri Harel — same hardware layout, new controller board and new software.

**Hardware: ESP32-WROOM-32 DevKit (NodeMCU-32S, 38-pin)** — CP2102 USB-UART bridge, Mini-USB

---

## Features

- **1–6 analog sliders** — potentiometers on ADC1 pins (GPIO 32, 33, 34, 35, 36, 39)
- **12-bit resolution** — 4× better than the original Arduino-based deej (0–4095 vs 0–1023)
- **EMA noise filter** — exponential moving average smoothing per slider, tunable alpha
- **Deadband suppression** — ignores sub-threshold jitter, keeps serial traffic minimal
- **100 Hz update rate** — low latency, compact framing
- **Wireless OTA updates** — push new firmware via PlatformIO over WiFi, no USB needed
- **WiFiManager** — captive portal on first boot, no hardcoded credentials
- **mDNS** — accessible as `controldeck.local` on your network

---

## Quick Start

### Requirements

- [PlatformIO](https://platformio.org/) (VS Code extension or CLI)
- ESP32-WROOM-32 DevKit connected via Mini-USB
- CP2102 driver — Windows usually installs automatically, shows as *Silicon Labs CP210x USB to UART Bridge*

### First flash (USB)

```bash
git clone https://github.com/paaaer/ControlDeck
cd ControlDeck/ControlDeckCore

pio run -t upload
pio device monitor
```

On first boot the device opens a WiFi access point called **ControlDeckCore**.
Connect to it from your phone or laptop, enter your network credentials via the captive portal.
Future boots connect automatically.

### OTA updates (wireless)

```bash
pio run -t upload --upload-port controldeck.local
# or by IP:
pio run -t upload --upload-port 192.168.x.x
```

OTA password is set in `include/config.h` → `OTA_PASSWORD` (default: `controldeck` — **change this**).

---

## Configuration

Everything is in [`include/config.h`](include/config.h) — it's the only file you ever need to edit.

### Slider pins

```cpp
static const uint8_t SLIDER_PINS[] = {
    32,  // Slider 1 — ADC1_CH4
    33,  // Slider 2 — ADC1_CH5
    34,  // Slider 3 — ADC1_CH6  (input only)
    35,  // Slider 4 — ADC1_CH7  (input only)
    36,  // Slider 5 — ADC1_CH0  (input only)
    39,  // Slider 6 — ADC1_CH3  (input only)
};
```

Add or remove lines to change slider count. The desktop app reads count from the handshake — no changes needed on the PC side.

### Key settings

| Setting | Default | Description |
|---------|---------|-------------|
| `EMA_ALPHA` | `0.15` | Smoothing factor — lower is smoother but slower to respond |
| `DEADBAND_THRESHOLD` | `8` | Minimum change (out of 4095) before a new value is sent |
| `SEND_INTERVAL_MS` | `10` | Send rate — 10ms = 100 Hz |
| `OTA_PASSWORD` | `controldeck` | Change before deploying |
| `MDNS_HOSTNAME` | `controldeck` | Device accessible as `controldeck.local` |

---

## Hardware

### Bill of Materials

| Component | Qty | Notes |
|-----------|-----|-------|
| ESP32-WROOM-32 DevKit (NodeMCU-32S) | 1 | 38-pin, CP2102 bridge, Mini-USB |
| 10kΩ linear potentiometer (B10K) | 1–6 | Slide or rotary |
| 100nF ceramic capacitor | 1–6 | One per slider, reduces ADC noise |
| USB Mini-B cable | 1 | Data-capable |

### Wiring

Each potentiometer has 3 pins:

```
3.3V ──── [Left  pin]
GND  ──── [Right pin]
GPIO ──── [Wiper/Mid]  + 100nF cap to GND
```

Full 6-slider example:

```
3.3V ─┬─[POT1]─ GND    Wiper ── GPIO32
      ├─[POT2]─ GND    Wiper ── GPIO33
      ├─[POT3]─ GND    Wiper ── GPIO34
      ├─[POT4]─ GND    Wiper ── GPIO35
      ├─[POT5]─ GND    Wiper ── GPIO36
      └─[POT6]─ GND    Wiper ── GPIO39
```

See [`docs/hardware.md`](docs/hardware.md) for the full pinout reference.

---

## Protocol

ControlDeckCore speaks the **CDC2 wire protocol** over USB serial at 115200 baud.

```
← CDC2:SLIDERS=6;VERSION=1.0.0;NAME=ControlDeckCore   (on connect / CMD:INFO)
← V:512|780|0|4095|2048|1024                           (data frame, ~100 Hz)
→ CMD:PING   ←  ACK:PONG
→ CMD:INFO   ←  handshake line
```

Values are 12-bit unsigned (0–4095), pipe-delimited, newline-terminated.
Full specification in [`docs/protocol.md`](docs/protocol.md).

---

## Project Structure

```
ControlDeckCore/
├── platformio.ini          # Board: esp32dev, framework: arduino
├── include/
│   ├── config.h            # ← All user settings here
│   ├── sliders.h           # Slider manager interface
│   ├── protocol.h          # Protocol encoder interface
│   └── ota.h               # WiFi + OTA interface
├── src/
│   ├── main.cpp            # setup() / loop() — wires everything together
│   ├── sliders.cpp         # ADC reading, oversampling, EMA filter, deadband
│   ├── protocol.cpp        # Frame encoding, command handling
│   └── ota.cpp             # WiFiManager captive portal + ArduinoOTA + mDNS
└── docs/
    ├── hardware.md         # Wiring guide, BOM, full pinout
    └── protocol.md         # CDC2 wire protocol specification
```
