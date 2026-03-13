using NAudio.CoreAudioApi;

namespace ControlDeck.Audio;

/// <summary>
/// Controls Windows audio sessions using NAudio's Core Audio API wrappers.
///
/// Supported targets:
///   "master"        — system master output volume
///   "mic"           — default microphone/input device
///   "system"        — Windows system sounds session
///   "unassigned"    — do nothing
///   "chrome.exe"    — any process name (case-insensitive, with or without .exe)
/// </summary>
public sealed class AudioController : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    /// <summary>
    /// Set volume level (0.0–1.0) for the given target.
    /// Returns true on success.
    /// </summary>
    public bool SetVolume(string target, float level)
    {
        if (string.IsNullOrWhiteSpace(target) || target == "unassigned")
            return true;

        level = Math.Clamp(level, 0f, 1f);

        try
        {
            return target.ToLowerInvariant() switch
            {
                "master" => SetMasterVolume(level),
                "mic"    => SetMicVolume(level),
                _        => SetSessionVolume(target, level),
            };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns all currently active audio session process names
    /// plus the well-known special targets.
    /// </summary>
    public List<string> GetActiveSessions()
    {
        var result = new List<string> { "master", "mic", "system", "unassigned" };

        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(
                DataFlow.Render, Role.Multimedia);

            var sessionManager = device.AudioSessionManager;
            var sessions       = sessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                var pid = (int)session.GetProcessID;
                if (pid == 0) continue;  // system sounds — already in list

                try
                {
                    var proc = System.Diagnostics.Process.GetProcessById(pid);
                    var name = proc.ProcessName + ".exe";
                    if (!result.Contains(name, StringComparer.OrdinalIgnoreCase))
                        result.Add(name);
                }
                catch { /* process already exited */ }
            }
        }
        catch { /* audio engine not ready */ }

        return result;
    }

    // -----------------------------------------------------------------------

    private bool SetMasterVolume(float level)
    {
        using var device = _enumerator.GetDefaultAudioEndpoint(
            DataFlow.Render, Role.Multimedia);
        device.AudioEndpointVolume.MasterVolumeLevelScalar = level;
        return true;
    }

    private bool SetMicVolume(float level)
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(
                DataFlow.Capture, Role.Communications);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = level;
            return true;
        }
        catch { return false; }
    }

    private bool SetSessionVolume(string target, float level)
    {
        // Normalise target — allow "chrome" or "chrome.exe"
        var targetName = target.ToLowerInvariant();
        if (!targetName.EndsWith(".exe")) targetName += ".exe";

        bool matched = false;

        using var device = _enumerator.GetDefaultAudioEndpoint(
            DataFlow.Render, Role.Multimedia);

        var sessions = device.AudioSessionManager.Sessions;

        for (int i = 0; i < sessions.Count; i++)
        {
            using var session = sessions[i];

            // System sounds session (pid == 0)
            if (target == "system" && session.GetProcessID == 0)
            {
                session.SimpleAudioVolume.Volume = level;
                matched = true;
                continue;
            }

            if (session.GetProcessID == 0) continue;

            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(
                    (int)session.GetProcessID);
                var procName = proc.ProcessName.ToLowerInvariant() + ".exe";

                if (procName == targetName)
                {
                    session.SimpleAudioVolume.Volume = level;
                    matched = true;
                }
            }
            catch { /* process exited */ }
        }

        return matched;
    }

    public void Dispose() => _enumerator.Dispose();
}
