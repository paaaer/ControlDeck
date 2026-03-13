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

    private ConfigForm? _configForm;
    private AboutForm?  _aboutForm;

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
        _device.Connected      += OnConnected;
        _device.Disconnected   += OnDisconnected;
        _device.ValuesReceived += OnValues;
        _device.Start();
    }

    // -----------------------------------------------------------------------
    // Device events (already on UI thread via SynchronizationContext)

    private void OnConnected(Handshake hs)
    {
        _tray.Icon = TrayIcon.Make(connected: true);
        _tray.Text = $"ControlDeck — {hs.Port} · {hs.Sliders} sliders · fw {hs.Version}";
        _tray.ShowBalloonTip(
            2000,
            "ControlDeck",
            $"Connected on {hs.Port} ({hs.Sliders} sliders)",
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
        _tray.Icon = TrayIcon.Make(connected: false);
        _tray.Text = "ControlDeck — disconnected, searching...";
        _configForm?.Refresh(null, _cfg);
    }

    private void OnValues(float[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            var target = _cfg.GetSliderTarget(i);
            if (target != "unassigned")
                _audio.SetVolume(target, values[i]);
        }
        _configForm?.UpdatePreview(values);
    }

    // -----------------------------------------------------------------------
    // Menu

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var statusItem = new ToolStripMenuItem("Status: searching...") { Enabled = false };
        menu.Items.Add(statusItem);
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
        _cfg.Save();
        _device.Stop();
        _device.Dispose();
        _audio.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Quit();
        base.Dispose(disposing);
    }
}
