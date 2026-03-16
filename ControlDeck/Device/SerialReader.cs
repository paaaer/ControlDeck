using System.IO.Ports;

namespace ControlDeck.Device;

/// <summary>
/// Reads CDC2 frames from a serial port on a background thread.
/// Raises events which callers must marshal to the UI thread if needed.
/// </summary>
public sealed class SerialReader : IDisposable
{
    public event Action<Handshake>? Connected;
    public event Action<float[]>?  ValuesReceived;
    public event Action?           Disconnected;

    private readonly string     _port;
    private readonly int        _baud;
    private          SerialPort? _sp;
    private          Thread?    _thread;
    private volatile bool       _running;

    public bool IsConnected => _running && _sp?.IsOpen == true;

    public SerialReader(string port, int baud = 921600)
    {
        _port = port;
        _baud = baud;
    }

    public void Start()
    {
        _running = true;
        _thread  = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name         = $"SerialReader-{_port}",
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        // Do NOT close the port here — calling Close() while ReadLine() is active on the
        // background thread throws OperationCanceledException on Windows.
        // The ReadLoop finally block handles port cleanup when the thread exits naturally
        // (ReadTimeout is 1 s, so it exits within one tick of _running becoming false).
    }

    /// <summary>
    /// Stop the reader and block until the background thread has fully exited
    /// (and released the COM port). Waits at most <paramref name="timeoutMs"/> ms.
    /// </summary>
    public void StopAndWait(int timeoutMs = 1500)
    {
        Stop();
        _thread?.Join(timeoutMs);
    }

    public void Dispose() => Stop();

    // -----------------------------------------------------------------------

    private void ReadLoop()
    {
        try
        {
            _sp = new SerialPort(_port, _baud)
            {
                ReadTimeout  = 1000,
                WriteTimeout = 1000,
                NewLine      = "\n",
                // Prevent toggling DTR/RTS on Open() — many ESP32 boards use
                // those lines to drive the EN reset circuit, which reboots the
                // device and makes it unreachable for several seconds.
                DtrEnable    = false,
                RtsEnable    = false,
            };
            _sp.Open();
            System.Diagnostics.Debug.WriteLine($"[SerialReader] {_port} @ {_baud}: port opened");

            // Ask device to re-send handshake immediately
            _sp.Write(Protocol.InfoCommand, 0, Protocol.InfoCommand.Length);

            bool handshakeReceived = false;

            while (_running)
            {
                string line;
                try
                {
                    line = _sp.ReadLine();
                }
                catch (TimeoutException)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialReader] {_port}: read timeout (waiting for data)");
                    continue;   // normal — no data in this window
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialReader] {_port}: read error — {ex.GetType().Name}");
                    break;      // port lost
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                System.Diagnostics.Debug.WriteLine($"[SerialReader] {_port}: '{line.Trim()}'");

                // Handshake
                var hs = Protocol.ParseHandshake(line, _port, _baud);
                if (hs is not null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialReader] {_port}: handshake received — {hs.Name}");
                    handshakeReceived = true;
                    Connected?.Invoke(hs);
                    continue;
                }

                // Data frame
                if (handshakeReceived)
                {
                    var values = Protocol.ParseFrame(line);
                    if (values is not null)
                        ValuesReceived?.Invoke(values);
                }
            }
        }
        catch (Exception)
        {
            // Failed to open port
        }
        finally
        {
            try { _sp?.Close(); _sp?.Dispose(); } catch { /* ignore */ }
            _sp      = null;
            _running = false;
            Disconnected?.Invoke();
        }
    }
}
