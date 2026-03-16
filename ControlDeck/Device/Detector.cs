using System.IO.Ports;
using System.Linq;

namespace ControlDeck.Device;

/// <summary>
/// Scans available serial ports for a ControlDeckCore device.
/// Prioritises Silicon Labs / CP210x ports.
/// </summary>
public static class Detector
{
    // 300 ms is plenty — the device responds in < 100 ms if it's there.
    // The old 2000 ms value meant 5 bauds × 2 s = 10 s worst-case per port.
    private const int ProbeTimeoutMs = 300;

    // Try fast baud first (new firmware), fall back to 115200 (old firmware /
    // chips that don't support 921600 cleanly such as some CH340 variants).
    // Order: highest to lowest so we connect at the best rate first.
    private static readonly int[] ProbeBauds = [..BaudRates.Valid.Reverse()];

    /// <summary>
    /// Scan all ports and return the first ControlDeckCore found.
    /// Tries each supported baud rate per port before moving on.
    /// </summary>
    public static Handshake? AutoDetect(int baud = BaudRates.Default)
    {
        var ports = SerialPort.GetPortNames();

        // Heuristic: sort COM ports with lower numbers first (CP2102 usually gets low numbers)
        Array.Sort(ports, (a, b) =>
        {
            int na = ExtractPortNumber(a);
            int nb = ExtractPortNumber(b);
            return na.CompareTo(nb);
        });

        foreach (var port in ports)
        {
            // Honour explicit baud first, then try the fallback list
            var bauds = baud == ProbeBauds[0]
                ? ProbeBauds
                : new[] { baud }.Concat(ProbeBauds).Distinct().ToArray();

            foreach (var b in bauds)
            {
                var result = ProbePort(port, b);
                if (result is not null) return result;
            }
        }
        return null;
    }

    /// <summary>
    /// Probe a specific port for a ControlDeckCore handshake.
    /// </summary>
    public static Handshake? ProbePort(string port, int baud = BaudRates.Default)
    {
        SerialPort? sp = null;
        try
        {
            System.Diagnostics.Debug.WriteLine($"[Detector] Probing {port} @ {baud}");
            sp = new SerialPort(port, baud)
            {
                ReadTimeout  = ProbeTimeoutMs,
                WriteTimeout = 1000,
                NewLine      = "\n",
                // Prevent toggling DTR/RTS on Open() — many ESP32 boards use
                // those lines to drive the EN reset circuit, which would reboot
                // the device and make it unreachable for several seconds.
                DtrEnable    = false,
                RtsEnable    = false,
            };
            sp.Open();

            // Discard any bytes already in the OS receive buffer from a previous
            // probe or from frames that arrived before we opened the port.
            sp.DiscardInBuffer();

            // Ask device to re-send handshake
            sp.Write(Protocol.InfoCommand, 0, Protocol.InfoCommand.Length);

            // Read lines looking for CDC2:.
            // Strategy:
            //   - At the wrong baud, \n rarely appears in framing garbage so
            //     each ReadLine burns the full timeout.  Break on the FIRST
            //     timeout to keep the scan fast (~300 ms per wrong baud).
            //   - At the correct baud we may read V: value frames before the
            //     CDC2: handshake arrives (device sends a keepalive every
            //     200 ms; CMD:INFO response follows within one loop tick ~10 ms).
            //     Once we've seen any valid line we know the baud is right, so
            //     continue through timeouts rather than bailing out early.
            bool receivedAnyData = false;
            for (int i = 0; i < 16; i++)
            {
                string line;
                try   { line = sp.ReadLine(); }
                catch (TimeoutException)
                {
                    System.Diagnostics.Debug.WriteLine($"[Detector] {port} @ {baud}: timeout on read {i} (receivedData={receivedAnyData})");
                    if (!receivedAnyData) break;  // wrong baud — exit quickly

                    // Right baud but CDC2 not yet arrived (firmware may have been
                    // busy in webui.handle() when CMD:INFO arrived).  Resend once
                    // after the first timeout to make sure the device got it.
                    if (i == 1)
                        sp.Write(Protocol.InfoCommand, 0, Protocol.InfoCommand.Length);
                    continue;
                }

                receivedAnyData = true;
                System.Diagnostics.Debug.WriteLine($"[Detector] {port} @ {baud}: got '{line.Trim()}'");
                var hs = Protocol.ParseHandshake(line, port, baud);
                if (hs is not null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Detector] {port} @ {baud}: handshake OK — {hs.Name} fw{hs.Version}");
                    return hs;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Detector] {port} @ {baud}: exception — {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            sp?.Close();
            sp?.Dispose();
        }
        return null;
    }

    public static string[] ListPorts() => SerialPort.GetPortNames();

    private static int ExtractPortNumber(string portName)
    {
        var digits = new string(portName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int n) ? n : 999;
    }
}
