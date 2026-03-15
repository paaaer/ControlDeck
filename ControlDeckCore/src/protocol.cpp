#include "protocol.h"

// =============================================================================
// ControlDeckCore — protocol.cpp
// Handshake now includes slider names:
//   CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic
// =============================================================================

void Protocol::buildHandshake(char* buf, size_t bufLen, const SliderManager& sliders) {
    // Build names list
    String names = "";
    for (uint8_t i = 0; i < sliders.count(); i++) {
        if (i) names += ",";
        names += sliders.getName(i);
    }

    snprintf(buf, bufLen,
        "CDC2:SLIDERS=%u;VERSION=%s;NAME=%s;NAMES=%s",
        (unsigned)NUM_SLIDERS,
        FIRMWARE_VERSION,
        DEVICE_NAME,
        names.c_str()
    );
}

void Protocol::sendHandshake(Stream& port, const SliderManager& sliders) {
    char buf[256];
    buildHandshake(buf, sizeof(buf), sliders);
    port.println(buf);

    // Also log names to serial for debugging
    Serial.println("[Protocol] Slider names:");
    for (uint8_t i = 0; i < sliders.count(); i++) {
        Serial.printf("  %u: %s (GPIO%u)\n", i + 1,
            sliders.getName(i).c_str(), SLIDER_PINS[i]);
    }
}

void Protocol::sendFrame(Stream& port, const SliderManager& sliders) {
    char buf[64];
    int  pos = 0;

    buf[pos++] = 'V';
    buf[pos++] = ':';

    for (uint8_t i = 0; i < sliders.count(); i++) {
        if (i > 0) buf[pos++] = '|';
        uint16_t val = (uint16_t)(sliders.getNormalized(i) * 4095.0f + 0.5f);
        pos += snprintf(buf + pos, sizeof(buf) - pos - 2, "%u", val);
    }

    buf[pos++] = '\n';
    buf[pos]   = '\0';

    port.write((const uint8_t*)buf, pos);
}

bool Protocol::handleCommand(Stream& port, const char* line) {
    if (strncmp(line, "CMD:", 4) != 0) return false;
    const char* cmd = line + 4;

    if (strcmp(cmd, "PING") == 0)  { port.println("ACK:PONG");  return true; }
    if (strcmp(cmd, "INFO") == 0)  { /* handled in main with sliders ref */ return true; }

    port.println("ERR:UNKNOWN");
    return true;
}
