#pragma once

// =============================================================================
// ControlDeckCore — config.h
// All user-facing configuration lives here.
// =============================================================================

// ----------------------------------------------------------------------------
// Device identity
// ----------------------------------------------------------------------------
#define DEVICE_NAME        "ControlDeckCore"
#define FIRMWARE_VERSION   "1.0.0"

// ----------------------------------------------------------------------------
// Slider configuration
// ESP32-WROOM-32 ADC1 pins — safe to use with WiFi active:
//   GPIO32, GPIO33, GPIO34, GPIO35, GPIO36, GPIO39
//
// ⚠️  GPIO34, 35, 36, 39 are INPUT ONLY — no internal pull-up/down.
//     This is fine for potentiometers (they drive the pin directly).
// ⚠️  Do NOT use ADC2 pins (GPIO 0,2,4,12-15,25-27) when WiFi is active.
// ⚠️  Do NOT use GPIO37/38 — internally connected to flash on WROOM-32.
//
// Add or remove pins from this list to change slider count (max 6).
// ----------------------------------------------------------------------------
static const uint8_t SLIDER_PINS[] = {
    32,  // Slider 1 — ADC1_CH4
    33,  // Slider 2 — ADC1_CH5
    34,  // Slider 3 — ADC1_CH6 (input only)
    35,  // Slider 4 — ADC1_CH7 (input only)
    36,  // Slider 5 — ADC1_CH0 (input only)
    39,  // Slider 6 — ADC1_CH3 (input only)
};
#define NUM_SLIDERS (sizeof(SLIDER_PINS) / sizeof(SLIDER_PINS[0]))

// ----------------------------------------------------------------------------
// ADC settings
// ----------------------------------------------------------------------------
#define ADC_RESOLUTION      12          // bits (0–4095)
#define ADC_SAMPLES         8           // oversample per read (reduces noise)
#define ADC_ATTENUATION     ADC_11db    // full 0–3.3V range

// EMA (Exponential Moving Average) filter alpha, 0.0–1.0
// Lower = smoother but slower to respond. 0.15 is a good starting point.
#define EMA_ALPHA           0.15f

// Deadband: ignore changes smaller than this (out of 4095).
// Prevents jitter from triggering constant serial traffic.
#define DEADBAND_THRESHOLD  8

// ----------------------------------------------------------------------------
// Serial protocol
// ----------------------------------------------------------------------------
#define SERIAL_BAUD         115200
#define SEND_INTERVAL_MS    10          // ~100 Hz update rate

// ----------------------------------------------------------------------------
// WiFi & OTA
// ----------------------------------------------------------------------------
// WiFiManager will create an AP named DEVICE_NAME if no credentials are stored.
// Connect to it and configure your network via the captive portal.
#define OTA_PASSWORD        "controldeck"   // change before deploying
#define MDNS_HOSTNAME       "controldeck"   // accessible as controldeck.local

// ----------------------------------------------------------------------------
// Onboard LED (GPIO2 on ESP32-WROOM-32 DevKit — active high)
// ----------------------------------------------------------------------------
#define LED_PIN             2
#define LED_ACTIVE_LOW      false
