#pragma once

#include <Arduino.h>
#include <WebServer.h>
#include <Preferences.h>
#include "sliders.h"
#include "config.h"

extern uint32_t g_sendIntervalMs;

class WebUI {
public:
    WebUI(SliderManager& sliders);
    void begin();
    void handle();

private:
    WebServer      _server;
    SliderManager& _sliders;
    Preferences    _prefs;

    void handleRoot();
    void handleValues();
    void handleCalMin();
    void handleCalMax();
    void handleCalReset();
    void handleCalSave();
    void handleRename();        // POST /slider/rename?s=0&name=Master
    void handleInvert();        // POST /slider/invert?s=0&inv=1
    void handleSettings();
    void handleSettingsSave();
    void handlePollRate();
    void handleWifiChange();
    void handleWifiReset();

    String buildPage();
};
