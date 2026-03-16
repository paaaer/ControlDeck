namespace ControlDeck.UI;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text            = "About ControlDeck";
        Size            = new Size(400, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;
        FormClosing    += (_, e) => { e.Cancel = true; Hide(); };

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            Padding     = new Padding(20),
            RowCount    = 6,
        };
        Controls.Add(layout);

        // App name
        layout.Controls.Add(new Label
        {
            Text     = Program.AppName,
            Font     = new Font("Segoe UI", 18f, FontStyle.Bold),
            AutoSize = true,
            Padding  = new Padding(0, 0, 0, 4),
        });

        // Version
        layout.Controls.Add(new Label
        {
            Text      = $"Version {Program.AppVersion}",
            ForeColor = Color.Gray,
            AutoSize  = true,
            Padding   = new Padding(0, 0, 0, 12),
        });

        // Description
        layout.Controls.Add(new Label
        {
            Text      = "A hardware volume mixer for your PC.\n" +
                        "Control audio sessions with physical sliders\n" +
                        "via a ControlDeckCore (ESP32) device.",
            AutoSize  = true,
            Padding   = new Padding(0, 0, 0, 12),
        });

        // Source
        layout.Controls.Add(MakeLink(
            "github.com/paaaer/ControlDeck",
            "https://github.com/paaaer/ControlDeck"));

        // Spacer
        layout.Controls.Add(new Label { Height = 12 });

        // Close button
        var btnPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var closeBtn = new Button { Text = "Close", AutoSize = true };
        closeBtn.Click += (_, _) => Hide();
        btnPanel.Controls.Add(closeBtn);
        layout.Controls.Add(btnPanel);
    }

    private static LinkLabel MakeLink(string text, string url)
    {
        var lbl = new LinkLabel { Text = text, AutoSize = true, Padding = new Padding(0, 0, 0, 4) };
        lbl.LinkClicked += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* ignore */ }
        };
        return lbl;
    }
}
