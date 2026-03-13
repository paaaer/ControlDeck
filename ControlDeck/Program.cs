using ControlDeck.UI;

namespace ControlDeck;

static class Program
{
    public const string AppName    = "ControlDeck";
    public const string AppVersion = "1.0.0";

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Prevent multiple instances
        using var mutex = new Mutex(true, "ControlDeck_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show(
                "ControlDeck is already running.\nCheck the system tray.",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}
