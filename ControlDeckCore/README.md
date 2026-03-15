# ControlDeckCore

Firmware for the ControlDeck hardware — an ESP32-WROOM-32 based USB volume slider controller.

Part of the [ControlDeck](https://github.com/paaaer/ControlDeck) project.
Inspired by [deej](https://github.com/omriharel/deej) by Omri Harel — same hardware layout, new controller board and new software.

**Hardware: ESP32-WROOM-32 DevKit (NodeMCU-32S, 38-pin)** — CP2102 USB-UART bridge, Mini-USB

---

## Features

- **1–5 analog sliders** — potentiometers on ADC1 pins (GPIO 32, 33, 34, 35, 36)
- **12-bit resolution** — 4× better than the original Arduino-based deej (0–4095 vs 0–1023)
- **Per-slider calibration** — set min/max via the web UI, stored permanently in flash
- **Per-slider naming** — give each slider a custom name (Master, Discord, Spotify…)
- **EMA noise filter** — exponential moving average smoothing, tunable alpha
- **Deadband suppression** — ignores sub-threshold jitter, keeps serial traffic minimal
- **Configurable polling rate** — 10–100 Hz, adjustable from the web UI
- **Wireless OTA updates** — push new firmware via PlatformIO over WiFi, no USB needed
- **WiFiManager** — captive portal on first boot, no hardcoded credentials
- **mDNS** — accessible as `controldeck.local` on your network
- **Web UI** — built-in calibration and settings interface at `http://controldeck.local`

---

## Development Environment Setup

### Step 1 — Install VS Code

Download and install from https://code.visualstudio.com/download

### Step 2 — Install the PlatformIO extension

1. Open VS Code
2. Press `Ctrl+Shift+X` to open the Extensions panel
3. Search for **PlatformIO IDE**
4. Click **Install** — takes 2–5 minutes
5. When done, click **Reload Window** when prompted
6. You will see an **alien head icon** in the left sidebar — that is PlatformIO

### Step 3 — Open the project

PlatformIO works by opening the folder that contains `platformio.ini` — **not the repo root**.

1. In VS Code: **File → Open Folder**
2. Navigate to `ControlDeck/ControlDeckCore`
3. Click **Select Folder**

VS Code detects `platformio.ini` and PlatformIO initialises the project automatically.
The first time it downloads the ESP32 toolchain — takes a few minutes, just let it run.

### Step 4 — Connect your board

1. Plug in the NodeMCU-32S via Mini-USB
2. Open Windows Device Manager
3. Look under **Ports (COM & LPT)** for **Silicon Labs CP210x USB to UART Bridge (COMx)**
4. Note the COM number — PlatformIO will find it automatically

---

## Build, Upload and Verify

The PlatformIO toolbar appears at the **bottom of VS Code**:

| Button | Action |
|--------|--------|
| **✓** tick | Build / compile only — checks for errors without flashing |
| **→** arrow | Upload — compile and flash to the board |
| **🔌** plug | Serial monitor — open terminal to see output |
| **🗑** bin | Clean — delete build cache |

### Build

Click **✓** to compile. A successful build ends with:

```
RAM:   [=         ]   9.6% (used 31384 bytes from 327680 bytes)
Flash: [====      ]  36.2% (used 474962 bytes from 1310720 bytes)
==== [SUCCESS] Took 12.34 seconds ====
```

### Upload

Click **→** to flash the firmware. PlatformIO auto-detects the COM port.

You should see:

```
Connecting........
Chip is ESP32-D0WD-V3
Uploading stub...
Writing at 0x00010000... (100 %)
Leaving...
Hard resetting via RTS pin...
==== [SUCCESS] Took 8.21 seconds ====
```

> **If upload fails or hangs at `Connecting...`** — hold the **BOOT** button on the board while
> PlatformIO starts uploading, release it when `Connecting...` appears in the output.
> This manually puts the ESP32 into flash mode.

> **If the COM port is not found** — add these lines to `platformio.ini` with your actual port:
> ```ini
> upload_port = COM3
> monitor_port = COM3
> ```

### Verify with Serial Monitor

Click **🔌** to open the serial monitor. You should see:

```
=== ControlDeckCore v1.1.0 ===
[Poll] 10 ms (100 Hz)
[Sliders] 5 slider(s) configured
[WiFi] Starting WiFiManager...
[WiFi] Connected. IP: 192.168.1.42
[mDNS] Registered as controldeck.local
[OTA] Ready.
[Cal] Calibration loaded from NVS
[Names] Slider names loaded from NVS
[Protocol] Slider names:
  1: Master (GPIO32)
  2: Slider 2 (GPIO33)
  3: Slider 3 (GPIO34)
  4: Slider 4 (GPIO35)
  5: Slider 5 (GPIO36)
CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Slider 2,Slider 3,Slider 4,Slider 5
[Ready]
[WebUI] http://controldeck.local or http://192.168.1.42
```

The `CDC2:` handshake line confirms the firmware is running and ready for ControlDeck to connect.

### First boot — WiFi setup

On the very first boot, WiFiManager cannot find saved credentials and opens an access point:

1. On your phone or laptop, connect to WiFi network **"ControlDeckCore"**
2. A captive portal opens automatically (or browse to `192.168.4.1`)
3. Select your home network and enter the password
4. The board reboots and connects — the serial monitor will show the assigned IP address

From this point on the board connects to your WiFi automatically on every boot.

---

## OTA Updates (after first flash)

Once the board is on your network, all future firmware updates can be pushed wirelessly — no USB needed:

```bash
# By mDNS hostname (easiest):
pio run -t upload --upload-port controldeck.local

# Or by IP address:
pio run -t upload --upload-port 192.168.x.x
```

OTA password is set in `include/config.h` → `OTA_PASSWORD` (default: `controldeck` — **change this before deploying**).

To switch `platformio.ini` permanently to OTA upload, uncomment these two lines and comment out `upload_protocol = esptool`:

```ini
; upload_protocol = espota
; upload_port = controldeck.local
```

---

## Web UI

After connecting to WiFi, open **`http://controldeck.local`** (or the device IP) in any browser.

### Calibration tab

Each slider shows a live bar with its current value, raw ADC reading, and calibration min/max.

**To calibrate a slider:**
1. Push the slider all the way to the **bottom** → click **▼ Set MIN**
2. Push the slider all the way to the **top** → click **▲ Set MAX**
3. Click **💾 Save calibration to device**

**To rename a slider:** click the slider name at the top of its card, type a new name, press Enter or click ✓. Names are saved immediately to flash.

### Settings tab

| Setting | Description |
|---------|-------------|
| **Polling rate** | Drag the slider to change how often values are sent (10–100 Hz). Click Save to persist. Applied immediately, no reboot needed. |
| **Hostname** | Change the mDNS hostname (`controldeck.local`). Requires reboot. |
| **Change WiFi** | Opens the captive portal to connect to a different network. |
| **Reset WiFi** | Erases saved credentials and reboots to the default `ControlDeckCore` AP. |

---

## Configuration

Everything is in [`include/config.h`](include/config.h) — it is the only file you ever need to edit for hardware changes.

### Slider pins

Add or remove entries to match your physical build. The count is automatically reported to the PC app via the handshake — no changes needed on the PC side.

```cpp
static const uint8_t SLIDER_PINS[] = {
    32,  // Slider 1 — ADC1_CH4
    33,  // Slider 2 — ADC1_CH5
    34,  // Slider 3 — ADC1_CH6  (input only)
    35,  // Slider 4 — ADC1_CH7  (input only)
    36,  // Slider 5 — ADC1_CH0  (input only)
};
```

> ⚠️ Only use ADC1 pins when WiFi is active. ADC2 pins (GPIO 0, 2, 4, 12–15, 25–27) conflict with WiFi and will return garbage readings. Do not use GPIO 37/38 — they are connected to the SPI flash.

### Key settings

| Setting | Default | Description |
|---------|---------|-------------|
| `ADC_SAMPLES` | `16` | Oversample count per reading — higher = less noise, slower |
| `ADC_CHANNEL_DELAY_US` | `500` | Delay between channel reads (µs) — reduces ADC crosstalk |
| `EMA_ALPHA` | `0.05` | Smoothing factor — lower is smoother but slower to respond |
| `DEADBAND_THRESHOLD` | `30` | Minimum raw change (0–4095) before a new value is sent |
| `SEND_INTERVAL_MS` | `10` | Default send rate (10ms = 100 Hz) — overridden by NVS at runtime |
| `CAL_DEFAULT_MIN` | `80` | Default ADC floor (ESP32 hardware never reaches true 0) |
| `CAL_DEFAULT_MAX` | `4010` | Default ADC ceiling (ESP32 hardware never reaches true 4095) |
| `OTA_PASSWORD` | `controldeck` | Change before deploying |
| `MDNS_HOSTNAME` | `controldeck` | Device accessible as `controldeck.local` |

---

## NVS Storage

All persistent settings are stored in the ESP32's NVS (Non-Volatile Storage) — a key-value store in flash memory that survives reboots and power cycles. NVS is **not erased by OTA firmware updates**, only by a full flash erase.

All keys live in the namespace **`cdeckcore`**.

| NVS Key | Type | Description | Default |
|---------|------|-------------|---------|
| `cal0_min` … `cal4_min` | `uint16` | Raw ADC minimum for each slider (calibration floor) | `80` |
| `cal0_max` … `cal4_max` | `uint16` | Raw ADC maximum for each slider (calibration ceiling) | `4010` |
| `name0` … `name4` | `string` | Display name for each slider | `"Slider 1"` … `"Slider 5"` |
| `poll_ms` | `uint32` | Polling interval in milliseconds | `10` (100 Hz) |
| `inv0` … `inv4` | `bool` | Invert flag for each slider | `false` |
| `hostname` | `string` | mDNS hostname used at boot | `"controldeck"` |

WiFi credentials (SSID and password) are stored separately by the WiFiManager library in its own NVS namespace and are not affected by calibration resets.

**To erase all NVS data** (full factory reset including WiFi): in PlatformIO, run:
```bash
pio run -t erase
```
This erases the entire flash — the next boot will open the WiFi captive portal as if it were brand new.

---

## CDC2 Wire Protocol

ControlDeckCore communicates with ControlDeck over USB serial at **115200 baud**, using a simple line-based ASCII protocol. All messages are newline-terminated (`\n`), UTF-8, ASCII-safe.

### Transport

| Mode | Default | Notes |
|------|---------|-------|
| USB serial | 115200 baud, 8N1 | Always available |
| WiFi (future) | — | Same protocol, different transport |

---

### Device → PC messages

#### Handshake

Sent once on boot and in response to `CMD:INFO`. The PC app must process this before interpreting data frames.

```
CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic
```

| Field | Description |
|-------|-------------|
| `SLIDERS` | Number of active sliders — authoritative slider count |
| `VERSION` | Firmware version string |
| `NAME` | Device name |
| `NAMES` | Comma-separated list of slider display names, in order |

#### Data frame

Sent continuously at the configured polling rate (default 100 Hz).

```
V:512|780|0|4095|2048
```

- Prefix `V:` identifies this as a value frame
- Values are **12-bit unsigned integers (0–4095)**, calibrated and normalised
- Pipe-delimited, one value per slider, in pin order
- Count of values always matches `SLIDERS` from the handshake

#### Acknowledgements

```
ACK:PONG        ← response to CMD:PING
ACK:SETNAME     ← response to CMD:SETNAME
ERR:UNKNOWN     ← unrecognised command
```

---

### PC → Device commands

All commands begin with `CMD:` and are newline-terminated.

| Command | Response | Description |
|---------|----------|-------------|
| `CMD:PING` | `ACK:PONG` | Connectivity check |
| `CMD:INFO` | Handshake line | Request full device info and slider names |
| `CMD:SETNAME=foo` | `ACK:SETNAME` | Set device name (future: saved to NVS) |

---

### Value mapping

ControlDeckCore outputs calibrated 12-bit values. ControlDeck maps these to volume on the PC side:

```
volume = slider_value / 4095.0
```

All calibration happens in firmware — the PC app receives clean 0–4095 values regardless of the physical slider's actual ADC range.

---

### Full example session

```
[Device boots]
→ CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic

[Continuous stream at 100 Hz]
→ V:0|512|2048|4095|1024
→ V:0|515|2051|4095|1020
→ V:2|514|2050|4090|1024

[PC sends ping]
← CMD:PING
→ ACK:PONG

[PC requests info — e.g. after reconnect]
← CMD:INFO
→ CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic
```

---

## Hardware

### Bill of Materials

| Component | Qty | Notes |
|-----------|-----|-------|
| ESP32-WROOM-32 DevKit (NodeMCU-32S) | 1 | 38-pin, CP2102 bridge, Mini-USB |
| 10kΩ linear potentiometer (B10K) | 1–5 | Slide or rotary |
| 100nF ceramic capacitor | 1–5 | One per slider wiper pin — reduces ADC noise |
| 10µF electrolytic capacitor | 1 | Across 3V3 and GND on the board — prevents rail sag |
| USB Mini-B cable | 1 | Data-capable (not charge-only) |

> **Power supply note:** All slider VCC wires must connect to the **3V3 pin** on the NodeMCU-32S board header. Add a 10µF electrolytic capacitor between the 3V3 and GND pins to prevent rail sag when multiple sliders are sampled simultaneously. Without this cap, multiple sliders will affect each other's readings.

### Wiring

Each potentiometer/slider module has 3 connections:

```
3.3V ──── VCC pin
GND  ──── GND pin
GPIO ──── Signal / wiper (OTA) pin  +  100nF cap to GND
```

5-slider wiring:

```
3V3 ─┬─[POT1]─ GND    Signal ── GPIO32
     ├─[POT2]─ GND    Signal ── GPIO33
     ├─[POT3]─ GND    Signal ── GPIO34
     ├─[POT4]─ GND    Signal ── GPIO35
     └─[POT5]─ GND    Signal ── GPIO36

10µF cap: 3V3 (+) to GND (−) directly on the board header pins
100nF cap per slider: signal pin to GND, near the ESP32 pin
```

See [`docs/hardware.md`](docs/hardware.md) for the full pinout reference.

---

## Project Structure

```
ControlDeckCore/
├── platformio.ini          # Board: esp32dev, framework: arduino
├── include/
│   ├── config.h            # ← All user settings here
│   ├── sliders.h           # Slider manager — ADC, EMA, calibration, names
│   ├── protocol.h          # CDC2 protocol encoder
│   ├── webui.h             # Web UI interface
│   └── ota.h               # WiFi + OTA + mDNS
├── src/
│   ├── main.cpp            # setup() / loop() — wires everything together
│   ├── sliders.cpp         # ADC reading, oversampling, EMA, calibration, names
│   ├── protocol.cpp        # Frame encoding, handshake, command handling
│   ├── webui.cpp           # HTTP server, calibration UI, settings UI
│   └── ota.cpp             # WiFiManager captive portal + ArduinoOTA + mDNS
└── docs/
    ├── hardware.md         # Wiring guide, BOM, full pinout
    └── protocol.md         # CDC2 wire protocol specification
```
