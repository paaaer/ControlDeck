namespace ControlDeck.UI;

/// <summary>
/// Generates the system tray icon programmatically.
/// Green dot = connected, grey dot = disconnected.
/// No external .ico file required.
/// </summary>
public static class TrayIcon
{
    public static Icon Make(bool connected)
    {
        var color = connected ? Color.FromArgb(0x1D, 0x9E, 0x75) : Color.FromArgb(0x88, 0x87, 0x80);

        using var bmp = new Bitmap(32, 32);
        using var g   = Graphics.FromImage(bmp);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Filled circle
        using (var brush = new SolidBrush(color))
            g.FillEllipse(brush, 2, 2, 28, 28);

        // "CD" text
        using var font  = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var white = new SolidBrush(Color.White);
        var text   = "CD";
        var size   = g.MeasureString(text, font);
        var origin = new PointF((32 - size.Width) / 2f, (32 - size.Height) / 2f);
        g.DrawString(text, font, white, origin);

        // Convert Bitmap → Icon
        return Icon.FromHandle(bmp.GetHicon());
    }
}
