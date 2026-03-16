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
    private volatile bool _probing;

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
        if (_probing) return;
        _probing = true;
        try
        {
            // Grab and stop any existing reader, waiting for its thread to fully
            // exit so the COM port is released before we probe again.
            SerialReader? oldReader;
            lock (_lock)
            {
                if (_reader?.IsConnected == true) return;
                oldReader = _reader;
                _reader   = null;
            }
            oldReader?.StopAndWait();
            oldReader?.Dispose();

            var portCfg = _cfg.Device.Port;

            // Fast path: try last known port+baud first so reconnects after a
            // USB unplug are nearly instant.
            OpenConnection? conn = null;
            if (_lastPort is not null && _lastBaud > 0)
                conn = Detector.OpenPort(_lastPort, _lastBaud);

            // Full scan if fast path missed (first run or device changed port)
            conn ??= portCfg == "auto"
                ? Detector.AutoDetect(_cfg.Device.Baud)
                : Detector.OpenPort(portCfg, _cfg.Device.Baud);

            if (conn is null) return;

            _lastPort = conn.Handshake.Port;
            _lastBaud = conn.Handshake.Baud;

            // Hand the already-open connection to SerialReader — the port is
            // never closed and reopened, so no DTR glitch can reset the device.
            lock (_lock)
            {
                var reader = new SerialReader(conn);   // takes ownership of conn
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
        r?.StopAndWait();
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
