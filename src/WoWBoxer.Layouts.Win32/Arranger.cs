using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>"Arrange now": places windows that are already open, one per slot.</summary>
public static class Arranger
{
    /// <param name="windows">Windows in slot order: windows[0] goes to slot 1. Extra windows are left alone.</param>
    /// <returns>True if every window ended up in place.</returns>
    public static async Task<bool> ArrangeAsync(SquadPlan plan, IReadOnlyList<WindowInfo> windows, IStatusSink sink, CancellationToken cancel = default)
    {
        if (windows.Count == 0)
        {
            sink.Warn("No World of Warcraft windows found. Start the games first (or run Diagnose).");
            return false;
        }
        var n = Math.Min(windows.Count, plan.Slots.Count);
        var handles = windows.Take(n).Select(w => w.Handle).ToList();
        var before = handles.Select(WindowFinder.VisibleRectOf).ToList();
        for (var i = 0; i < n; i++)
        {
            plan.Place(handles[i], i);
            sink.Ok($"Placing {plan.Slots[i].Slot.Label}  ({windows[i].ProcessName}.exe, was {before[i]})");
        }
        await Task.Delay(1500, cancel);
        for (var i = 0; i < n; i++) plan.Place(handles[i], i);   // second pass: the game may have resized itself, or a frame was just added
        await Task.Delay(500, cancel);
        await Task.WhenAll(handles.Select((h, i) => plan.NudgeAsync(h, i)));
        await Task.Delay(1000, cancel);

        var titled = new HashSet<(nint, int)>();
        var ok = true;
        for (var i = 0; i < n; i++)
        {
            if (plan.IsPlaced(handles[i], i))
            {
                plan.Retitle(handles[i], i, titled);
                continue;
            }
            if (ok) plan.ExplainNotPlaced(sink, handles[i], i, before[i]);   // explain the first one only
            ok = false;
        }
        if (windows.Count > plan.Slots.Count) sink.Warn($"{windows.Count - plan.Slots.Count} extra WoW window(s) left as they are.");
        if (ok) sink.Ok("Done.");
        return ok;
    }
}
