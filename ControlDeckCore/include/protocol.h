#pragma once

#include <Arduino.h>
#include "config.h"
#include "sliders.h"

// =============================================================================
// ControlDeckCore — protocol.h
// CDC2 wire protocol — now includes slider names in handshake.
//
// Handshake:
//   CDC2:SLIDERS=5;VERSION=1.1.0;NAME=ControlDeckCore;NAMES=Master,Music,Chat,Game,Mic
//
// Data frame (unchanged):
//   V:512|780|0|4095|2048
// =============================================================================

class Protocol {
public:
    static void sendHandshake(Stream& port, const SliderManager& sliders);
    static void sendFrame(Stream& port, const SliderManager& sliders);
    static bool handleCommand(Stream& port, const char* line);
    static void buildHandshake(char* buf, size_t bufLen, const SliderManager& sliders);
};
