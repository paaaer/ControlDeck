using ControlDeck.Audio;
using ControlDeck.Device;

namespace ControlDeck.UI;

/// <summary>
/// Top-level ApplicationContext. Owns the tray icon, device manager,
/// audio controller, and all windows.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon     _tray;
    private readonly DeviceManager  _device;
    private readonly AudioController _audio;
    private          AppConfig      _cfg;

    private ConfigForm?          _configForm;
    private AboutForm?           _aboutForm;
    private ToolStripMenuItem    _statusItem = null!;
    private volatile bool        _quitting;

    // Per-slider last-sent volume — skip COM call when value hasn't changed enough
    private readonly float[] _lastVolumes  = new float[16];
    private const    float   VolumeEpsilon = 0.004f; // ~0.4 % change required

    public TrayApplicationContext()
    {
        _cfg   = AppConfig.Load();
        _audio = new AudioController();

        // Build tray icon
        _tray = new NotifyIcon
        {
            Icon             = TrayIcon.Make(connected: false),
            Text             = "ControlDeck — searching...",
            Visible          = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => ShowConfig();

        // Device manager
        _device = new DeviceManager(_cfg);
        _device.Connected         += OnConnected;
        _device.Disconnected      += OnDisconnected;
        _device.ValuesReceivedRaw += OnValuesAudio;  // background thread — audio only
        _device.ValuesReceived    += OnValuesUi;     // UI thread — preview only
        _device.Start();
    }

    // -----------------------------------------------------------------------
    // Device events (already on UI thread via SynchronizationContext)

    private void OnConnected(Handshake hs)
    {
        if (_quitting) return;
        _statusItem.Text = $"{hs.Name}  ·  {hs.Port}  ·  {hs.Sliders} sliders  ·  fw {hs.Version}";
        _tray.Icon = TrayIcon.Make(connected: true);
        _tray.Text = $"ControlDeck — {hs.Name} on {hs.Port} · {hs.Sliders} sliders · fw {hs.Version}";
        _tray.ShowBalloonTip(
            2000,
            "ControlDeck",
            $"{hs.Name} connected on {hs.Port} ({hs.Sliders} sliders)",
            ToolTipIcon.Info);

        // Ensure config has an entry for every slider
        for (int i = 0; i < hs.Sliders; i++)
        {
            if (_cfg.GetSliderTarget(i) == "unassigned")
                _cfg.SetSliderTarget(i, "unassigned");
        }
        _cfg.Save();

        _configForm?.Refresh(hs, _cfg);
    }

    private void OnDisconnected()
    {
        if (_quitting) return;
        _statusItem.Text = "Disconnected — searching...";
        _tray.Icon = TrayIcon.Make(connected: false);
        _tray.Text = "ControlDeck — disconnected, searching...";
        _configForm?.Refresh(null, _cfg);
    }

    // Runs on the serial background thread — no UI calls allowed here
    private void OnValuesAudio(float[] values)
    {
        if (_quitting) return;
        for (int i = 0; i < values.Length; i++)
        {
            if (Math.Abs(values[i] - _lastVolumes[i]) < VolumeEpsilon) continue;
            _lastVolumes[i] = values[i];
            var target = _cfg.GetSliderTarget(i);
            if (target != "unassigned")
                _audio.SetVolume(target, values[i]);
        }
    }

    // Runs on the UI thread — UI preview only, no audio calls
    private void OnValuesUi(float[] values)
    {
        if (_quitting) return;
        _configForm?.UpdatePreview(values);
    }

    // -----------------------------------------------------------------------
    // Menu

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("Searching for device...") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        var configItem = new ToolStripMenuItem("Configure...");
        configItem.Click += (_, _) => ShowConfig();
        menu.Items.Add(configItem);

        var aboutItem = new ToolStripMenuItem("About ControlDeck");
        aboutItem.Click += (_, _) => ShowAbout();
        menu.Items.Add(aboutItem);

        menu.Items.Add(new ToolStripSeparator());

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) => Quit();
        menu.Items.Add(quitItem);

        return menu;
    }

    // -----------------------------------------------------------------------
    // Windows

    private void ShowConfig()
    {
        if (_configForm == null || _configForm.IsDisposed)
        {
            _configForm = new ConfigForm(_cfg, _audio, OnConfigSaved);
        }
        _configForm.Refresh(_device.CurrentHandshake, _cfg);
        _configForm.Show();
        _configForm.Activate();
        _configForm.BringToFront();
    }

    private void ShowAbout()
    {
        if (_aboutForm == null || _aboutForm.IsDisposed)
            _aboutForm = new AboutForm();
        _aboutForm.Show();
        _aboutForm.Activate();
    }

    private void OnConfigSaved(AppConfig newCfg)
    {
        _cfg = newCfg;
        _cfg.Save();
        _device.UpdateConfig(_cfg);
    }

    private void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        try
        {
            _cfg.Save();
            _device.Stop();
            _device.Dispose();
            _audio.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
        }
        catch { /* ignore disposal errors — we are exiting regardless */ }
        finally
        {
            Application.Exit();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Quit();
        base.Dispose(disposing);
    }
}
