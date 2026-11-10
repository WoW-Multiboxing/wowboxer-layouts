namespace WoWBoxer.Layouts.Core;

/// <summary>
/// Stops a repeating action that never settles. <see cref="Allow"/> counts the events of one key (a window, a hotkey)
/// within a time window; after <paramref name="limit"/> of them the key is blocked (until <see cref="Reset"/> or the
/// pause ends). Used against fights with another program (#help 2026-11-10, Altsy: windows flipped non-stop): WoWBoxer
/// puts "on top" right, the other tool undoes it, and so on.
/// </summary>
public sealed class RateGuard<TKey>(int limit, TimeSpan window, TimeSpan? pause = null, Func<DateTime>? clock = null) where TKey : notnull
{
    private readonly Dictionary<TKey, Queue<DateTime>> _seen = [];
    private readonly Dictionary<TKey, DateTime> _blockedUntil = [];
    private readonly Func<DateTime> _now = clock ?? (() => DateTime.UtcNow);

    /// <summary>Null: the key is never unblocked by time (only by <see cref="Reset"/>).</summary>
    public TimeSpan? Pause { get; } = pause;

    public bool IsBlocked(TKey key)
    {
        if (!_blockedUntil.TryGetValue(key, out var until)) return false;
        if (_now() < until) return true;
        _blockedUntil.Remove(key);
        _seen.Remove(key);
        return false;
    }

    /// <summary>
    /// One more event for <paramref name="key"/>. False when it is blocked; <paramref name="justBlocked"/> is true on the
    /// event that crossed the limit (log it once).
    /// </summary>
    public bool Allow(TKey key, out bool justBlocked)
    {
        justBlocked = false;
        if (IsBlocked(key)) return false;
        var now = _now();
        if (!_seen.TryGetValue(key, out var times)) _seen[key] = times = new Queue<DateTime>();
        while (times.Count > 0 && now - times.Peek() > window) times.Dequeue();
        times.Enqueue(now);
        if (times.Count <= limit) return true;
        _blockedUntil[key] = Pause is { } p ? now + p : DateTime.MaxValue;
        justBlocked = true;
        return false;
    }

    public void Reset()
    {
        _seen.Clear();
        _blockedUntil.Clear();
    }

    public IReadOnlyCollection<TKey> Blocked => _blockedUntil.Keys.ToList().Where(IsBlocked).ToList();
}
