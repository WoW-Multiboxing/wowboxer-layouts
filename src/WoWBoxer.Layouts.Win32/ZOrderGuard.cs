using WoWBoxer.Layouts.Core;
using static WoWBoxer.Layouts.Win32.NativeMethods;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// Keeps "always on top" true to the layout. Listens passively (out-of-context WinEvent hooks, nothing injected, no
/// input) for foreground changes and for WoW windows moving, resizing, restoring or changing z-order, and then sets or
/// clears HWND_TOPMOST again where a window no longer matches its slot. Must be created on a thread with a message loop
/// (the app's UI thread); the callbacks arrive on that thread.
/// </summary>
public sealed unsafe class ZOrderGuard : IDisposable
{
    private static ZOrderGuard? _active;   // one guard per process (the callback is static)

    private readonly Func<IReadOnlyList<(nint Handle, ZOrder Z)>> _wanted;
    private readonly Action<nint, ZOrder>? _corrected;
    private readonly List<nint> _hooks = [];
    private readonly Dictionary<nint, DateTime> _lastFix = [];
    private bool _busy;

    /// <param name="wanted">The open WoW windows and the z-order their slot wants (read on every event).</param>
    /// <param name="corrected">Called after a window's "on top" had to be put right (for the activity log).</param>
    public ZOrderGuard(Func<IReadOnlyList<(nint Handle, ZOrder Z)>> wanted, Action<nint, ZOrder>? corrected = null)
    {
        _wanted = wanted;
        _corrected = corrected;
        _active?.Dispose();
        _active = this;
        if (Sandbox.Active) return;   // demo mode: no real windows to watch
        const uint flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
        _hooks.Add(SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, &OnEvent, 0, 0, flags));
        _hooks.Add(SetWinEventHook(EVENT_SYSTEM_MINIMIZEEND, EVENT_SYSTEM_MINIMIZEEND, 0, &OnEvent, 0, 0, flags));
        _hooks.Add(SetWinEventHook(EVENT_OBJECT_REORDER, EVENT_OBJECT_REORDER, 0, &OnEvent, 0, 0, flags));
        _hooks.Add(SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, 0, &OnEvent, 0, 0, flags));
        _hooks.RemoveAll(h => h == 0);
    }

    /// <summary>How many times a window's "on top" had to be put right since start (for tests and Diagnose).</summary>
    public int Corrections { get; private set; }

    public bool IsListening => _hooks.Count > 0;

    /// <summary>
    /// Checks every slot now and corrects what differs. Returns how many windows were corrected. <paramref name="ours"/>:
    /// the change comes from the app itself (a toggle, a reorder), so it is not reported as a correction.
    /// </summary>
    public int Reassert(bool ours = false)
    {
        if (_busy) return 0;
        _busy = true;   // our own SetWindowPos raises events too
        try
        {
            var fixedNow = 0;
            foreach (var (h, z) in _wanted())
            {
                // Our change is asynchronous: give it a moment to land before correcting the same window again.
                if (!ours && _lastFix.TryGetValue(h, out var at) && DateTime.UtcNow - at < TimeSpan.FromMilliseconds(400)) continue;
                if (!WindowPlacer.ApplyZOrder(h, z)) continue;
                _lastFix[h] = DateTime.UtcNow;
                fixedNow++;
                if (ours) continue;
                Corrections++;
                _corrected?.Invoke(h, z);
            }
            return fixedNow;
        }
        finally
        {
            _busy = false;
        }
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void OnEvent(nint hook, uint ev, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        var guard = _active;
        if (guard is null || guard._busy) return;
        try
        {
            // Location events fire for every window and many child objects (the caret, the cursor …): only the WoW
            // windows we place count. Reorder events name the parent (often the desktop), so they always re-check –
            // a re-check reads one style bit per window and changes nothing when all is right.
            if (ev == EVENT_OBJECT_LOCATIONCHANGE)
            {
                if (idObject != OBJID_WINDOW) return;
                var ours = false;
                foreach (var (h, _) in guard._wanted())
                    if (h == hwnd) { ours = true; break; }
                if (!ours) return;
            }
            guard.Reassert();
        }
        catch
        {
            // never let an exception cross into Windows' callback
        }
    }

    public void Dispose()
    {
        foreach (var h in _hooks) UnhookWinEvent(h);
        _hooks.Clear();
        if (_active == this) _active = null;
    }
}
