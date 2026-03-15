#include "ota.h"
#include "config.h"

#include <WiFi.h>
#include <WiFiManager.h>
#include <ArduinoOTA.h>
#include <ESPmDNS.h>
#include <Preferences.h>

// =============================================================================
// ControlDeckCore — ota.cpp
// Reads hostname from NVS (set via web UI settings tab).
// Falls back to MDNS_HOSTNAME defined in config.h if not set.
// =============================================================================

// Resolved hostname — populated in begin(), used by ipAddress() etc.
static String _resolvedHostname;

void OTAManager::begin() {
    // Load hostname from NVS — allows web UI to change it persistently
    Preferences prefs;
    prefs.begin(NVS_NAMESPACE, true);
    String hostname = prefs.getString("hostname", MDNS_HOSTNAME);
    prefs.end();
    _resolvedHostname = hostname;

    Serial.printf("[WiFi] Hostname: %s\n", hostname.c_str());
    Serial.println("[WiFi] Starting WiFiManager...");

    WiFiManager wm;
    wm.setConfigPortalTimeout(120);

    bool connected = wm.autoConnect(DEVICE_NAME);
    if (!connected) {
        Serial.println("[WiFi] Failed to connect — rebooting");
        delay(1000);
        ESP.restart();
    }

    Serial.print("[WiFi] Connected. IP: ");
    Serial.println(WiFi.localIP());

    // mDNS with resolved hostname
    if (MDNS.begin(hostname.c_str())) {
        Serial.printf("[mDNS] Registered as %s.local\n", hostname.c_str());
        MDNS.addService("controldeck", "tcp", 80);
    } else {
        Serial.println("[mDNS] Failed to start");
    }

    // ArduinoOTA
    ArduinoOTA.setHostname(hostname.c_str());
    ArduinoOTA.setPassword(OTA_PASSWORD);

    ArduinoOTA.onStart([]() {
        String type = (ArduinoOTA.getCommand() == U_FLASH) ? "firmware" : "filesystem";
        Serial.println("[OTA] Start: " + type);
    });
    ArduinoOTA.onEnd([]() {
        Serial.println("\n[OTA] Complete — rebooting");
    });
    ArduinoOTA.onProgress([](unsigned int progress, unsigned int total) {
        Serial.printf("[OTA] Progress: %u%%\r", (progress * 100) / total);
    });
    ArduinoOTA.onError([](ota_error_t error) {
        Serial.printf("[OTA] Error[%u]: ", error);
        switch (error) {
            case OTA_AUTH_ERROR:    Serial.println("Auth Failed");    break;
            case OTA_BEGIN_ERROR:   Serial.println("Begin Failed");   break;
            case OTA_CONNECT_ERROR: Serial.println("Connect Failed"); break;
            case OTA_RECEIVE_ERROR: Serial.println("Receive Failed"); break;
            case OTA_END_ERROR:     Serial.println("End Failed");     break;
        }
    });

    ArduinoOTA.begin();
    Serial.printf("[OTA] Ready. Push via: pio run -t upload --upload-port %s\n",
        WiFi.localIP().toString().c_str());
}

void OTAManager::handle() {
    ArduinoOTA.handle();
}

bool OTAManager::isConnected() const {
    return WiFi.status() == WL_CONNECTED;
}

String OTAManager::ipAddress() const {
    if (!isConnected()) return "";
    return WiFi.localIP().toString();
}
