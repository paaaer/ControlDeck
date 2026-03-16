using System.IO.Ports;

namespace ControlDeck.Device;

/// <summary>
/// Reads CDC2 frames from a serial port on a background thread.
/// Accepts an already-open <see cref="OpenConnection"/> from the Detector so
/// the port is never closed and reopened between detection and live reading —
/// eliminating the DTR glitch that resets ESP32 boards.
/// </summary>
public sealed class SerialReader : IDisposable
{
    public event Action<Handshake>? Connected;
    public event Action<float[]>?  ValuesReceived;
    public event Action?           Disconnected;

    private readonly string     _port;
    private readonly int        _baud;
    private          SerialPort _sp;
    private readonly Handshake  _initialHandshake;
    private          Thread?    _thread;
    private volatile bool       _running;

    public bool IsConnected => _running && _sp.IsOpen;

    public SerialReader(OpenConnection conn)
    {
        _sp               = conn.Port;
        _initialHandshake = conn.Handshake;
        _port             = conn.Handshake.Port;
        _baud             = conn.Handshake.Baud;
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
        // Do NOT close the port here — closing while ReadLine() is active on the
        // background thread throws OperationCanceledException on Windows.
        // ReadLoop's finally block closes the port after the thread exits
        // (ReadTimeout = 1 s, so it exits within one tick of _running → false).
    }

    /// <summary>
    /// Stop and block until the background thread has fully exited and released
    /// the COM port. Safe to call before opening the same port again.
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
            // Port is already open — reconfigure timeouts.
            _sp.ReadTimeout  = 1000;
            _sp.WriteTimeout = 1000;
            _sp.NewLine      = "\n";

            System.Diagnostics.Debug.WriteLine(
                $"[SerialReader] {_port} @ {_baud}: using pre-opened port (provisional={_initialHandshake.Version == "?"})");

            // Fire Connected immediately with whatever the Detector gave us
            // (may be a provisional handshake if CDC2 was delayed).
            Connected?.Invoke(_initialHandshake);

            // Request the real handshake — firmware will respond with CDC2:
            // on its next loop tick and Connected will fire again with full info.
            _sp.Write(Protocol.InfoCommand, 0, Protocol.InfoCommand.Length);

            while (_running)
            {
                string line;
                try
                {
                    line = _sp.ReadLine();
                }
                catch (TimeoutException)
                {
                    continue;   // normal — no data in this 1 s window
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SerialReader] {_port}: read error — {ex.GetType().Name}");
                    break;      // port lost
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                // Handshake re-sent by device (e.g. after CMD:INFO)
                var hs = Protocol.ParseHandshake(line, _port, _baud);
                if (hs is not null)
                {
                    Connected?.Invoke(hs);
                    continue;
                }

                // Data frame
                var values = Protocol.ParseFrame(line);
                if (values is not null)
                    ValuesReceived?.Invoke(values);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SerialReader] {_port}: fatal — {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { _sp.Close(); _sp.Dispose(); } catch { /* ignore */ }
            _running = false;
            Disconnected?.Invoke();
        }
    }
}
