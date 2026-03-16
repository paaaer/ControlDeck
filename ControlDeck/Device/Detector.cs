using System.IO.Ports;
using System.Linq;

namespace ControlDeck.Device;

/// <summary>
/// An open, confirmed connection to a ControlDeckCore device.
/// The caller must dispose this to close the port if it is not handed to a SerialReader.
/// </summary>
public sealed class OpenConnection(Handshake Handshake, SerialPort Port) : IDisposable
{
    public Handshake  Handshake { get; } = Handshake;
    public SerialPort Port      { get; } = Port;
    public void Dispose() { try { Port.Close(); } catch { /* ignore */ } Port.Dispose(); }
}

/// <summary>
/// Scans available serial ports for a ControlDeckCore device.
/// Returns an <see cref="OpenConnection"/> so the port is never closed and
/// reopened between detection and SerialReader startup.
/// </summary>
public static class Detector
{
    private const int ProbeTimeoutMs = 400;
    private static readonly int[] ProbeBauds = [..BaudRates.Valid.Reverse()];

    public static OpenConnection? AutoDetect(int baud = BaudRates.Default)
    {
        var ports = SerialPort.GetPortNames();
        Array.Sort(ports, (a, b) => ExtractPortNumber(a).CompareTo(ExtractPortNumber(b)));

        foreach (var port in ports)
        {
            var bauds = baud == ProbeBauds[0]
                ? ProbeBauds
                : new[] { baud }.Concat(ProbeBauds).Distinct().ToArray();

            foreach (var b in bauds)
            {
                var conn = OpenPort(port, b);
                if (conn is not null) return conn;
            }
        }
        return null;
    }

    /// <summary>
    /// Probe a specific port/baud. On success returns an open
    /// <see cref="OpenConnection"/> — caller owns it.  Returns null on failure.
    ///
    /// Detection strategy (two-tier):
    ///   1. Send CMD:INFO and wait for CDC2: handshake — ideal path.
    ///   2. If only V: value frames arrive (CDC2 delayed e.g. by webui.handle),
    ///      accept the V: frame as proof of device presence and build a
    ///      provisional handshake from it.  SerialReader will fire the real
    ///      Connected event once CMD:INFO is processed by the firmware.
    /// </summary>
    public static OpenConnection? OpenPort(string port, int baud = BaudRates.Default)
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
                DtrEnable    = false,
                RtsEnable    = false,
            };
            sp.Open();
            sp.DiscardInBuffer();
            sp.Write(Protocol.InfoCommand, 0, Protocol.InfoCommand.Length);

            Handshake?  confirmedHs  = null;
            float[]?    valueFrame   = null;

            for (int i = 0; i < 8; i++)
            {
                string line;
                try   { line = sp.ReadLine(); }
                catch (TimeoutException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Detector] {port} @ {baud}: timeout read {i}");
                    // Wrong baud — no data at all → give up quickly
                    if (valueFrame is null && confirmedHs is null) break;
                    // Right baud, CDC2 delayed — resend CMD:INFO and keep waiting
                    sp.Write(Protocol.InfoCommand, 0, Protocol.InfoCommand.Length);
                    continue;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Detector] {port} @ {baud}: read error — {ex.GetType().Name}: {ex.Message}");
                    break;
                }

                System.Diagnostics.Debug.WriteLine($"[Detector] {port} @ {baud}: rx '{line.Trim()}'");

                // Best case: CDC2 handshake received
                var hs = Protocol.ParseHandshake(line, port, baud);
                if (hs is not null)
                {
                    confirmedHs = hs;
                    System.Diagnostics.Debug.WriteLine(
                        $"[Detector] {port} @ {baud}: handshake OK — {hs.Name} fw{hs.Version}");
                    var conn = new OpenConnection(hs, sp);
                    sp = null;
                    return conn;
                }

                // Fallback: V: value frame — correct baud confirmed, device is alive
                if (valueFrame is null)
                {
                    valueFrame = Protocol.ParseFrame(line);
                    if (valueFrame is not null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[Detector] {port} @ {baud}: V: frame confirmed — {valueFrame.Length} sliders, waiting for CDC2");
                    }
                }
            }

            // If we received a value frame but never got CDC2, build a provisional
            // handshake so SerialReader can start.  It will fire the real Connected
            // event once it receives the CDC2 response to its own CMD:INFO.
            if (valueFrame is not null)
            {
                int n = valueFrame.Length;
                var provisional = new Handshake(
                    Sliders: n,
                    Version: "?",
                    Name:    "ControlDeckCore",
                    Names:   Enumerable.Range(1, n).Select(i => $"Slider {i}").ToArray(),
                    Port:    port,
                    Baud:    baud);

                System.Diagnostics.Debug.WriteLine(
                    $"[Detector] {port} @ {baud}: using provisional handshake ({n} sliders)");

                var conn = new OpenConnection(provisional, sp);
                sp = null;
                return conn;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Detector] {port} @ {baud}: exception — {ex.GetType().Name}: {ex.Message}");
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
