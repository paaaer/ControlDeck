using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlDeck;

/// <summary>
/// Persisted application configuration, stored as JSON next to the exe.
/// </summary>
public class AppConfig
{
    public List<SliderConfig> Sliders { get; set; } = [];
    public DeviceConfig Device        { get; set; } = new();
    public UiConfig Ui                { get; set; } = new();

    // -----------------------------------------------------------------------

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string ConfigPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "controldeck_config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, _jsonOpts) ?? new AppConfig();
            }
        }
        catch { /* corrupt config — start fresh */ }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, _jsonOpts);
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* ignore save errors */ }
    }

    public string GetSliderTarget(int index)
    {
        return Sliders.FirstOrDefault(s => s.Index == index)?.Target ?? "unassigned";
    }

    public void SetSliderTarget(int index, string target)
    {
        var existing = Sliders.FirstOrDefault(s => s.Index == index);
        if (existing is not null)
            existing.Target = target;
        else
            Sliders.Add(new SliderConfig { Index = index, Target = target });
    }
}

public class SliderConfig
{
    public int    Index  { get; set; }
    public string Target { get; set; } = "unassigned";
}

public class DeviceConfig
{
    /// <summary>"auto" or a specific port name like "COM3"</summary>
    public string Port { get; set; } = "auto";
    public int    Baud { get; set; } = 115200;
}

public class UiConfig
{
    public bool MinimizeToTray     { get; set; } = true;
    public bool ShowSliderPreview  { get; set; } = true;
}
