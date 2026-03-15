#include <Arduino.h>
#include <Preferences.h>

#include "config.h"
#include "sliders.h"
#include "protocol.h"
#include "ota.h"
#include "webui.h"

// =============================================================================
// ControlDeckCore — main.cpp
// =============================================================================

SliderManager sliders;
OTAManager    ota;
WebUI         webui(sliders);

// Polling rate — loaded from NVS at boot, changeable via web UI
uint32_t g_sendIntervalMs = SEND_INTERVAL_MS;

static char    cmdBuf[64];
static uint8_t cmdPos = 0;
static uint32_t lastSendMs = 0;

static void ledOn()  { digitalWrite(LED_PIN, LED_ACTIVE_LOW ? LOW  : HIGH); }
static void ledOff() { digitalWrite(LED_PIN, LED_ACTIVE_LOW ? HIGH : LOW);  }
static void ledBlink(uint8_t times, uint16_t onMs = 100, uint16_t offMs = 100) {
    for (uint8_t i = 0; i < times; i++) { ledOn(); delay(onMs); ledOff(); delay(offMs); }
}

void setup() {
    Serial.begin(SERIAL_BAUD);
    delay(500);
    Serial.println("\n=== ControlDeckCore v" FIRMWARE_VERSION " ===");

    pinMode(LED_PIN, OUTPUT);
    ledOff();

    // Load polling rate from NVS
    Preferences prefs;
    prefs.begin(NVS_NAMESPACE, true);
    g_sendIntervalMs = prefs.getUInt("poll_ms", SEND_INTERVAL_MS);
    g_sendIntervalMs = constrain(g_sendIntervalMs,
                                 (uint32_t)SEND_INTERVAL_MIN_MS,
                                 (uint32_t)SEND_INTERVAL_MAX_MS);
    prefs.end();
    Serial.printf("[Poll] %u ms (%u Hz)\n", g_sendIntervalMs, 1000 / g_sendIntervalMs);

    // Sliders (loads calibration + names from NVS)
    sliders.begin();
    Serial.printf("[Sliders] %u slider(s) configured\n", sliders.count());

    // WiFi + OTA
    ledBlink(3, 200, 200);
    ota.begin();
    ledBlink(5, 50, 50);

    // Web UI
    webui.begin();

    // Send handshake with slider names
    Protocol::sendHandshake(Serial, sliders);

    Serial.println("[Ready]");
    Serial.printf("[WebUI] http://%s.local or http://%s\n",
        MDNS_HOSTNAME, ota.ipAddress().c_str());
}

void loop() {
    ota.handle();
    webui.handle();

    while (Serial.available()) {
        char c = (char)Serial.read();
        if (c == '\n' || c == '\r') {
            if (cmdPos > 0) {
                cmdBuf[cmdPos] = '\0';
                // Handle INFO here so we have access to sliders
                if (strcmp(cmdBuf, "CMD:INFO") == 0) {
                    Protocol::sendHandshake(Serial, sliders);
                } else {
                    Protocol::handleCommand(Serial, cmdBuf);
                }
                cmdPos = 0;
            }
        } else if (cmdPos < sizeof(cmdBuf) - 1) {
            cmdBuf[cmdPos++] = c;
        }
    }

    uint32_t now = millis();
    if (now - lastSendMs >= g_sendIntervalMs) {
        lastSendMs = now;
        sliders.update();
        Protocol::sendFrame(Serial, sliders);
    }
}
