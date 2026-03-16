namespace ControlDeck.Device;

/// <summary>
/// Owns the SerialReader, handles auto-detection and reconnection.
/// All events are raised on the UI (main) thread via the supplied SynchronizationContext.
/// </summary>
public sealed class DeviceManager : IDisposable
{
    public event Action<Handshake>? Connected;
    /// <summary>Fired on the UI thread — for UI updates only.</summary>
    public event Action<float[]>?  ValuesReceived;
    /// <summary>Fired on the serial background thread — use for audio processing.</summary>
    public event Action<float[]>?  ValuesReceivedRaw;
    public event Action?           Disconnected;

    public Handshake? CurrentHandshake { get; private set; }
    public bool IsConnected => _reader?.IsConnected == true;

    private readonly SynchronizationContext _uiContext;
    private          AppConfig              _cfg;
    private          SerialReader?          _reader;
    private          System.Threading.Timer? _reconnectTimer;
    private readonly object                 _lock = new();

    // Prevents overlapping probe runs when the reconnect timer fires faster
    // than a full multi-baud scan completes.
    private volatile bool   _probing;

    // Cached from the last successful connection — lets reconnects skip the
    // full baud-rate scan and go straight to the known-good port + baud.
    private string? _lastPort;
    private int     _lastBaud;

    private const int ReconnectIntervalMs = 5000;

    public DeviceManager(AppConfig cfg)
    {
        _cfg       = cfg;
        _uiContext = SynchronizationContext.Current
                     ?? new SynchronizationContext();
    }

    public void Start()
    {
        TryConnect();
        _reconnectTimer = new System.Threading.Timer(
            _ => TryConnect(),
            null,
            ReconnectIntervalMs,
            ReconnectIntervalMs);
    }

    public void UpdateConfig(AppConfig cfg)
    {
        _cfg = cfg;
        // If port setting changed, force reconnect
        Stop();
        Start();
    }

    public void Stop()
    {
        _reconnectTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
        StopReader();
    }

    public void Dispose() => Stop();

    // -----------------------------------------------------------------------

    private void TryConnect()
    {
        // Drop overlapping calls — a slow multi-baud scan must not race itself.
        if (_probing) return;
        _probing = true;
        try
        {
            // Stop any existing reader and wait for its thread to fully exit so
            // the COM port is released before ProbePort tries to open it.
            SerialReader? oldReader;
            lock (_lock)
            {
                if (_reader?.IsConnected == true) return;
                oldReader = _reader;
                _reader   = null;
            }
            oldReader?.StopAndWait();   // blocks until background thread exits (≤1.5 s)
            oldReader?.Dispose();

            var portCfg = _cfg.Device.Port;

            // Fast path: if we've connected before, try that exact port+baud
            // first.  Reconnects after a USB unplug are usually instant this way.
            Handshake? hs = null;
            if (_lastPort is not null && _lastBaud > 0)
                hs = Detector.ProbePort(_lastPort, _lastBaud);

            // Full scan if the fast path missed (first run, or device moved port)
            if (hs is null)
            {
                hs = portCfg == "auto"
                    ? Detector.AutoDetect(_cfg.Device.Baud)
                    : Detector.ProbePort(portCfg, _cfg.Device.Baud);
            }

            if (hs is null) return;

            _lastPort = hs.Port;
            _lastBaud = hs.Baud;

            // Give the OS/driver a moment to fully release the COM port after
            // the probe closed it.  Without this, some USB-serial drivers briefly
            // re-assert DTR on the next Open() even when DtrEnable=false, which
            // can trigger the ESP32 auto-reset circuit.
            System.Threading.Thread.Sleep(150);

            lock (_lock)
            {
                var reader = new SerialReader(hs.Port, hs.Baud);
                reader.Connected      += OnConnected;
                reader.ValuesReceived += OnValues;
                reader.Disconnected   += OnDisconnected;
                _reader = reader;
                reader.Start();
            }
        }
        finally
        {
            _probing = false;
        }
    }

    private void StopReader()
    {
        SerialReader? r;
        lock (_lock)
        {
            r       = _reader;
            _reader = null;
        }
        r?.Stop();
        r?.Dispose();
    }

    private void OnConnected(Handshake hs)
    {
        CurrentHandshake = hs;
        _uiContext.Post(_ => Connected?.Invoke(hs), null);
    }

    private void OnValues(float[] values)
    {
        ValuesReceivedRaw?.Invoke(values);                          // background thread — audio
        _uiContext.Post(_ => ValuesReceived?.Invoke(values), null); // UI thread — preview
    }

    private void OnDisconnected()
    {
        CurrentHandshake = null;
        _uiContext.Post(_ => Disconnected?.Invoke(), null);
    }
}
