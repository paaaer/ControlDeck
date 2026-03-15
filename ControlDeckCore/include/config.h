#pragma once

// =============================================================================
// ControlDeckCore — config.h
// All user-facing configuration lives here.
// =============================================================================

// ----------------------------------------------------------------------------
// Device identity
// ----------------------------------------------------------------------------
#define DEVICE_NAME        "ControlDeckCore"
#define FIRMWARE_VERSION   "1.1.0"

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
#define ADC_SAMPLES         16          // oversample per read — more = less noise
#define ADC_ATTENUATION     ADC_11db    // full 0–3.3V range
#define ADC_CHANNEL_DELAY_US 500        // delay between channels — reduces crosstalk

// EMA smoothing alpha (0.0–1.0). Lower = smoother, slower to respond.
#define EMA_ALPHA           0.05f

// Deadband: ignore changes smaller than this (out of 4095).
// Increase if values wobble at rest.
#define DEADBAND_THRESHOLD  30

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
#define SERIAL_BAUD         115200
#define SEND_INTERVAL_MS    10          // default ~100 Hz — overridden by NVS at runtime
#define SEND_INTERVAL_MIN_MS 10         // 100 Hz max
#define SEND_INTERVAL_MAX_MS 100        // 10 Hz min

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
