#pragma once
#include "BaudRates.h"

// =============================================================================
// ControlDeckCore — config.h
// All user-facing configuration lives here.
// =============================================================================

// ----------------------------------------------------------------------------
// Device identity
// ----------------------------------------------------------------------------
#define DEVICE_NAME        "ControlDeckCore"
#define FIRMWARE_VERSION   "1.2.0"

// ----------------------------------------------------------------------------
// Slider configuration
// ESP32-WROOM-32 ADC1 pins — safe with WiFi active:
//   GPIO32 (ADC1_CH4), GPIO33 (ADC1_CH5), GPIO34 (ADC1_CH6),
//   GPIO35 (ADC1_CH7), GPIO36 (ADC1_CH0)
//
// ⚠️  GPIO34, 35, 36, 39 are INPUT ONLY — no internal pull resistors.
// ⚠️  Do NOT use ADC2 pins when WiFi is active.
// ⚠️  Do NOT use GPIO37/38 — connected to flash.
//
// Remove/add pins to match your physical build.
// ----------------------------------------------------------------------------
static const uint8_t SLIDER_PINS[] = {
    32,  // Slider 1 — ADC1_CH4
    33,  // Slider 2 — ADC1_CH5
    34,  // Slider 3 — ADC1_CH6 (input only)
    35,  // Slider 4 — ADC1_CH7 (input only)
    36,  // Slider 5 — ADC1_CH0 (input only)
};
#define NUM_SLIDERS (sizeof(SLIDER_PINS) / sizeof(SLIDER_PINS[0]))

// ----------------------------------------------------------------------------
// ADC settings
// ----------------------------------------------------------------------------
#define ADC_RESOLUTION      12          // bits (0–4095)
#define ADC_SAMPLES         4           // 4× oversampling — fast reads, noise ~5 LSB
                                        // (well below deadband; the EMA handles the rest)
#define ADC_ATTENUATION     ADC_11db    // full 0–3.3V range
#define ADC_CHANNEL_DELAY_US 200        // µs between channels — discard read pre-settles
                                        // the S&H cap, so 200µs is plenty

// EMA smoothing alpha (0.0–1.0). Lower = smoother but slower to respond.
//
// Latency to 95% of target at 100 Hz:
//   α=0.05 →  58 samples = 580 ms  ← original
//   α=0.20 →  13 samples = 130 ms  ← previous
//   α=0.40 →   5 samples =  50 ms  ← now
//
// Noise budget at α=0.40 with 4× oversampling:
//   σ_raw ≈ 15 LSB  →  σ_avg ≈ 7.5 (÷√4)  →  σ_ema ≈ 3.75 (×√(α/(2-α)))
//   Deadband=25 requires a ~7σ excursion to false-trigger → negligible.
#define EMA_ALPHA           0.40f

// Deadband: ignore changes smaller than this (out of 4095).
// Slightly wider than before to absorb the extra noise from less oversampling.
// Increase toward 35 if any slider wobbles at rest.
#define DEADBAND_THRESHOLD  25

// ----------------------------------------------------------------------------
// Calibration defaults
// The ESP32 ADC never reaches true 0 or 4095 due to hardware non-linearity.
// These defaults are used until the user runs calibration via the web UI.
// ----------------------------------------------------------------------------
#define CAL_DEFAULT_MIN     80          // typical ESP32 ADC floor
#define CAL_DEFAULT_MAX     4010        // typical ESP32 ADC ceiling

// NVS namespace for storing calibration data
#define NVS_NAMESPACE       "cdeckcore"

// ----------------------------------------------------------------------------
// Serial protocol
// ----------------------------------------------------------------------------
#include "BaudRates.h"   // VALID_BAUDS[], VALID_BAUDS_COUNT, DEFAULT_BAUD
// 921600 baud cuts frame transmission from 2.34 ms → 0.29 ms.
// USB CDC is virtual serial so the baud has no effect on the physical line,
// but the ESP32 UART FIFO still drains at this rate before USB packetisation.
// CP2102 / CH340 / native-USB all support 921600.  If your adapter tops out
// at 460800 that value works too; 115200 is the safe fallback.
#define SERIAL_BAUD         DEFAULT_BAUD     // from Shared/BaudRates.h
#define SEND_INTERVAL_MS    10          // default ~100 Hz — overridden by NVS at runtime
#define SEND_INTERVAL_MIN_MS 10         // 100 Hz max
#define SEND_INTERVAL_MAX_MS 100        // 10 Hz min
// Send a frame at least this often even if nothing changed — keeps the PC
// in sync on first connect and acts as a lightweight heartbeat.
#define KEEPALIVE_INTERVAL_MS 200

// ----------------------------------------------------------------------------
// WiFi & OTA
// ----------------------------------------------------------------------------
#define OTA_PASSWORD        "controldeck"   // change before deploying
#define MDNS_HOSTNAME       "controldeck"   // accessible as controldeck.local

// ----------------------------------------------------------------------------
// Web UI (calibration interface)
// ----------------------------------------------------------------------------
#define WEBUI_PORT          80

// ----------------------------------------------------------------------------
// Onboard LED (GPIO2 on ESP32-WROOM-32 DevKit — active high)
// ----------------------------------------------------------------------------
#define LED_PIN             2
#define LED_ACTIVE_LOW      false
