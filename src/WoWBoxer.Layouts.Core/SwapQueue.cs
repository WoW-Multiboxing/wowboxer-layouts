namespace WoWBoxer.Layouts.Core;

/// <summary>
/// One window swap at a time (bug 2026-11-10: Forever clients went back and forth when swap hotkeys came faster than
/// the clients settled). A swap that comes while the previous one is still landing waits; at most
/// <see cref="MaxWaiting"/> wait, further presses are dropped. A swap counts as landed when the caller says so
/// (<see cref="Landed"/>: every moved window is in place) or after <see cref="MaxBusy"/>, whatever comes first.
/// </summary>
public sealed class SwapQueue(TimeSpan maxBusy, int maxWaiting = 2, Func<DateTime>? clock = null)
{
    public enum Offer { RunNow, Waiting, Dropped }

    private readonly Func<DateTime> _now = clock ?? (() => DateTime.UtcNow);
    private readonly Queue<string> _waiting = new();
    private DateTime _busyUntil;

    public TimeSpan MaxBusy => maxBusy;
    public int MaxWaiting => maxWaiting;

    /// <summary>A swap was applied and its windows are still landing.</summary>
    public bool Busy => _now() < _busyUntil;

    public int WaitingCount => _waiting.Count;

    /// <summary>A swap action was asked for: run it now, let it wait for the one that is landing, or drop it.</summary>
    public Offer Ask(string action)
    {
        if (!Busy && _waiting.Count == 0) return Offer.RunNow;
        if (_waiting.Count >= maxWaiting) return Offer.Dropped;
        _waiting.Enqueue(action);
        return Offer.Waiting;
    }

    /// <summary>A swap moved windows: the next one waits until they have landed.</summary>
    public void Started() => _busyUntil = _now() + maxBusy;

    /// <summary>Every window of the last swap is in place (or nothing needs to wait any more).</summary>
    public void Landed() => _busyUntil = default;

    /// <summary>The next waiting action once the last swap has landed; null while busy or when none waits.</summary>
    public string? Next() => !Busy && _waiting.Count > 0 ? _waiting.Dequeue() : null;

    /// <summary>Stop / Home / a new layout: nothing waits any more.</summary>
    public void Clear()
    {
        _waiting.Clear();
        _busyUntil = default;
    }
}
