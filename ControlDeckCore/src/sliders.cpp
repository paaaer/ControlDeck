#include "sliders.h"

// =============================================================================
// ControlDeckCore — sliders.cpp
// =============================================================================

SliderManager::SliderManager() {
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        _ema[i]        = 0.0f;
        _last[i]       = 0;
        _current[i]    = 0;
        _normalized[i] = 0.0f;
        _cal[i]        = { CAL_DEFAULT_MIN, CAL_DEFAULT_MAX };
        _names[i]      = String(SLIDER_DEFAULT_NAME) + " " + String(i + 1);
        _invert[i]     = false;
    }
}

void SliderManager::begin() {
    analogReadResolution(ADC_RESOLUTION);
    for (uint8_t i = 0; i < NUM_SLIDERS; i++)
        analogSetPinAttenuation(SLIDER_PINS[i], ADC_ATTENUATION);

    // Warm-up — cycle all pins to settle the S&H capacitor
    for (uint8_t warmup = 0; warmup < 3; warmup++) {
        for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
            readRaw(SLIDER_PINS[i]);
            delayMicroseconds(ADC_CHANNEL_DELAY_US);
        }
    }
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        _ema[i]        = (float)readRaw(SLIDER_PINS[i]);
        _current[i]    = (uint16_t)_ema[i];
        _last[i]       = _current[i];
        _normalized[i] = applyCalibration(i, _current[i]);
        delayMicroseconds(ADC_CHANNEL_DELAY_US);
    }

    loadCalibration();
    loadNames();
    loadInvert();
}

bool SliderManager::update() {
    bool changed = false;
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        uint16_t raw = readRaw(SLIDER_PINS[i]);
        _ema[i]     = EMA_ALPHA * (float)raw + (1.0f - EMA_ALPHA) * _ema[i];
        _current[i] = (uint16_t)(_ema[i] + 0.5f);

        int32_t delta = (int32_t)_current[i] - (int32_t)_last[i];
        if (delta < 0) delta = -delta;
        if (delta >= DEADBAND_THRESHOLD) {
            _last[i]       = _current[i];
            _normalized[i] = applyCalibration(i, _current[i]);
            changed        = true;
        }
    }
    return changed;
}

float    SliderManager::getNormalized(uint8_t index) const { return index < NUM_SLIDERS ? _normalized[index] : 0.0f; }
uint16_t SliderManager::getRaw(uint8_t index)        const { return index < NUM_SLIDERS ? _current[index]   : 0;    }

// --- Calibration -----------------------------------------------------------

void SliderManager::setCalMin(uint8_t index, uint16_t raw) {
    if (index >= NUM_SLIDERS) return;
    _cal[index].rawMin = raw;
    Serial.printf("[Cal] Slider %u (%s) min = %u\n", index + 1, _names[index].c_str(), raw);
}

void SliderManager::setCalMax(uint8_t index, uint16_t raw) {
    if (index >= NUM_SLIDERS) return;
    _cal[index].rawMax = raw;
    Serial.printf("[Cal] Slider %u (%s) max = %u\n", index + 1, _names[index].c_str(), raw);
}

SliderCalibration SliderManager::getCalibration(uint8_t index) const {
    return index < NUM_SLIDERS ? _cal[index] : SliderCalibration{ CAL_DEFAULT_MIN, CAL_DEFAULT_MAX };
}

void SliderManager::saveCalibration() {
    _prefs.begin(NVS_NAMESPACE, false);
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        char kMin[16], kMax[16];
        snprintf(kMin, sizeof(kMin), "cal%u_min", i);
        snprintf(kMax, sizeof(kMax), "cal%u_max", i);
        _prefs.putUShort(kMin, _cal[i].rawMin);
        _prefs.putUShort(kMax, _cal[i].rawMax);
    }
    _prefs.end();
    Serial.println("[Cal] Saved to NVS");
}

void SliderManager::resetCalibration() {
    for (uint8_t i = 0; i < NUM_SLIDERS; i++)
        _cal[i] = { CAL_DEFAULT_MIN, CAL_DEFAULT_MAX };
    saveCalibration();
    Serial.println("[Cal] Reset to defaults");
}

// --- Names -----------------------------------------------------------------

String SliderManager::getName(uint8_t index) const {
    return index < NUM_SLIDERS ? _names[index] : "";
}

void SliderManager::setName(uint8_t index, const String& name) {
    if (index >= NUM_SLIDERS) return;
    String s = name; s.trim();
    if (s.length() == 0) s = String(SLIDER_DEFAULT_NAME) + " " + String(index + 1);
    if (s.length() > 24)  s = s.substring(0, 24);
    _names[index] = s;
    _prefs.begin(NVS_NAMESPACE, false);
    char key[16]; snprintf(key, sizeof(key), "name%u", index);
    _prefs.putString(key, s);
    _prefs.end();
    Serial.printf("[Names] Slider %u renamed: %s\n", index + 1, s.c_str());
}

void SliderManager::resetNames() {
    _prefs.begin(NVS_NAMESPACE, false);
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        _names[i] = String(SLIDER_DEFAULT_NAME) + " " + String(i + 1);
        char key[16]; snprintf(key, sizeof(key), "name%u", i);
        _prefs.putString(key, _names[i]);
    }
    _prefs.end();
    Serial.println("[Names] Reset to defaults");
}

// --- Invert ----------------------------------------------------------------

bool SliderManager::getInvert(uint8_t index) const {
    return index < NUM_SLIDERS ? _invert[index] : false;
}

void SliderManager::setInvert(uint8_t index, bool inverted) {
    if (index >= NUM_SLIDERS) return;
    _invert[index] = inverted;

    // Immediately recalculate normalised value so change is instant
    _normalized[index] = applyCalibration(index, _current[index]);

    // Save to NVS
    _prefs.begin(NVS_NAMESPACE, false);
    char key[16]; snprintf(key, sizeof(key), "inv%u", index);
    _prefs.putBool(key, inverted);
    _prefs.end();

    Serial.printf("[Invert] Slider %u (%s) invert = %s\n",
        index + 1, _names[index].c_str(), inverted ? "ON" : "OFF");
}

void SliderManager::resetInvert() {
    _prefs.begin(NVS_NAMESPACE, false);
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        _invert[i] = false;
        char key[16]; snprintf(key, sizeof(key), "inv%u", i);
        _prefs.putBool(key, false);
    }
    _prefs.end();
    Serial.println("[Invert] All sliders reset to normal");
}

// --- Private ---------------------------------------------------------------

void SliderManager::loadCalibration() {
    _prefs.begin(NVS_NAMESPACE, true);
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        char kMin[16], kMax[16];
        snprintf(kMin, sizeof(kMin), "cal%u_min", i);
        snprintf(kMax, sizeof(kMax), "cal%u_max", i);
        uint16_t mn = _prefs.getUShort(kMin, CAL_DEFAULT_MIN);
        uint16_t mx = _prefs.getUShort(kMax, CAL_DEFAULT_MAX);
        if (mn < mx) _cal[i] = { mn, mx };
    }
    _prefs.end();
    Serial.println("[Cal] Loaded from NVS");
}

void SliderManager::loadNames() {
    _prefs.begin(NVS_NAMESPACE, true);
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        char key[16]; snprintf(key, sizeof(key), "name%u", i);
        String def = String(SLIDER_DEFAULT_NAME) + " " + String(i + 1);
        _names[i] = _prefs.getString(key, def);
    }
    _prefs.end();
    Serial.println("[Names] Loaded from NVS");
}

void SliderManager::loadInvert() {
    _prefs.begin(NVS_NAMESPACE, true);
    for (uint8_t i = 0; i < NUM_SLIDERS; i++) {
        char key[16]; snprintf(key, sizeof(key), "inv%u", i);
        _invert[i] = _prefs.getBool(key, false);
    }
    _prefs.end();
    Serial.println("[Invert] Loaded from NVS");
}

uint16_t SliderManager::readRaw(uint8_t pin) const {
    analogRead(pin);                          // discard — flush S&H cap from previous channel
    delayMicroseconds(ADC_CHANNEL_DELAY_US);  // let cap settle to new pin voltage
    uint32_t sum = 0;
    for (uint8_t s = 0; s < ADC_SAMPLES; s++) {
        sum += analogRead(pin);
        delayMicroseconds(20);
    }
    return (uint16_t)(sum / ADC_SAMPLES);
}

float SliderManager::applyCalibration(uint8_t index, uint16_t raw) const {
    uint16_t mn = _cal[index].rawMin;
    uint16_t mx = _cal[index].rawMax;
    if (mx <= mn) return 0.0f;
    float v = constrain(((float)raw - mn) / ((float)mx - mn), 0.0f, 1.0f);
    // Apply invert — flips the slider direction
    return _invert[index] ? 1.0f - v : v;
}
