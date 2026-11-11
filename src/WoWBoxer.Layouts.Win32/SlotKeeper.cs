using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// The app's "always watching": which open WoW window sits in which slot. A new window takes the first free slot and is
/// placed again every step for 30 s (WoW resizes itself while loading), then nudged once. A closed window frees its
/// slot; the others keep theirs. The caller decides which windows belong to the squad and when to step.
/// </summary>
public sealed class SlotKeeper
{
    private readonly Dictionary<nint, int> _slots = [];          // window -> slot index
    private readonly Dictionary<nint, DateTime> _since = [];     // when the window got its slot (placing runs 30 s from here)
    private readonly Dictionary<nint, PixelRect> _before = [];
    private readonly Dictionary<nint, int> _misses = [];
    private readonly HashSet<nint> _nudged = [];
    private readonly HashSet<(nint, int)> _titled = [];
    private readonly HashSet<nint> _explained = [];
    private readonly Dictionary<nint, TimeSpan> _for = [];        // how long placing runs (30 s for a new window, less after a swap)
    private readonly Dictionary<nint, PixelRect> _lastMiss = [];  // where the game put the window instead, last step
    private readonly Dictionary<nint, int> _held = [];            // steps in a row the game held that same other rect
    private readonly HashSet<nint> _gaveUp = [];

    /// <summary>
    /// The game put the window back to the same other place/size this many steps in a row (one step = 1 s): WoWBoxer
    /// stops resizing it for this placing round and says why once (bug 2026-11-10: Forever clients that keep their own
    /// size were resized every second for 30 s – the windows went back and forth).
    /// </summary>
    public const int HoldLimit = 3;

    /// <summary>A new window's first seconds don't count towards <see cref="HoldLimit"/>: WoW resizes itself while loading.</summary>
    public static readonly TimeSpan LoadingGrace = TimeSpan.FromSeconds(10);

    /// <summary>Windows WoWBoxer stopped resizing in this placing round (the game keeps its own size).</summary>
    public IReadOnlyCollection<nint> GaveUp => _gaveUp;

    /// <summary>Called once per window WoWBoxer stops resizing: window, slot, the rect the game keeps.</summary>
    public Action<nint, int, PixelRect>? Refused { get; set; }

    public IReadOnlyDictionary<nint, int> Slots => _slots;

    /// <summary>The window in a slot, or 0.</summary>
    public nint WindowIn(int slot) => _slots.FirstOrDefault(kv => kv.Value == slot).Key;

    /// <summary>True while a window is still being placed (its first 30 s in the slot).</summary>
    public bool IsSettling(nint h) => _since.TryGetValue(h, out var t) && DateTime.UtcNow - t < _for.GetValueOrDefault(h, SquadWatcher.ReapplyTime) && !_gaveUp.Contains(h);

    /// <summary>
    /// Brings the mapping up to date. Closed windows free their slot; slots past <paramref name="slotCount"/> are given up;
    /// new windows take the first free slot – or the slot they already sit in exactly (<paramref name="sitsIn"/>), so
    /// windows arranged earlier keep their places.
    /// </summary>
    /// <param name="windows">The squad's open windows, oldest first (launch order decides the next free slot).</param>
    /// <returns>Windows that got a slot and windows that lost theirs (closed or past the slot count), with the slot.</returns>
    public (List<(nint Window, int Slot)> Added, List<(nint Window, int Slot)> Removed) Sync(IReadOnlyList<WindowInfo> windows, int slotCount, Func<nint, int>? sitsIn = null)
    {
        var open = windows.Select(w => w.Handle).ToHashSet();
        var removed = new List<(nint, int)>();
        foreach (var (h, slot) in _slots.Where(kv => !open.Contains(kv.Key) || kv.Value >= slotCount).ToList())
        {
            Forget(h);
            removed.Add((h, slot));
        }
        var added = new List<(nint, int)>();
        var fresh = windows.Where(w => !_slots.ContainsKey(w.Handle)).ToList();
        if (sitsIn is not null)
            foreach (var w in fresh.ToList())
            {
                var k = sitsIn(w.Handle);
                if (k < 0 || k >= slotCount || _slots.ContainsValue(k)) continue;
                Assign(w.Handle, k);
                added.Add((w.Handle, k));
                fresh.Remove(w);
            }
        foreach (var w in fresh)
        {
            var free = Enumerable.Range(0, slotCount).FirstOrDefault(s => !_slots.ContainsValue(s), -1);
            if (free < 0) break;   // more windows than slots: the rest are left alone
            Assign(w.Handle, free);
            added.Add((w.Handle, free));
        }
        return (added, removed);
    }

    /// <summary>
    /// One step: windows in their first 30 s are placed (again), then nudged once. A window that keeps refusing to move
    /// is explained once (admin rights, display mode).
    /// </summary>
    public async Task StepAsync(SquadPlan plan, IStatusSink sink)
    {
        foreach (var (h, slot) in _slots.ToList())
        {
            // (the mapping may change while a nudge is awaited: skip windows that left meanwhile)
            if (slot >= plan.Slots.Count || !_since.TryGetValue(h, out var since) || !WindowFinder.Exists(h)) continue;
            var age = DateTime.UtcNow - since;
            var span = _for.GetValueOrDefault(h, SquadWatcher.ReapplyTime);
            if (age >= span || _gaveUp.Contains(h))
            {
                if (span >= SquadWatcher.ReapplyTime && !_gaveUp.Contains(h) && _nudged.Add(h)) await plan.NudgeAsync(h, slot);   // once, after loading settled
                continue;
            }
            var ok = plan.IsPlaced(h, slot);   // checked before this step's move: the last move had a moment to land
            if (ok) plan.Retitle(h, slot, _titled);
            if (Holds(h, ok, age, span))
            {
                _gaveUp.Add(h);
                Refused?.Invoke(h, slot, _lastMiss[h]);
                continue;
            }
            plan.Place(h, slot);
            if (age.TotalSeconds < 3 || _explained.Contains(h)) continue;
            _misses[h] = ok ? 0 : _misses.GetValueOrDefault(h) + 1;
            if (_misses[h] >= 8 && WindowFinder.Exists(h) && _explained.Add(h)) plan.ExplainNotPlaced(sink, h, slot, _before.GetValueOrDefault(h));
        }
    }

    /// <summary>
    /// True when the game has put the window back to the same other rect <see cref="HoldLimit"/> steps in a row (not
    /// counted in a new window's loading seconds): resizing it again would only make it flip between two sizes.
    /// </summary>
    private bool Holds(nint h, bool placed, TimeSpan age, TimeSpan span)
    {
        if (placed || (span >= SquadWatcher.ReapplyTime && age < LoadingGrace))
        {
            _held[h] = 0;
            _lastMiss.Remove(h);
            return false;
        }
        var now = WindowFinder.VisibleRectOf(h);
        _held[h] = _lastMiss.TryGetValue(h, out var last) && last == now ? _held.GetValueOrDefault(h) + 1 : 1;
        _lastMiss[h] = now;
        return _held[h] >= HoldLimit;
    }

    /// <summary>
    /// Places every window again – for 30 s after a pause or "Re-arrange", for <paramref name="span"/> after a swap (the
    /// windows have loaded long ago; a few seconds catch the game's own resize, longer only fights it).
    /// </summary>
    public void Settle(TimeSpan? span = null)
    {
        foreach (var h in _slots.Keys.ToList())
        {
            Assign(h, _slots[h]);
            if (span is { } s) _for[h] = s;
        }
    }

    /// <summary>True when every window in a slot is where the plan wants it, or WoWBoxer has stopped resizing it.</summary>
    public bool AllLanded(SquadPlan plan) =>
        _slots.All(kv => kv.Value >= plan.Slots.Count || !WindowFinder.Exists(kv.Key) || _gaveUp.Contains(kv.Key) || plan.IsPlaced(kv.Key, kv.Value));

    /// <summary>Squad reorder: the window in slot <paramref name="from"/> moves to <paramref name="to"/>, the ones between shift by one.</summary>
    public void Move(int from, int to, int slotCount)
    {
        var order = Enumerable.Range(0, slotCount).Select(WindowIn).ToList();
        var moving = order[from];
        order.RemoveAt(from);
        order.Insert(to, moving);
        Restore(order.Select((h, i) => (h, i)).Where(p => p.h != 0).ToDictionary(p => p.h, p => p.i));
    }

    /// <summary>Puts back a mapping (undo). Windows that closed meanwhile are skipped.</summary>
    public void Restore(IReadOnlyDictionary<nint, int> slots)
    {
        var keep = _slots.Keys.ToList();
        _slots.Clear();
        foreach (var (h, slot) in slots)
            if (keep.Contains(h)) _slots[h] = slot;
        foreach (var h in keep.Where(h => !_slots.ContainsKey(h))) Forget(h);
        _titled.Clear();   // titles follow the slot's name
    }

    private void Assign(nint h, int slot)
    {
        _slots[h] = slot;
        _since[h] = DateTime.UtcNow;
        _before[h] = WindowFinder.VisibleRectOf(h);
        _misses[h] = 0;
        _nudged.Remove(h);
        _explained.Remove(h);
        _for.Remove(h);
        _lastMiss.Remove(h);
        _held.Remove(h);
        _gaveUp.Remove(h);
    }

    private void Forget(nint h)
    {
        _slots.Remove(h);
        _since.Remove(h);
        _before.Remove(h);
        _misses.Remove(h);
        _nudged.Remove(h);
        _explained.Remove(h);
        _for.Remove(h);
        _lastMiss.Remove(h);
        _held.Remove(h);
        _gaveUp.Remove(h);
        _titled.RemoveWhere(t => t.Item1 == h);
    }
}
