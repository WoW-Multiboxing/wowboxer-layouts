namespace WoWBoxer.Layouts.Core;

/// <summary>Which client plays when "Sound follows the main window" is on.</summary>
public enum SoundFollowMode
{
    /// <summary>The client in the main slot (after every swap); the default.</summary>
    MainSlot,
    /// <summary>The client whose window has the keyboard (the last WoW window that had it, while another program has it).</summary>
    ActiveWindow,
}

/// <summary>A volume-mixer entry's state: its level (0–1) and mute.</summary>
public readonly record struct AudioState(float Volume, bool Muted)
{
    /// <summary>Same within 0.5 % (the mixer stores floats; a round trip is not exact).</summary>
    public bool SameAs(AudioState other) => Muted == other.Muted && Math.Abs(Volume - other.Volume) < 0.005f;

    public override string ToString() => Muted ? $"muted ({Volume:P0})" : $"{Volume:P0}";
}

/// <summary>One audio session in Windows' volume mixer (per process and per output device).</summary>
/// <param name="Id">Unique while it lives: the device id plus the session instance id.</param>
public sealed record AudioSession(string Id, uint Pid, AudioState State);

/// <summary>Windows' per-process volume mixer (Core Audio). The app implements it; tests use a fake.</summary>
public interface IAudioMixer
{
    /// <summary>Every live audio session on every active output device.</summary>
    IReadOnlyList<AudioSession> Sessions();

    /// <summary>Sets one session's level and mute. False if it is gone or Windows refused.</summary>
    bool Set(string id, AudioState state);
}

/// <summary>
/// "Sound follows the main window" (owner, 2026-11-10): the WoW client in the main slot (or the active one) plays at its
/// normal level, the other WoW clients are muted or turned down to a percentage of their normal level. Only Windows'
/// volume mixer changes – nothing is sent to the game – and only for the WoW client processes it is given; every other
/// program's sound is left alone.
/// <para>
/// The normal level of a client is the one it had before WoWBoxer first changed it, kept per client exe in
/// <see cref="Originals"/> (saved in the settings): Windows remembers a program's mixer level per exe, so a client that
/// closed while muted would start muted next time. Those originals are put back by <see cref="Restore"/> (feature off,
/// app exit, uninstall); an exe without a live session then stays listed until one of its clients plays again
/// (<see cref="RestorePending"/>).
/// </para>
/// </summary>
public sealed class SoundFollower(Dictionary<string, AudioState> originals, Action? saveOriginals = null)
{
    public const int DefaultFollowerPercent = 0;

    /// <summary>Normal level per client exe (lower case), from before WoWBoxer changed it. Shared with the settings.</summary>
    public Dictionary<string, AudioState> Originals => originals;

    private readonly Dictionary<string, AudioState> _written = [];
    private uint? _lastLoud;
    private int _lastPercent = -1;
    private HashSet<uint> _lastClients = [];

    /// <summary>What a client's session should be: the loud one at its normal level (unmuted), the others muted (0 %) or at a share of it.</summary>
    public static AudioState Target(AudioState normal, bool loud, int followerPercent)
    {
        if (loud) return new AudioState(normal.Volume, false);
        var percent = Math.Clamp(followerPercent, 0, 100);
        return percent == 0 ? new AudioState(normal.Volume, true) : new AudioState(normal.Volume * percent / 100f, false);
    }

    public static string Key(string exePath) => exePath.Trim().ToLowerInvariant();

    /// <summary>
    /// Brings the mixer in line: <paramref name="loudPid"/> plays, the other <paramref name="clients"/> (pid → exe) follow
    /// <paramref name="followerPercent"/>. All sessions are written when the loud client, the percentage or the set of
    /// clients changed (a swap, a new or closed window); otherwise only sessions that appeared since (a client gets one
    /// when it first plays a sound) – so a follower the user unmuted by hand is not muted again every second.
    /// A change the user made to the loud client in the mixer becomes its normal level. Returns the sessions written.
    /// </summary>
    public int Update(IAudioMixer mixer, uint? loudPid, IReadOnlyDictionary<uint, string> clients, int followerPercent)
    {
        var all = loudPid != _lastLoud || followerPercent != _lastPercent || !_lastClients.SetEquals(clients.Keys);
        _lastLoud = loudPid;
        _lastPercent = followerPercent;
        _lastClients = [.. clients.Keys];
        var sessions = mixer.Sessions();
        var changed = 0;
        var saved = false;
        foreach (var s in sessions)
        {
            if (s.Pid == 0 || !clients.TryGetValue(s.Pid, out var exe) || exe.Length == 0) continue;
            var key = Key(exe);
            if (!originals.TryGetValue(key, out var normal))
            {
                normal = s.State;   // first time WoWBoxer sees this client exe: its level now is the normal one
                originals[key] = normal;
                saved = true;
            }
            var loud = s.Pid == loudPid;
            var known = _written.TryGetValue(s.Id, out var last);
            if (loud && known && !s.State.Muted && !s.State.SameAs(last) && Math.Abs(s.State.Volume - normal.Volume) >= 0.005f)
            {
                normal = normal with { Volume = s.State.Volume };   // the user set a new level for the client that plays
                originals[key] = normal;
                saved = true;
            }
            if (!all && known) continue;
            var target = Target(normal, loud, followerPercent);
            if (s.State.SameAs(target))
            {
                _written[s.Id] = target;
                continue;
            }
            if (mixer.Set(s.Id, target))
            {
                _written[s.Id] = target;
                changed++;
            }
        }
        var live = sessions.Select(s => s.Id).ToHashSet();
        foreach (var gone in _written.Keys.Where(id => !live.Contains(id)).ToList()) _written.Remove(gone);
        if (saved) saveOriginals?.Invoke();
        return changed;
    }

    /// <summary>
    /// Puts every live session of a client exe WoWBoxer changed back to its original level and mute (feature off, app exit,
    /// uninstall). <paramref name="exeOf"/> gives a process's exe path ("" if unknown). Exes with a live session are then
    /// forgotten; the others stay listed for <see cref="RestorePending"/>. Returns the sessions written.
    /// </summary>
    public int Restore(IAudioMixer mixer, Func<uint, string> exeOf)
    {
        _written.Clear();
        _lastLoud = null;
        _lastPercent = -1;
        _lastClients = [];
        return RestorePending(mixer, exeOf);
    }

    /// <summary>While the feature is off: a client of an exe still listed in <see cref="Originals"/> gets its original back. Returns the sessions written.</summary>
    public int RestorePending(IAudioMixer mixer, Func<uint, string> exeOf)
    {
        if (originals.Count == 0) return 0;
        var changed = 0;
        var done = new HashSet<string>();
        foreach (var s in mixer.Sessions())
        {
            if (s.Pid == 0) continue;
            var key = Key(exeOf(s.Pid));
            if (key.Length == 0 || !originals.TryGetValue(key, out var original)) continue;
            if (!s.State.SameAs(original) && mixer.Set(s.Id, original)) changed++;
            done.Add(key);
        }
        foreach (var key in done) originals.Remove(key);
        if (done.Count > 0) saveOriginals?.Invoke();
        return changed;
    }

    /// <summary>
    /// The client (0-based) whose window is the main window of party 1: the crowned main slot when the layout has one
    /// chosen (followed through the swap placement <paramref name="pos"/>), else the clearly largest window as placed now
    /// – so a canvas drag-swap that moved the big window to another client counts too. -1 = no main window (a grid).
    /// </summary>
    public static int MainClient(LayoutProfile home, IReadOnlyList<int>? pos)
    {
        var n = home.Slots.Count;
        var p = pos is not null && pos.Count == n ? WindowSwap.Valid(pos, n) : WindowSwap.Home(n);
        if (home.SwapMainSlots is { Count: > 0 })
        {
            var main = WindowSwap.MainSlots(home)[0];
            if (main >= 0) return WindowSwap.ClientIn(p, main);
        }
        return WindowSwap.ClearlyLargest(WindowSwap.Apply(home, p), 0);
    }
}
