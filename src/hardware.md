# ControlDeckCore — Hardware Guide

## Bill of Materials

| Component                   | Qty | Notes                                        |
|-----------------------------|-----|----------------------------------------------|
| ESP32-WROOM-32 DevKit       | 1   | 38-pin DevKit, CP2102 USB-UART bridge        |
| 10kΩ linear potentiometer   | 1–6 | B10K, 3-pin slide or rotary                 |
| 0.1µF ceramic capacitor     | 1–6 | One per slider, ADC noise bypass             |
| USB-A to Mini-B cable       | 1   | Standard Mini-USB data cable                 |

> **CP2102 driver**: Windows 10/11 usually installs it automatically.
> Shows as *"Silicon Labs CP210x USB to UART Bridge"* in Device Manager. ✅

---

## ADC1 Pins — use these for sliders (WiFi-safe)

| Slider | GPIO | ADC Channel | Pin type       |
|--------|------|-------------|----------------|
| 1      | 32   | ADC1_CH4    | Input/Output   |
| 2      | 33   | ADC1_CH5    | Input/Output   |
| 3      | 34   | ADC1_CH6    | **Input only** |
| 4      | 35   | ADC1_CH7    | **Input only** |
| 5      | 36   | ADC1_CH0    | **Input only** |
| 6      | 39   | ADC1_CH3    | **Input only** |

> ⚠️ **GPIO 34, 35, 36, 39** are input-only — no internal pull-up/down. Fine for potentiometers.
> ⚠️ **Do NOT use ADC2 pins** (GPIO 0,2,4,12-15,25-27) when WiFi is active.
> ⚠️ **Do NOT use GPIO 37/38** — internally connected to flash on WROOM-32.

---

## Wiring a Slider (potentiometer)

```
   3.3V ──── [Left pin  ]
   GND  ──── [Right pin ]
   GPIO ──── [Wiper/Mid ]
```

Add a **100nF capacitor** between the wiper and GND to reduce ADC noise.

### Full 6-slider wiring

```
3.3V ─┬─[POT1 ends]─ GND      Wiper ── GPIO32
      ├─[POT2 ends]─ GND      Wiper ── GPIO33
      ├─[POT3 ends]─ GND      Wiper ── GPIO34
      ├─[POT4 ends]─ GND      Wiper ── GPIO35
      ├─[POT5 ends]─ GND      Wiper ── GPIO36
      └─[POT6 ends]─ GND      Wiper ── GPIO39

Each wiper also has 100nF cap to GND.
```

---

## Adding/Removing Sliders

Edit `include/config.h`:

```cpp
static const uint8_t SLIDER_PINS[] = {
    32, 33, 34,   // always safe
    35, 36, 39,   // remove these for fewer sliders
};
```

Reflash via USB or OTA. The PC app reads slider count from the handshake automatically.

---

## First Flash

```bash
pio run -t upload          # flash via Mini-USB / CP2102
pio device monitor         # verify handshake output
```

On first boot: connect to WiFi AP **"ControlDeckCore"** and enter your network credentials.

---

## OTA Updates

```bash
pio run -t upload --upload-port controldeck.local
```

OTA password set in `config.h` → `OTA_PASSWORD` (default: `controldeck` — **change this!**)
