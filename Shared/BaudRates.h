#pragma once
#include <stdint.h>

// =============================================================================
// Shared/BaudRates.h — single source of truth for valid serial baud rates.
// Used by both ControlDeckCore (ESP32 firmware) and the ControlDeck PC app.
// Keep BaudRates.cs in sync with this file.
// =============================================================================

static const uint32_t VALID_BAUDS[]    = { 115200, 230400, 460800, 576000, 921600 };
static const uint8_t  VALID_BAUDS_COUNT = sizeof(VALID_BAUDS) / sizeof(VALID_BAUDS[0]);
static const uint32_t DEFAULT_BAUD      = 921600;
