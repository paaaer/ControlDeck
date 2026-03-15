#pragma once

#include <Arduino.h>

// =============================================================================
// ControlDeckCore — ota.h
// WiFi provisioning (WiFiManager captive portal) + ArduinoOTA setup.
// =============================================================================

class OTAManager {
public:
    // Call in setup(). Blocks until WiFi is connected.
    // On first boot: opens captive portal AP named DEVICE_NAME.
    // Subsequent boots: reconnects automatically.
    void begin();

    // Call every loop() to service OTA requests.
    void handle();

    // Returns true if WiFi is currently connected.
    bool isConnected() const;

    // Returns the device's IP as a string (empty if not connected).
    String ipAddress() const;
};
