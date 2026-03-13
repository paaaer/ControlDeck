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

    public SerialReader(string port, int baud = 115200)
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
        try { _sp?.Close(); } catch { /* ignore */ }
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
            };
            _sp.Open();

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
                    continue;   // normal — no data in this window
                }
                catch (Exception)
                {
                    break;      // port lost
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                // Handshake
                var hs = Protocol.ParseHandshake(line, _port);
                if (hs is not null)
                {
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
