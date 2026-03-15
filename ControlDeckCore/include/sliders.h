#pragma once

#include <Arduino.h>
#include <Preferences.h>
#include "config.h"

// =============================================================================
// ControlDeckCore — sliders.h
// ADC reading, oversampling, EMA filter, deadband, per-slider calibration,
// per-slider naming, and per-slider invert flag.
// All settings stored in NVS, survive reboots and OTA updates.
// =============================================================================

#define SLIDER_DEFAULT_NAME "Slider"

struct SliderCalibration {
    uint16_t rawMin;
    uint16_t rawMax;
};

class SliderManager {
public:
    SliderManager();

    void     begin();
    bool     update();

    float    getNormalized(uint8_t index) const;  // 0.0–1.0, calibrated, inverted if set
    uint16_t getRaw(uint8_t index) const;          // 0–4095, post-EMA, pre-calibration
    uint8_t  count() const { return NUM_SLIDERS; }

    // --- Calibration ---
    void              setCalMin(uint8_t index, uint16_t raw);
    void              setCalMax(uint8_t index, uint16_t raw);
    SliderCalibration getCalibration(uint8_t index) const;
    void              saveCalibration();
    void              resetCalibration();

    // --- Names ---
    String getName(uint8_t index) const;
    void   setName(uint8_t index, const String& name);
    void   resetNames();

    // --- Invert ---
    bool getInvert(uint8_t index) const;

    // Set invert flag and save immediately to NVS
    void setInvert(uint8_t index, bool inverted);

    // Reset all invert flags to false
    void resetInvert();

private:
    float    _ema[NUM_SLIDERS];
    uint16_t _last[NUM_SLIDERS];
    uint16_t _current[NUM_SLIDERS];
    float    _normalized[NUM_SLIDERS];

    SliderCalibration _cal[NUM_SLIDERS];
    String            _names[NUM_SLIDERS];
    bool              _invert[NUM_SLIDERS];
    Preferences       _prefs;

    void     loadCalibration();
    void     loadNames();
    void     loadInvert();
    uint16_t readRaw(uint8_t pin) const;
    float    applyCalibration(uint8_t index, uint16_t raw) const;
};
