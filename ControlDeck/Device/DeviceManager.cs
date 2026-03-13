namespace ControlDeck.Device;

/// <summary>
/// Owns the SerialReader, handles auto-detection and reconnection.
/// All events are raised on the UI (main) thread via the supplied SynchronizationContext.
/// </summary>
public sealed class DeviceManager : IDisposable
{
    public event Action<Handshake>? Connected;
    public event Action<float[]>?  ValuesReceived;
    public event Action?           Disconnected;

    public Handshake? CurrentHandshake { get; private set; }
    public bool IsConnected => _reader?.IsConnected == true;

    private readonly SynchronizationContext _uiContext;
    private          AppConfig              _cfg;
    private          SerialReader?          _reader;
    private          System.Threading.Timer? _reconnectTimer;
    private readonly object                 _lock = new();

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
        lock (_lock)
        {
            if (_reader?.IsConnected == true) return;
        }

        var portCfg = _cfg.Device.Port;
        var baud    = _cfg.Device.Baud;

        Handshake? hs = portCfg == "auto"
            ? Detector.AutoDetect(baud)
            : Detector.ProbePort(portCfg, baud);

        if (hs is null) return;

        lock (_lock)
        {
            StopReader();
            var reader = new SerialReader(hs.Port, baud);
            reader.Connected      += OnConnected;
            reader.ValuesReceived += OnValues;
            reader.Disconnected   += OnDisconnected;
            _reader = reader;
            reader.Start();
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
        _uiContext.Post(_ => ValuesReceived?.Invoke(values), null);
    }

    private void OnDisconnected()
    {
        CurrentHandshake = null;
        _uiContext.Post(_ => Disconnected?.Invoke(), null);
    }
}
