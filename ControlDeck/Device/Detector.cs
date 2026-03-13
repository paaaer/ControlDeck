using System.IO.Ports;

namespace ControlDeck.Device;

/// <summary>
/// Scans available serial ports for a ControlDeckCore device.
/// Prioritises Silicon Labs / CP210x ports.
/// </summary>
public static class Detector
{
    private const int ProbeTimeoutMs = 2000;

    /// <summary>
    /// Scan all ports and return the first ControlDeckCore found.
    /// Returns null if none found.
    /// </summary>
    public static Handshake? AutoDetect(int baud = 115200)
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
            var result = ProbePort(port, baud);
            if (result is not null) return result;
        }
        return null;
    }

    /// <summary>
    /// Probe a specific port for a ControlDeckCore handshake.
    /// </summary>
    public static Handshake? ProbePort(string port, int baud = 115200)
    {
        SerialPort? sp = null;
        try
        {
            sp = new SerialPort(port, baud)
            {
                ReadTimeout  = ProbeTimeoutMs,
                WriteTimeout = 1000,
                NewLine      = "\n",
            };
            sp.Open();

            // Ask device to re-send handshake
            sp.Write(Protocol.InfoCommand, 0, Protocol.InfoCommand.Length);

            // Read up to 15 lines looking for CDC2:
            for (int i = 0; i < 15; i++)
            {
                string line;
                try   { line = sp.ReadLine(); }
                catch (TimeoutException) { break; }

                var hs = Protocol.ParseHandshake(line, port);
                if (hs is not null) return hs;
            }
        }
        catch (Exception)
        {
            // Port busy, access denied, not a serial device — skip silently
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
