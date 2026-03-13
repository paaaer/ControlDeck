namespace ControlDeck.Device;

/// <summary>
/// Parsed CDC2 handshake from ControlDeckCore.
/// Example: CDC2:SLIDERS=4;VERSION=1.0.0;NAME=ControlDeckCore
/// </summary>
public record Handshake(int Sliders, string Version, string Name, string Port);

/// <summary>
/// Parses the ControlDeck wire protocol (CDC2).
/// </summary>
public static class Protocol
{
    private const string HandshakePrefix = "CDC2:";
    private const string FramePrefix     = "V:";

    /// <summary>
    /// Try to parse a CDC2 handshake line.
    /// Returns null if the line is not a valid handshake.
    /// </summary>
    public static Handshake? ParseHandshake(string line, string port)
    {
        line = line.Trim();
        if (!line.StartsWith(HandshakePrefix, StringComparison.Ordinal))
            return null;

        var payload = line[HandshakePrefix.Length..];
        var fields  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in payload.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            fields[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }

        if (!fields.TryGetValue("SLIDERS", out var slidersStr) ||
            !int.TryParse(slidersStr, out int sliders))
            return null;

        fields.TryGetValue("VERSION", out var version);
        fields.TryGetValue("NAME",    out var name);

        return new Handshake(sliders, version ?? "?", name ?? "?", port);
    }

    /// <summary>
    /// Try to parse a V: data frame into normalised float values (0.0–1.0).
    /// Returns null if the line is not a valid frame.
    /// </summary>
    public static float[]? ParseFrame(string line)
    {
        line = line.Trim();
        if (!line.StartsWith(FramePrefix, StringComparison.Ordinal))
            return null;

        var parts = line[FramePrefix.Length..].Split('|');
        var result = new float[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int raw))
                return null;
            result[i] = Math.Clamp(raw / 4095f, 0f, 1f);
        }

        return result;
    }

    public static byte[] InfoCommand  => "CMD:INFO\n"u8.ToArray();
    public static byte[] PingCommand  => "CMD:PING\n"u8.ToArray();
}
