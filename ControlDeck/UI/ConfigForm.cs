using ControlDeck.Audio;
using ControlDeck.Device;

namespace ControlDeck.UI;

/// <summary>
/// Configuration window — map sliders to audio targets, preview live values.
/// </summary>
public sealed class ConfigForm : Form
{
    private readonly AudioController      _audio;
    private readonly Action<AppConfig>    _onSave;
    private          AppConfig            _cfg;

    private readonly Label          _statusLabel;
    private readonly FlowLayoutPanel _rowPanel;
    private readonly ComboBox        _portCombo;

    private readonly List<SliderRow> _rows = [];

    public ConfigForm(AppConfig cfg, AudioController audio, Action<AppConfig> onSave)
    {
        _cfg    = cfg;
        _audio  = audio;
        _onSave = onSave;

        Text            = "ControlDeck — Configure";
        Size            = new Size(560, 480);
        MinimumSize     = new Size(480, 360);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterScreen;
        ShowInTaskbar   = true;
        FormClosing    += (_, e) => { e.Cancel = true; Hide(); };

        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 5,
            Padding     = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // status
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // slider rows
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // port
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // buttons
        Controls.Add(root);

        // Status label
        _statusLabel = new Label
        {
            Dock      = DockStyle.Fill,
            Text      = "Searching for device...",
            ForeColor = Color.Gray,
            Padding   = new Padding(0, 0, 0, 8),
            AutoSize  = true,
        };
        root.Controls.Add(_statusLabel, 0, 0);

        // Slider rows panel (scrollable)
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _rowPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoSize      = true,
            AutoSizeMode  = AutoSizeMode.GrowAndShrink,
        };
        scroll.Controls.Add(_rowPanel);
        root.Controls.Add(scroll, 0, 1);

        // Port setting
        var portGroup = new GroupBox { Text = "Device port", Dock = DockStyle.Fill, Height = 60, Padding = new Padding(8) };
        var portPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        portPanel.Controls.Add(new Label { Text = "Port:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 4, 4, 0) });
        _portCombo = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDown };
        _portCombo.Items.Add("auto");
        foreach (var p in ControlDeck.Device.Detector.ListPorts())
            _portCombo.Items.Add(p);
        _portCombo.Text = cfg.Device.Port;
        portPanel.Controls.Add(_portCombo);
        portGroup.Controls.Add(portPanel);
        root.Controls.Add(portGroup, 0, 2);

        // Button row
        var btnPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding       = new Padding(0, 8, 0, 0),
            AutoSize      = true,
        };

        AddButton(btnPanel, "Close",            (_, _) => Hide());
        AddButton(btnPanel, "Save",             (_, _) => Save(), isDefault: true);
        AddButton(btnPanel, "Refresh sessions", (_, _) => RefreshSessions());
        root.Controls.Add(btnPanel, 0, 3);
    }

    // -----------------------------------------------------------------------
    // Public API called from TrayApplicationContext

    public new void Refresh(Handshake? hs, AppConfig cfg)
    {
        _cfg = cfg;

        if (hs is not null)
        {
            _statusLabel.Text      = $"Connected: {hs.Port}  ·  {hs.Sliders} sliders  ·  firmware {hs.Version}";
            _statusLabel.ForeColor = Color.FromArgb(0x0F, 0x6E, 0x56);
            RebuildRows(hs.Sliders);
        }
        else
        {
            _statusLabel.Text      = "No device connected — plug in your ControlDeckCore";
            _statusLabel.ForeColor = Color.Gray;
            RebuildRows(0);
        }
    }

    public void UpdatePreview(float[] values)
    {
        if (!Visible) return;
        for (int i = 0; i < _rows.Count && i < values.Length; i++)
            _rows[i].SetValue(values[i]);
    }

    // -----------------------------------------------------------------------

    private void RebuildRows(int count)
    {
        _rowPanel.Controls.Clear();
        _rows.Clear();

        if (count == 0)
        {
            _rowPanel.Controls.Add(new Label
            {
                Text      = "Connect a ControlDeckCore device to configure sliders.",
                ForeColor = Color.Gray,
                AutoSize  = true,
                Padding   = new Padding(4),
            });
            return;
        }

        var sessions = _audio.GetActiveSessions();
        for (int i = 0; i < count; i++)
        {
            var row = new SliderRow(i, _cfg.GetSliderTarget(i), sessions);
            _rows.Add(row);
            _rowPanel.Controls.Add(row);
        }
    }

    private void RefreshSessions()
    {
        var sessions = _audio.GetActiveSessions();
        foreach (var row in _rows)
            row.RefreshSessions(sessions);
    }

    private void Save()
    {
        foreach (var row in _rows)
            _cfg.SetSliderTarget(row.SliderIndex, row.SelectedTarget);
        _cfg.Device.Port = _portCombo.Text.Trim();
        _onSave(_cfg);
        Hide();
    }

    private static void AddButton(FlowLayoutPanel panel, string text,
        EventHandler handler, bool isDefault = false)
    {
        var btn = new Button
        {
            Text      = text,
            AutoSize  = true,
            Padding   = new Padding(8, 4, 8, 4),
            Margin    = new Padding(4, 0, 0, 0),
        };
        btn.Click += handler;
        if (isDefault) btn.Font = new Font(btn.Font, FontStyle.Bold);
        panel.Controls.Add(btn);
    }
}

/// <summary>
/// One row in the config form: "Slider N | [target dropdown] | [live bar]"
/// </summary>
public sealed class SliderRow : UserControl
{
    public int    SliderIndex    { get; }
    public string SelectedTarget => _combo.Text;

    private readonly ComboBox    _combo;
    private readonly ProgressBar _bar;

    public SliderRow(int index, string currentTarget, List<string> sessions)
    {
        SliderIndex = index;
        Height      = 36;
        Dock        = DockStyle.Top;
        Padding     = new Padding(0, 2, 0, 2);

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 3,
            RowCount    = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text      = $"Slider {index + 1}",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font(Font, FontStyle.Bold),
        }, 0, 0);

        _combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
        PopulateCombo(sessions, currentTarget);
        layout.Controls.Add(_combo, 1, 0);

        _bar = new ProgressBar
        {
            Dock    = DockStyle.Fill,
            Minimum = 0,
            Maximum = 1000,
            Value   = 0,
            Style   = ProgressBarStyle.Continuous,
        };
        layout.Controls.Add(_bar, 2, 0);
    }

    public void RefreshSessions(List<string> sessions) =>
        PopulateCombo(sessions, _combo.Text);

    public void SetValue(float v) =>
        _bar.Value = (int)Math.Clamp(v * 1000, 0, 1000);

    private void PopulateCombo(List<string> sessions, string current)
    {
        _combo.Items.Clear();
        foreach (var s in sessions) _combo.Items.Add(s);
        // Preserve current selection even if process isn't running
        if (!sessions.Contains(current, StringComparer.OrdinalIgnoreCase))
            _combo.Items.Add(current);
        _combo.Text = current;
    }
}
