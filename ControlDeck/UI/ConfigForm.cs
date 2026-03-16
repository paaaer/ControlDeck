using ControlDeck.Audio;
using ControlDeck.Device;

namespace ControlDeck.UI;

/// <summary>
/// Configuration window — map sliders to audio targets, preview live values.
/// </summary>
public sealed class ConfigForm : Form
{
    private readonly AudioController   _audio;
    private readonly Action<AppConfig> _onSave;
    private          AppConfig         _cfg;

    private readonly Label           _statusLabel;
    private readonly Panel           _rowPanel;   // plain Panel — DockStyle.Top children fill width correctly
    private readonly ComboBox        _portCombo;
    private readonly ComboBox        _baudCombo;
    private readonly List<SliderRow> _rows = [];

    // WiFi section
    private readonly TextBox   _webUiHostBox;
    private readonly LinkLabel _webUiLink;
    private readonly Label     _webUiStatus;

    public ConfigForm(AppConfig cfg, AudioController audio, Action<AppConfig> onSave)
    {
        _cfg    = cfg;
        _audio  = audio;
        _onSave = onSave;

        Text            = "ControlDeck — Configure";
        Size            = new Size(720, 540);
        MinimumSize     = new Size(580, 380);
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
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // status
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // slider rows
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // port
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // web UI
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // buttons
        Controls.Add(root);

        // ── Status ──────────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Dock      = DockStyle.Fill,
            Text      = "Searching for device...",
            ForeColor = Color.Gray,
            Padding   = new Padding(0, 0, 0, 8),
            AutoSize  = true,
        };
        root.Controls.Add(_statusLabel, 0, 0);

        // ── Slider rows (scrollable) ─────────────────────────────────────────
        // Plain Panel with AutoScroll + DockStyle.Top children fills width correctly.
        _rowPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        root.Controls.Add(_rowPanel, 0, 1);

        // ── Port group ───────────────────────────────────────────────────────
        var portGroup = new GroupBox
        {
            Text    = "Device port",
            Dock    = DockStyle.Fill,
            Height  = 60,
            Padding = new Padding(8),
        };
        var portPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        portPanel.Controls.Add(new Label
        {
            Text    = "Port:",
            AutoSize = true,
            Anchor  = AnchorStyles.Left,
            Padding = new Padding(0, 4, 4, 0),
        });
        _portCombo = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDown };
        _portCombo.Items.Add("auto");
        foreach (var p in Detector.ListPorts())
            _portCombo.Items.Add(p);
        _portCombo.Text = cfg.Device.Port;
        portPanel.Controls.Add(_portCombo);

        portPanel.Controls.Add(new Label
        {
            Text    = "Baud:",
            AutoSize = true,
            Anchor  = AnchorStyles.Left,
            Padding = new Padding(12, 4, 4, 0),
        });
        _baudCombo = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var b in BaudRates.Valid)
            _baudCombo.Items.Add(b);
        _baudCombo.SelectedItem = BaudRates.Valid.Contains(cfg.Device.Baud)
            ? cfg.Device.Baud : BaudRates.Default;
        portPanel.Controls.Add(_baudCombo);
        portGroup.Controls.Add(portPanel);
        root.Controls.Add(portGroup, 0, 2);

        // ── Web UI / WiFi ────────────────────────────────────────────────────
        var wifiGroup = new GroupBox
        {
            Text    = "Device web UI (WiFi)",
            Dock    = DockStyle.Fill,
            Padding = new Padding(8, 4, 8, 6),
        };

        var wifiLayout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 3,
            RowCount    = 2,
            AutoSize    = true,
        };
        wifiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // label "Host:"
        wifiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // text box
        wifiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // link / status

        wifiLayout.Controls.Add(new Label
        {
            Text     = "Host:",
            AutoSize = true,
            Anchor   = AnchorStyles.Left | AnchorStyles.Right,
            Padding  = new Padding(0, 4, 6, 0),
        }, 0, 0);

        _webUiHostBox = new TextBox
        {
            Text   = cfg.Device.WebUiHost,
            Dock   = DockStyle.Fill,
            Margin = new Padding(0, 2, 6, 2),
        };
        wifiLayout.Controls.Add(_webUiHostBox, 1, 0);

        _webUiLink = new LinkLabel
        {
            Text     = "Open web UI ↗",
            AutoSize = true,
            Anchor   = AnchorStyles.Left,
            Padding  = new Padding(0, 4, 0, 0),
            Visible  = false,
        };
        _webUiLink.LinkClicked += (_, _) => OpenWebUi();
        wifiLayout.Controls.Add(_webUiLink, 2, 0);

        _webUiStatus = new Label
        {
            Text      = "Not checked — connect a device first.",
            ForeColor = Color.Gray,
            AutoSize  = true,
            Padding   = new Padding(0, 2, 0, 0),
        };
        wifiLayout.SetColumnSpan(_webUiStatus, 3);
        wifiLayout.Controls.Add(_webUiStatus, 0, 1);

        wifiGroup.Controls.Add(wifiLayout);
        root.Controls.Add(wifiGroup, 0, 3);

        // ── Buttons ──────────────────────────────────────────────────────────
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
        root.Controls.Add(btnPanel, 0, 4);
    }

    // -----------------------------------------------------------------------
    // Public API called from TrayApplicationContext

    public void Refresh(Handshake? hs, AppConfig cfg)
    {
        _cfg = cfg;

        if (hs is not null)
        {
            _statusLabel.Text      = $"Connected: {hs.Port}  ·  {hs.Name}  ·  {hs.Sliders} sliders  ·  fw {hs.Version}";
            _statusLabel.ForeColor = Color.FromArgb(0x0F, 0x6E, 0x56);
            RebuildRows(hs.Sliders, hs.Names);
            _ = CheckWebUiAsync();
        }
        else
        {
            _statusLabel.Text      = "No device connected — plug in your ControlDeckCore";
            _statusLabel.ForeColor = Color.Gray;
            RebuildRows(0, null);
            SetWifiDisconnectedHint();
        }
    }

    public void UpdatePreview(float[] values)
    {
        if (!Visible) return;
        for (int i = 0; i < _rows.Count && i < values.Length; i++)
            _rows[i].SetValue(values[i]);
    }

    // -----------------------------------------------------------------------

    private void RebuildRows(int count, string[]? names = null)
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

        var targets = _audio.GetAudioTargets();

        // Build _rows in order (index 0 = slider 0) for correct UpdatePreview indexing
        for (int i = 0; i < count; i++)
        {
            var displayName = (names is not null && i < names.Length) ? names[i] : $"Slider {i + 1}";
            _rows.Add(new SliderRow(i, displayName, _cfg.GetSliderTarget(i), targets));
        }

        // Add to panel in REVERSE so DockStyle.Top stacks row 0 at the top
        for (int i = _rows.Count - 1; i >= 0; i--)
            _rowPanel.Controls.Add(_rows[i]);
    }

    private void RefreshSessions()
    {
        var targets = _audio.GetAudioTargets();
        foreach (var row in _rows)
            row.RefreshTargets(targets);
    }

    private void Save()
    {
        foreach (var row in _rows)
            _cfg.SetSliderTarget(row.SliderIndex, row.SelectedTargetId);
        _cfg.Device.Port      = _portCombo.Text.Trim();
        _cfg.Device.Baud      = _baudCombo.SelectedItem is int b ? b : BaudRates.Default;
        _cfg.Device.WebUiHost = _webUiHostBox.Text.Trim();
        _onSave(_cfg);
        Hide();
    }

    // ── WiFi helpers ─────────────────────────────────────────────────────────

    private void OpenWebUi()
    {
        var host = _webUiHostBox.Text.Trim();
        var url  = host.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? host : $"http://{host}";
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    private async Task CheckWebUiAsync()
    {
        var host = _webUiHostBox.Text.Trim();
        var url  = host.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? host : $"http://{host}";

        _webUiLink.Visible  = false;
        _webUiStatus.Text      = "Checking WiFi connection...";
        _webUiStatus.ForeColor = Color.Gray;

        bool reachable = false;
        try
        {
            using var http    = new System.Net.Http.HttpClient();
            http.Timeout      = TimeSpan.FromSeconds(3);
            var response      = await http.GetAsync(url + "/settings").ConfigureAwait(true);
            reachable         = response.IsSuccessStatusCode;
        }
        catch { reachable = false; }

        if (reachable)
        {
            _webUiLink.Text        = $"Open  http://{host}  ↗";
            _webUiLink.Visible     = true;
            _webUiStatus.Text      = "WiFi is configured and the device web UI is reachable.";
            _webUiStatus.ForeColor = Color.FromArgb(0x0F, 0x6E, 0x56);
        }
        else
        {
            _webUiLink.Visible     = false;
            _webUiStatus.Text      =
                "Web UI not reachable at this address. " +
                "If WiFi is not set up yet: on first boot the device creates a hotspot named 'ControlDeck' — " +
                "connect to it with your phone or PC and follow the captive portal to configure your network.";
            _webUiStatus.ForeColor = Color.FromArgb(0xA0, 0x60, 0x00);
        }
    }

    private void SetWifiDisconnectedHint()
    {
        _webUiLink.Visible     = false;
        _webUiStatus.Text      = "Connect the device via USB first, then the WiFi status will be checked automatically.";
        _webUiStatus.ForeColor = Color.Gray;
    }

    private static void AddButton(FlowLayoutPanel panel, string text,
        EventHandler handler, bool isDefault = false)
    {
        var btn = new Button
        {
            Text    = text,
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
            Margin  = new Padding(4, 0, 0, 0),
        };
        btn.Click += handler;
        if (isDefault) btn.Font = new Font(btn.Font, FontStyle.Bold);
        panel.Controls.Add(btn);
    }
}

// ---------------------------------------------------------------------------

/// <summary>
/// One row: [Name] [GroupedComboBox] [XX%] [████ bar] [⚠]
/// The ⚠ indicator appears when the configured target is not currently available.
/// The binding is always preserved — the target will re-bind automatically when it
/// becomes available again (e.g. an app is launched).
/// </summary>
public sealed class SliderRow : UserControl
{
    public int    SliderIndex     { get; }
    public string SelectedTargetId => _combo.SelectedTarget?.Id ?? "unassigned";

    private readonly GroupedComboBox _combo;
    private readonly ProgressBar     _bar;
    private readonly Label           _valueLabel;
    private readonly Label           _warnLabel;
    private readonly ToolTip         _toolTip = new();

    public SliderRow(int index, string displayName, string targetId, List<AudioTarget> targets)
    {
        SliderIndex = index;
        Height      = 44;
        Dock        = DockStyle.Top;
        Padding     = new Padding(0, 3, 0, 3);

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 5,
            RowCount    = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));  // name
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));  // combo
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,  46));  // %
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100));  // bar
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,  22));  // ⚠
        Controls.Add(layout);

        // Name
        layout.Controls.Add(new Label
        {
            Text      = displayName,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font(Font, FontStyle.Bold),
        }, 0, 0);

        // Target dropdown
        _combo = new GroupedComboBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 4, 4) };
        _combo.Populate(targets);
        _combo.SelectById(targetId);
        _combo.SelectedIndexChanged += (_, _) => UpdateWarning();
        layout.Controls.Add(_combo, 1, 0);

        // Percentage label
        _valueLabel = new Label
        {
            Text      = "—",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding   = new Padding(0, 0, 4, 0),
            ForeColor = Color.Gray,
        };
        layout.Controls.Add(_valueLabel, 2, 0);

        // Progress bar
        _bar = new ProgressBar
        {
            Dock    = DockStyle.Fill,
            Minimum = 0,
            Maximum = 1000,
            Value   = 0,
            Style   = ProgressBarStyle.Continuous,
            Margin  = new Padding(0, 8, 4, 8),
        };
        layout.Controls.Add(_bar, 3, 0);

        // Warning indicator
        _warnLabel = new Label
        {
            Text      = "⚠",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Orange,
            Visible   = false,
            Font      = new Font(Font, FontStyle.Bold),
        };
        layout.Controls.Add(_warnLabel, 4, 0);

        UpdateWarning();
    }

    public void SetValue(float v)
    {
        var pct = (int)Math.Clamp(v * 100f, 0f, 100f);
        _bar.Value       = (int)Math.Clamp(v * 1000f, 0f, 1000f);
        _valueLabel.Text = $"{pct}%";
    }

    public void RefreshTargets(List<AudioTarget> targets)
    {
        var currentId = SelectedTargetId;
        _combo.Populate(targets);
        _combo.SelectById(currentId);
        UpdateWarning();
    }

    private void UpdateWarning()
    {
        bool missing = _combo.SelectedTarget?.Category == "__missing__";
        _warnLabel.Visible = missing;
        if (missing)
        {
            var id = _combo.SelectedTarget?.Id ?? "?";
            _toolTip.SetToolTip(_warnLabel,
                $"'{id}' is not currently available.\n" +
                "The binding is kept — it will resume automatically\n" +
                "when the target becomes available again.\n" +
                "Click 'Refresh sessions' or rebind to a new target.");
        }
    }
}

// ---------------------------------------------------------------------------

/// <summary>
/// ComboBox with non-selectable group header rows and a special style for missing targets.
/// Missing targets (category "__missing__") are shown in orange.
/// </summary>
internal sealed class GroupedComboBox : ComboBox
{
    private readonly HashSet<int> _headerIndices = [];
    private int _lastValidIndex = -1;

    public GroupedComboBox()
    {
        DrawMode      = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        ItemHeight    = 18;
    }

    public AudioTarget? SelectedTarget => SelectedItem as AudioTarget;

    /// <summary>
    /// Clears and repopulates items from the given target list, inserting category headers.
    /// </summary>
    public void Populate(List<AudioTarget> targets)
    {
        _headerIndices.Clear();
        Items.Clear();
        _lastValidIndex = -1;

        string? lastCategory = null;
        foreach (var t in targets)
        {
            if (t.Category == "__missing__") continue; // inserted only by SelectById
            if (t.Category != lastCategory)
            {
                _headerIndices.Add(Items.Count);
                Items.Add($"── {t.Category} ──");
                lastCategory = t.Category;
            }
            Items.Add(t);
        }
    }

    /// <summary>
    /// Selects the item whose Id matches. If not found and id is not "unassigned",
    /// adds a special "missing" item (styled in orange) and selects it.
    /// </summary>
    public void SelectById(string id)
    {
        // Remove any previously added missing item
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i] is AudioTarget t && t.Category == "__missing__")
            {
                if (_lastValidIndex > i) _lastValidIndex--;
                _headerIndices.Remove(i);
                Items.RemoveAt(i);
            }
        }

        // Try to find the id in the current list
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] is AudioTarget t &&
                string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                SelectedIndex   = i;
                _lastValidIndex = i;
                return;
            }
        }

        // If id is unassigned or empty, fall back to the first valid item
        if (string.IsNullOrEmpty(id) || id == "unassigned")
        {
            SelectFirstValid();
            return;
        }

        // Target not found — add a placeholder and select it
        var missing = new AudioTarget(id, id, "__missing__");
        Items.Add(missing);
        SelectedIndex   = Items.Count - 1;
        _lastValidIndex = SelectedIndex;
    }

    private void SelectFirstValid()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (!_headerIndices.Contains(i))
            {
                SelectedIndex   = i;
                _lastValidIndex = i;
                return;
            }
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        if (SelectedIndex >= 0 && _headerIndices.Contains(SelectedIndex))
        {
            // Snap to last valid item instead of staying on a header
            SelectedIndex = _lastValidIndex >= 0 ? _lastValidIndex : FindNextValid(SelectedIndex);
            return;
        }
        if (SelectedIndex >= 0)
            _lastValidIndex = SelectedIndex;
        base.OnSelectedIndexChanged(e);
    }

    private int FindNextValid(int from)
    {
        for (int i = from + 1; i < Items.Count; i++)
            if (!_headerIndices.Contains(i)) return i;
        for (int i = from - 1; i >= 0; i--)
            if (!_headerIndices.Contains(i)) return i;
        return 0;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;

        e.DrawBackground();

        var  item     = Items[e.Index];
        if (item is null) return;
        bool isHeader  = _headerIndices.Contains(e.Index);
        bool isMissing = item is AudioTarget at && at.Category == "__missing__";
        var  text      = item.ToString() ?? string.Empty;

        if (isHeader)
        {
            using var brush = new SolidBrush(Color.FromArgb(110, 110, 110));
            using var font  = new Font(e.Font ?? Font, FontStyle.Bold);
            e.Graphics.DrawString(text, font, brush,
                new RectangleF(e.Bounds.X + 2, e.Bounds.Y + 1,
                               e.Bounds.Width - 4, e.Bounds.Height - 1));
        }
        else if (isMissing)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var brush = new SolidBrush(selected ? Color.FromArgb(255, 180, 80) : Color.Orange);
            e.Graphics.DrawString($"⚠  {text}", e.Font ?? Font, brush,
                new RectangleF(e.Bounds.X + 14, e.Bounds.Y + 1,
                               e.Bounds.Width - 16, e.Bounds.Height - 1));
        }
        else
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var brush = new SolidBrush(selected ? SystemColors.HighlightText : e.ForeColor);
            e.Graphics.DrawString(text, e.Font ?? Font, brush,
                new RectangleF(e.Bounds.X + 14, e.Bounds.Y + 1,
                               e.Bounds.Width - 16, e.Bounds.Height - 1));
        }

        e.DrawFocusRectangle();
    }
}
