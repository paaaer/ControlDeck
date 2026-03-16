using NAudio.CoreAudioApi;

namespace ControlDeck.Audio;

/// <summary>
/// Controls Windows audio sessions using NAudio's Core Audio API.
///
/// Supported target Id formats:
///   "master"           — default output device master volume
///   "mic"              — default input device volume
///   "system"           — Windows system sounds session
///   "unassigned"       — do nothing
///   "chrome.exe"       — any process name (case-insensitive, .exe optional)
///   "out:Speakers"     — specific output device by friendly name
///   "in:Blue Yeti"     — specific input device by friendly name
/// </summary>
public sealed class AudioController : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    // Cached default endpoints — recreated on failure
    private MMDevice? _cachedOutput;
    private MMDevice? _cachedInput;

    // -----------------------------------------------------------------------
    // Public API

    /// <summary>
    /// Set volume level (0.0–1.0) for the given target Id.
    /// </summary>
    public bool SetVolume(string target, float level)
    {
        if (string.IsNullOrWhiteSpace(target) || target == "unassigned")
            return true;

        level = Math.Clamp(level, 0f, 1f);

        try
        {
            if (target.StartsWith("out:", StringComparison.OrdinalIgnoreCase))
                return SetNamedOutputVolume(target[4..], level);

            if (target.StartsWith("in:", StringComparison.OrdinalIgnoreCase))
                return SetNamedInputVolume(target[3..], level);

            return target.ToLowerInvariant() switch
            {
                "master" => SetMasterVolume(level),
                "mic"    => SetMicVolume(level),
                _        => SetSessionVolume(target, level),
            };
        }
        catch { return false; }
    }

    /// <summary>
    /// Returns all currently bindable audio targets, grouped by category.
    /// Order: None → Output → Input → Applications
    /// </summary>
    public List<AudioTarget> GetAudioTargets()
    {
        var targets = new List<AudioTarget>
        {
            new("unassigned", "— Unassigned —", "None"),
        };

        // ── Output ──────────────────────────────────────────────────────────
        try
        {
            using var defaultOut = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            targets.Add(new("master", $"Master — {defaultOut.FriendlyName}", "Output"));
            targets.Add(new("system", "System Sounds", "Output"));

            // Additional (non-default) output devices
            var outputs = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var dev in outputs)
            {
                using (dev)
                {
                    if (dev.ID == defaultOut.ID) continue;
                    targets.Add(new($"out:{dev.FriendlyName}", dev.FriendlyName, "Output"));
                }
            }
        }
        catch
        {
            targets.Add(new("master", "Master Volume", "Output"));
            targets.Add(new("system", "System Sounds", "Output"));
        }

        // ── Input ────────────────────────────────────────────────────────────
        try
        {
            using var defaultIn = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            targets.Add(new("mic", $"Mic — {defaultIn.FriendlyName}", "Input"));

            var inputs = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var dev in inputs)
            {
                using (dev)
                {
                    if (dev.ID == defaultIn.ID) continue;
                    targets.Add(new($"in:{dev.FriendlyName}", dev.FriendlyName, "Input"));
                }
            }
        }
        catch
        {
            targets.Add(new("mic", "Default Microphone", "Input"));
        }

        // ── Applications ────────────────────────────────────────────────────
        try
        {
            using var outDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = outDevice.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                var pid = (int)session.GetProcessID;
                if (pid == 0) continue; // system sounds — already in list

                try
                {
                    var proc = System.Diagnostics.Process.GetProcessById(pid);
                    var id   = proc.ProcessName.ToLowerInvariant() + ".exe";

                    if (targets.Any(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    targets.Add(new(id, proc.ProcessName, "Applications"));
                }
                catch { /* process already exited */ }
            }
        }
        catch { /* audio engine not ready */ }

        return targets;
    }

    // -----------------------------------------------------------------------
    // Private helpers

    private bool SetMasterVolume(float level)
    {
        try
        {
            _cachedOutput ??= _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _cachedOutput.AudioEndpointVolume.MasterVolumeLevelScalar = level;
            return true;
        }
        catch
        {
            _cachedOutput?.Dispose();
            _cachedOutput = null;
            return false;
        }
    }

    private bool SetMicVolume(float level)
    {
        try
        {
            _cachedInput ??= _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            _cachedInput.AudioEndpointVolume.MasterVolumeLevelScalar = level;
            return true;
        }
        catch
        {
            _cachedInput?.Dispose();
            _cachedInput = null;
            return false;
        }
    }

    private bool SetNamedOutputVolume(string friendlyName, float level)
    {
        var outputs = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var dev in outputs)
        {
            using (dev)
            {
                if (!string.Equals(dev.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase)) continue;
                dev.AudioEndpointVolume.MasterVolumeLevelScalar = level;
                return true;
            }
        }
        return false;
    }

    private bool SetNamedInputVolume(string friendlyName, float level)
    {
        var inputs = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        foreach (var dev in inputs)
        {
            using (dev)
            {
                if (!string.Equals(dev.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase)) continue;
                dev.AudioEndpointVolume.MasterVolumeLevelScalar = level;
                return true;
            }
        }
        return false;
    }

    private bool SetSessionVolume(string target, float level)
    {
        var targetName = target.ToLowerInvariant();
        if (!targetName.EndsWith(".exe")) targetName += ".exe";

        bool matched = false;

        using var device   = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var       sessions = device.AudioSessionManager.Sessions;

        for (int i = 0; i < sessions.Count; i++)
        {
            using var session = sessions[i];

            if (target == "system" && session.GetProcessID == 0)
            {
                session.SimpleAudioVolume.Volume = level;
                matched = true;
                continue;
            }

            if (session.GetProcessID == 0) continue;

            try
            {
                var proc     = System.Diagnostics.Process.GetProcessById((int)session.GetProcessID);
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

    public void Dispose()
    {
        _cachedOutput?.Dispose();
        _cachedInput?.Dispose();
        _enumerator.Dispose();
    }
}
