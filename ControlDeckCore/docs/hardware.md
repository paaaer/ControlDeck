# ControlDeckCore — Hardware Guide

## Bill of Materials

| Component | Qty | Notes |
|-----------|-----|-------|
| ESP32-WROOM-32 DevKit (NodeMCU-32S) | 1 | 38-pin, CP2102 USB-UART bridge, Mini-USB |
| 10kΩ linear potentiometer / slide pot module (B10K) | 1–5 | Slide or rotary. Modules with PCB carrier (e.g. "Slide Pot" red module) work well. |
| 100nF (0.1µF) ceramic capacitor | 1–5 | One per slider — reduces ADC noise on the signal line. Marked **104** on the body. |
| 10µF electrolytic capacitor | 1 | Across the 3V3 and GND pins on the board — prevents 3.3V rail sag under load |
| USB Mini-B cable | 1 | Must be a data cable, not a charge-only cable |

> **CP2102 driver:** Windows 10/11 installs it automatically on first plug-in.
> Appears in Device Manager as **Silicon Labs CP210x USB to UART Bridge (COMx)**.

---

## Power supply — important

All slider VCC pins must connect to the **3V3 pin** on the NodeMCU-32S board header.

The ESP32 ADC measures voltage relative to its supply rail. If the rail sags when
multiple sliders load it simultaneously, every slider reading drops together — causing
sliders to affect each other even with no actual crosstalk on the ADC lines.

**Fix:** solder a **10µF electrolytic capacitor** directly across the 3V3 and GND
header pins on the board. This acts as a local charge reservoir and prevents sag.

```
NodeMCU-32S top-left header pins:

  [ 3V3 ] [ GND ] [ EN ] ...
     +        −
     └──[10µF]┘    ← solder here, positive leg to 3V3
```

> ⚠️ Electrolytic capacitors are polarised — the **longer leg is positive (3V3)**, the
> shorter leg (marked with a stripe) is negative (GND). Reverse polarity will damage the cap.

---

## ADC pin selection

Only **ADC1** pins are safe to use when WiFi is active. ADC2 shares hardware with
the WiFi radio and returns unpredictable values when WiFi is enabled.

| Slider | GPIO | ADC channel | Pin type | Notes |
|--------|------|-------------|----------|-------|
| 1 | 32 | ADC1_CH4 | Input + Output | — |
| 2 | 33 | ADC1_CH5 | Input + Output | — |
| 3 | 34 | ADC1_CH6 | **Input only** | No internal pull resistors |
| 4 | 35 | ADC1_CH7 | **Input only** | No internal pull resistors |
| 5 | 36 | ADC1_CH0 | **Input only** | No internal pull resistors |

> ⚠️ **Input-only pins (34, 35, 36)** have no internal pull-up or pull-down resistors.
> This is fine for potentiometers — the pot drives the pin directly.

> ⚠️ **Do NOT use ADC2 pins** (GPIO 0, 2, 4, 12–15, 25–27) when WiFi is active — they
> return garbage or interfere with WiFi operation.

> ⚠️ **Do NOT use GPIO 37 or 38** — these are internally connected to the SPI flash
> chip on the WROOM-32 module and are not available as general I/O.

---

## Wiring — slide pot modules

The red "Slide Pot" modules used in this build have a 6-pin header split into
two groups of 3. The relevant group (nearest the edge) is labelled:

```
GND
VCC
OTA    ← this is the wiper / signal output — connect to ESP32 GPIO
```

Wire each module:

```
ESP32 3V3  ──── module VCC
ESP32 GND  ──── module GND
ESP32 GPIO ──── module OTA (wiper)

100nF cap between OTA pin and GND — solder directly across those two pins on the module header
```

### Full 5-slider wiring diagram

```
NodeMCU-32S                     Slide pot modules
─────────────                   ─────────────────
3V3  ──────────┬──── VCC [POT1]    OTA ──── GPIO32
               ├──── VCC [POT2]    OTA ──── GPIO33
               ├──── VCC [POT3]    OTA ──── GPIO34
               ├──── VCC [POT4]    OTA ──── GPIO35
               └──── VCC [POT5]    OTA ──── GPIO36

GND  ──────────┬──── GND [POT1]
               ├──── GND [POT2]
               ├──── GND [POT3]
               ├──── GND [POT4]
               └──── GND [POT5]

Each OTA pin: 100nF cap to GND (solder on module header)
3V3 to GND:   10µF electrolytic cap on the NodeMCU-32S board header pins
```

---

## ADC noise and crosstalk

The ESP32 ADC has two known hardware limitations that require mitigation:

### 1. Crosstalk between channels

The ESP32 uses a single shared sample-and-hold (S&H) capacitor for all ADC channels.
When switching channels, the cap retains charge from the previous channel, contaminating
the first reading on the new channel.

**Firmware mitigation:**
- Each channel read discards the first sample and waits 500µs before taking real samples
- 16 samples are averaged per reading (oversampling)
- EMA filter with α=0.05 smooths remaining noise
- Deadband of 30 counts suppresses jitter at rest

**Hardware mitigation:**
- 100nF cap on each wiper pin gives the S&H cap a low-impedance charge source

### 2. Non-linearity at rail edges

The ESP32 ADC never reaches true 0 or 4095 — it saturates at approximately 80 (bottom)
and 4010 (top). This is corrected by the per-slider calibration system.

Default calibration values in `config.h`:
```cpp
#define CAL_DEFAULT_MIN  80     // typical ESP32 ADC floor
#define CAL_DEFAULT_MAX  4010   // typical ESP32 ADC ceiling
```

Run the calibration procedure in the web UI to get exact values for your hardware.

---

## Slider orientation and invert

If your physical build mounts sliders upside-down (maximum position at the bottom),
enable the **Invert** toggle for that slider in the web UI calibration tab.

Inversion is applied in firmware before transmission — the PC app always receives
correctly-oriented values. The invert state is stored in NVS and survives reboots.

> **Calibration tip:** calibrate before enabling invert. Set MIN with the slider at
> its physical bottom position and MAX at its physical top position — regardless of
> which end produces the higher ADC value. The firmware handles the rest.

---

## Adding or removing sliders

Edit `include/config.h` and change the `SLIDER_PINS` array:

```cpp
static const uint8_t SLIDER_PINS[] = {
    32,  // Slider 1
    33,  // Slider 2
    34,  // Slider 3
    35,  // Slider 4
    36,  // Slider 5  ← remove this line for 4 sliders
};
```

Reflash (USB or OTA). The PC app reads slider count from the handshake automatically —
no changes needed on the PC side. Slider names, calibration and invert data for removed
sliders remain in NVS but are ignored until that slider index is re-added.

---

## First flash

```bash
cd ControlDeckCore
pio run -t upload          # flash via Mini-USB (CP2102)
pio device monitor         # open serial monitor at 115200 baud
```

On first boot the device opens a WiFi access point named **ControlDeckCore**.
Connect from your phone or laptop, enter your network credentials via the captive portal.
The device reboots and connects automatically on all future boots.

After connecting to WiFi, open **`http://controldeck.local`** in your browser
to access the calibration and settings web UI.

---

## OTA firmware updates

After the first USB flash, push all future updates wirelessly:

```bash
# By mDNS hostname:
pio run -t upload --upload-port controldeck.local

# Or by IP address:
pio run -t upload --upload-port 192.168.x.x
```

The OTA password is set in `include/config.h` → `OTA_PASSWORD`.
Default is `controldeck` — **change this before building a permanent installation.**

To make OTA the default upload method, edit `platformio.ini`:

```ini
; Comment out USB upload:
; upload_protocol = esptool

; Uncomment OTA upload:
upload_protocol = espota
upload_port = controldeck.local
```

---

## Factory reset

To erase all flash including WiFi credentials, calibration, names and settings:

```bash
pio run -t erase
```

The next boot will open the **ControlDeckCore** WiFi AP as if the device is brand new.
All NVS data (calibration, names, invert flags, hostname, poll rate) will reset to defaults.
