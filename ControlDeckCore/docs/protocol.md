# ControlDeck Wire Protocol — CDC2

## Overview

A lightweight, human-readable, line-oriented protocol for streaming slider data
from **ControlDeckCore** (ESP32-WROOM-32) to **ControlDeck** (PC app) over USB serial.

All messages are **newline-terminated (`\n`)**, UTF-8, ASCII-safe, and contain
no binary data — you can read the stream directly in any serial terminal.

---

## Version history

| Version | Changes |
|---------|---------|
| CDC2 v1.0 | Initial protocol — handshake, data frames, PING/INFO commands |
| CDC2 v1.1 | Added `NAMES` field to handshake — per-slider display names |

---

## Transport

| Mode | Baud / address | Notes |
|------|----------------|-------|
| USB serial (CP2102) | 115200 baud, 8N1 | Always available — no WiFi required |
| WiFi TCP | Planned for v2 | Same protocol, different transport |

---

## Message structure

Every message is a single line ending with `\n`. There are four types:

| Prefix | Direction | Description |
|--------|-----------|-------------|
| `CDC2:` | ESP32 → PC | Handshake — device identity and slider info |
| `V:` | ESP32 → PC | Data frame — current slider values |
| `ACK:` | ESP32 → PC | Acknowledgement of a command |
| `ERR:` | ESP32 → PC | Error response |
| `CMD:` | PC → ESP32 | Command from the PC application |

---

## ESP32 → PC messages

### Handshake

Sent automatically on boot and in response to `CMD:INFO`.
The PC app **must** process the handshake before interpreting data frames —
slider count and names are authoritative here.

```
CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic
```

All fields are semicolon-separated key=value pairs.

| Field | Type | Description |
|-------|------|-------------|
| `SLIDERS` | integer | Number of active sliders — defines how many values appear in each data frame |
| `VERSION` | string | Firmware version (semver) |
| `NAME` | string | Device name — configurable via Settings tab in web UI |
| `NAMES` | string | Comma-separated list of slider display names, one per slider, in pin order |

Example with 5 sliders:
```
CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic
```

> The `NAMES` field was added in v1.1. PC apps should treat it as optional for
> backward compatibility with v1.0 firmware — fall back to "Slider N" if absent.

---

### Data frame

Sent continuously at the configured polling rate (default 100 Hz, range 10–100 Hz).

```
V:512|780|0|4095|2048
```

- Prefix `V:` identifies this as a value frame
- Values are **12-bit unsigned integers (0–4095)**
- **Calibrated and normalised in firmware** — 0 always means slider at physical minimum, 4095 always means physical maximum, regardless of the actual ADC floor/ceiling
- **Invert applied in firmware** — if a slider is marked as inverted in the web UI, its value is already flipped before transmission. The PC app does not need to know about invert.
- Pipe-delimited `|`, one value per slider, in GPIO pin order
- Count of values always matches `SLIDERS` from the handshake
- Post-EMA-filter and post-deadband — stable output, no jitter

**Mapping to volume (PC side):**
```
volume = slider_value / 4095.0    →  0.0 to 1.0
```

---

### Acknowledgements and errors

```
ACK:PONG        ← response to CMD:PING
ACK:SETNAME     ← response to CMD:SETNAME
ERR:UNKNOWN     ← unrecognised command
```

---

## PC → ESP32 commands

All commands begin with `CMD:` and are newline-terminated (`\n`).
The ESP32 processes them between frame sends and responds on the same serial port.

| Command | Response | Description |
|---------|----------|-------------|
| `CMD:PING` | `ACK:PONG` | Round-trip connectivity check |
| `CMD:INFO` | Full handshake line | Re-request device info — useful after reconnect |
| `CMD:SETNAME=foo` | `ACK:SETNAME` | Set device name. Acknowledged but not yet persisted to NVS in v1.1. |

---

## Web API (HTTP — same device, different interface)

In addition to the serial protocol, ControlDeckCore serves a web interface on port 80.
These endpoints are used by the browser UI and may also be called by other tools.

### GET endpoints

| Endpoint | Returns | Description |
|----------|---------|-------------|
| `GET /` | HTML | Web UI page (calibration + settings tabs) |
| `GET /values` | JSON | Live slider values, calibration, names, invert flags, poll rate |
| `GET /settings` | JSON | Device info, hostname, SSID, IP, firmware version |

**`/values` response format:**
```json
{
  "raw":     [512, 780, 0, 4095, 2048],
  "norm":    [0.125, 0.190, 0.000, 1.000, 0.500],
  "cal":     [{"min":80,"max":4010}, {"min":82,"max":4008}, ...],
  "names":   ["Master", "Music", "Chat", "Game", "Mic"],
  "invert":  [false, false, true, false, false],
  "poll_ms": 10,
  "version": "1.1.0",
  "sliders": 5
}
```

| Field | Description |
|-------|-------------|
| `raw` | Current post-EMA ADC values (0–4095), before calibration |
| `norm` | Calibrated and inverted normalised values (0.0–1.0) — same as serial output |
| `cal` | Per-slider calibration min/max raw ADC values stored in NVS |
| `names` | Per-slider display names stored in NVS |
| `invert` | Per-slider invert flags stored in NVS |
| `poll_ms` | Current polling interval in milliseconds |

### POST/GET action endpoints

| Endpoint | Parameters | Description |
|----------|------------|-------------|
| `GET /cal/min?s=N` | `s` = slider index (0-based) | Set calibration minimum for slider N to current raw value |
| `GET /cal/max?s=N` | `s` = slider index (0-based) | Set calibration maximum for slider N to current raw value |
| `GET /cal/save` | — | Save all calibration data to NVS |
| `GET /cal/reset` | — | Reset all calibration to defaults, save to NVS |
| `POST /slider/rename?s=N&name=X` | `s` = index, `name` = new name (max 24 chars) | Rename slider N, saved immediately to NVS |
| `POST /slider/invert?s=N&inv=1` | `s` = index, `inv` = 1 or 0 | Set/clear invert flag for slider N, saved immediately to NVS |
| `GET /settings/pollrate?ms=N` | `ms` = interval (10–100) | Set polling rate, apply immediately, save to NVS |
| `GET /settings/save?hostname=X` | `hostname` = new name | Save hostname to NVS and reboot |
| `POST /settings/wifi` | — | Open WiFiManager captive portal to change network |
| `POST /settings/wifireset` | — | Erase WiFi credentials and reboot to default AP |

All action endpoints return JSON: `{"ok":true}` on success or `{"error":"message"}` on failure.

---

## Full example session

```
[ESP32 boots, connects to WiFi]
→ CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic

[Continuous stream at 100 Hz — slider 3 is inverted in firmware]
→ V:0|512|4095|4095|2048
→ V:0|515|4092|4095|2051
→ V:2|514|4090|4090|2048

[User moves Master slider to halfway]
→ V:2048|514|4090|4090|2048
→ V:2051|514|4090|4090|2048

[PC sends ping to verify connection]
← CMD:PING
→ ACK:PONG

[PC reconnects after cable unplug — requests fresh handshake]
← CMD:INFO
→ CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic
```

---

## Design notes

**Why not JSON for the data stream?** JSON adds ~4× overhead for no benefit on a 100 Hz stream. A 5-slider frame is ~20 bytes in CDC2 vs ~100+ bytes in JSON. At 100 Hz that's 2 KB/s vs 10 KB/s — significant on a shared USB bus.

**Why 12-bit values?** The ESP32 ADC is natively 12-bit. The original deej used a 10-bit Arduino and output 0–1023. CDC2 outputs 0–4095 for 4× finer resolution.

**Why is calibration in firmware, not the PC app?** Keeping calibration in firmware means the PC app always receives clean 0–4095 values regardless of hardware quirks. Swapping the PC app requires no recalibration.

**Why is invert in firmware, not the PC app?** Same reason — physical orientation is a hardware property of the build. Any PC app connecting to this device gets correctly oriented values automatically.

**Backward compatibility with deej:** A simple shim in the PC app can translate `V:512|780|0|4095\n` → `512|780|0|4095\n` to work with the original deej client if needed.
