namespace ControlDeck;

// =============================================================================
// Shared/BaudRates.cs — single source of truth for valid serial baud rates.
// Used by both the ControlDeck PC app and ControlDeckCore (ESP32 firmware).
// Keep BaudRates.h in sync with this file.
// =============================================================================

public static class BaudRates
{
    public static readonly int[] Valid = [115200, 230400, 460800, 576000, 921600];
    public const int Default = 921600;
}
