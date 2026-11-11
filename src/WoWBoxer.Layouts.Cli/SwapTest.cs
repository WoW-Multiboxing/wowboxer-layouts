using System.Diagnostics;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.Cli;

/// <summary>
/// wbx swaptest --rounds N: the window swap against the real clients (bug 2026-11-10: Forever windows went back and forth
/// after a swap and the main window ended up at the wrong size). Takes the windows as they are placed now as the home
/// layout (largest = main), then swaps N times like a swap hotkey (next client into the main slot) and N times like a
/// drag-swap on the map (two slots trade places), with the app's own placing code: SquadPlan.Place, SlotKeeper with the
/// 5 s swap settle span and its hold limit, and the one-at-a-time rule (SwapQueue). Every window is sampled every 20 ms;
/// a round passes when every window lands in its slot, its game area is the slot's size, and no window leaves its slot
/// again after landing (no back and forth). Ends with fast presses through the queue. A picture of the main window is
/// saved now and then (look at it: action bars at the right size?). At the end every window goes back exactly where it
/// was (rect and "on top"). Stops at once when the mouse or keyboard is used. Moves windows only; nothing is sent to the
/// game, no window is activated.
/// </summary>
internal static class SwapTest
{
    private sealed record Saved(nint Handle, PixelRect Visible, bool Topmost);

    public static async Task<int> RunAsync(CliOptions o, Log log)
    {
        var open = WindowFinder.FindGameWindows(o.Game);
        if (open.Count < 2)
        {
            log.Warn("Needs at least 2 open WoW windows.");
            return 1;
        }
        var rounds = Math.Max(1, o.Rounds);
        var monitors = MonitorReader.GetMonitors();
        // slot order: largest first (the main), then left to right, top to bottom
        var windows = open.OrderByDescending(w => Area(WindowFinder.VisibleRectOf(w.Handle)))
            .ThenBy(w => w.Rect.X).ThenBy(w => w.Rect.Y).Select(w => w.Handle).ToList();
        var saved = windows.Select(h => new Saved(h, WindowFinder.VisibleRectOf(h), WindowPlacer.IsTopmost(h))).ToList();
        var borderless = open.All(w => (w.Style & 0x00C00000) == 0);
        var home = ProfileCapture.FromWindows(saved.Select(s => s.Visible).ToList(), monitors, borderless, "Swap test");
        var mains = WindowSwap.MainSlots(home);
        if (mains[0] < 0)
        {
            log.Warn("No window is clearly the largest – arrange a layout with a main window first.");
            return 1;
        }
        var folder = o.OutFolder ?? Path.Combine(Log.Folder, "swaptest", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        log.Info($"Swap test: {windows.Count} windows, {rounds} hotkey swaps + {rounds} drag-swaps + fast presses. Pictures: {folder}");
        for (var i = 0; i < saved.Count; i++)
            log.Info($"  window {i + 1}: pid {WindowFinder.PidOf(saved[i].Handle)}  {saved[i].Visible}{(saved[i].Topmost ? "  on top" : "")}");

        var startInput = Idle.LastInputTick();
        var failures = 0;
        var keeper = new SlotKeeper();
        // the test's slot order is by size: each window takes the slot it sits in
        keeper.Sync(windows.Select(WindowFinder.Describe).ToList(), windows.Count, h => windows.IndexOf(h));
        var refused = 0;
        keeper.Refused = (h, slot, r) =>
        {
            refused++;
            log.Warn($"    window in slot {slot + 1} keeps {r} – WoWBoxer stopped resizing it (hold limit)");
        };
        try
        {
            // 1) like the swap hotkey "next": the next client into the main slot
            var pos = WindowSwap.Home(windows.Count);
            var cursor = -1;
            var order = Enumerable.Range(0, windows.Count).ToList();
            for (var r = 0; r < rounds; r++)
            {
                (var client, cursor) = WindowSwap.Step(pos, mains[0], order, _ => true, cursor, forward: true);
                pos = WindowSwap.ToMain(pos, client, mains[0]);
                var ok = await RoundAsync($"hotkey {r + 1,2}: client {client + 1} → main", WindowSwap.Apply(home, pos), windows, keeper, monitors, log,
                    snap: r is 0 || r == rounds - 1 ? Path.Combine(folder, $"hotkey{r + 1}-main.png") : null, mainSlot: client);
                if (!ok) failures++;
                if (Idle.LastInputTick() != startInput) return Abort(saved, log);
            }
            // 2) like a drag-swap on the map: two slots trade places in the layout itself (main ↔ follower, then followers)
            var layout = WindowSwap.Apply(home, pos);
            for (var r = 0; r < rounds; r++)
            {
                var n = windows.Count;
                var (a, b) = r % 3 == 2 && n >= 3 ? (1 + r % (n - 1), 1 + (r + 1) % (n - 1)) : (mains[0], 1 + r % (n - 1));
                if (a == b) b = a == 1 ? 2 % n : 1;
                layout = SwapSlots(layout, a, b);
                var main = WindowSwap.ClearlyLargest(layout, 0);
                var ok = await RoundAsync($"drag   {r + 1,2}: slots {a + 1} ↔ {b + 1}", layout, windows, keeper, monitors, log,
                    snap: r == rounds - 1 ? Path.Combine(folder, $"drag{r + 1}-main.png") : null, mainSlot: main);
                if (!ok) failures++;
                if (Idle.LastInputTick() != startInput) return Abort(saved, log);
            }
            // 3) fast presses: 6 "next" presses 250 ms apart go through the queue (one swap at a time, at most 2 waiting)
            if (!await FastAsync(home, mains[0], windows, keeper, monitors, log)) failures++;
            if (Idle.LastInputTick() != startInput) return Abort(saved, log);
        }
        finally
        {
            Put(saved);
        }
        await Task.Delay(1500);
        var back = saved.Count(s => WindowFinder.VisibleRectOf(s.Handle) == s.Visible && WindowPlacer.IsTopmost(s.Handle) == s.Topmost);
        log.Info($"  put back: {back} of {saved.Count} windows exactly where they were");
        if (back != saved.Count) failures++;
        if (refused > 0) log.Info($"  hold limit reached {refused} time(s)");
        if (failures == 0) log.Ok($"Swap test passed: {rounds} hotkey swaps and {rounds} drag-swaps landed without going back and forth; every window put back.");
        else log.Warn($"Swap test: {failures} problem(s).");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>One swap: place every window at once (as MoveWindowsNow), keep placing them like the app's 1 s tick for the settle span, watch.</summary>
    private static async Task<bool> RoundAsync(string name, LayoutProfile layout, List<nint> windows, SlotKeeper keeper,
        IReadOnlyList<MonitorInfo> monitors, Log log, string? snap, int mainSlot)
    {
        // mainSlot: the CLIENT (index into windows) that sits in the main slot now
        var plan = new SquadPlan(layout, monitors, rename: false);
        var before = windows.Select(WindowFinder.VisibleRectOf).ToList();
        for (var c = 0; c < windows.Count; c++) plan.Place(windows[c], c);
        keeper.Settle(TimeSpan.FromSeconds(5));
        var timeline = await WatchAsync(plan, windows, keeper, TimeSpan.FromSeconds(6));
        var landed = timeline.LandedMs;
        var sizes = windows.Select((h, c) => (Want: plan.Slots[c].Rect, Got: WindowFinder.VisibleRectOf(h), Client: WindowFinder.ClientSizeOf(h))).ToList();
        var placed = sizes.All(s => s.Got == s.Want);
        var render = sizes.All(s => s.Client.Width == s.Want.Width && s.Client.Height == s.Want.Height);
        var ok = placed && render && timeline.Bounces == 0 && landed >= 0;
        var main = mainSlot >= 0 ? sizes[mainSlot] : default;
        log.Info($"  {name}: {(landed >= 0 ? $"landed in {landed} ms" : "NOT landed")}, back and forth {timeline.Bounces}, main game area {main.Client.Width}x{main.Client.Height}  {(ok ? "OK" : "WRONG")}");
        if (!ok || timeline.Bounces > 0)
        {
            foreach (var line in timeline.Lines) log.Info("      " + line);
            for (var c = 0; c < sizes.Count; c++)
                if (sizes[c].Got != sizes[c].Want || sizes[c].Client.Width != sizes[c].Want.Width)
                    log.Info($"      slot {c + 1}: wanted {sizes[c].Want}, is {sizes[c].Got}, game area {sizes[c].Client.Width}x{sizes[c].Client.Height} (was {before[c]})");
        }
        if (snap is not null && mainSlot >= 0 && WindowCapture.Capture(windows[mainSlot]) is { } shot)
        {
            WindowCapture.SavePng(shot, snap);
            log.Info($"      picture of the main window: {shot.Width}x{shot.Height} ({shot.Method}) -> {snap}");
        }
        return ok;
    }

    private sealed record Timeline(int LandedMs, int Bounces, List<string> Lines);

    /// <summary>
    /// Samples every window every 20 ms; the keeper steps every 1 s (the app's tick). Landed = every window in its slot
    /// for 300 ms (SwapLandingTick). A bounce = a window that was in its slot leaves it again.
    /// </summary>
    private static async Task<Timeline> WatchAsync(SquadPlan plan, List<nint> windows, SlotKeeper keeper, TimeSpan span)
    {
        var sw = Stopwatch.StartNew();
        var last = new PixelRect[windows.Count];
        var wasIn = new bool[windows.Count];
        var lines = new List<string>();
        var bounces = 0;
        var landed = -1;
        long inSince = -1, nextStep = 1000;
        var sink = new Quiet();
        while (sw.Elapsed < span)
        {
            var all = true;
            for (var c = 0; c < windows.Count; c++)
            {
                var now = WindowFinder.VisibleRectOf(windows[c]);
                var isIn = now == plan.Slots[c].Rect;
                if (now != last[c])
                {
                    var (cw, ch) = WindowFinder.ClientSizeOf(windows[c]);
                    lines.Add($"{sw.ElapsedMilliseconds,5} ms  slot {c + 1}: {now}  game area {cw}x{ch}{(isIn ? "  (in its slot)" : "")}");
                    last[c] = now;
                }
                if (wasIn[c] && !isIn) bounces++;
                wasIn[c] |= isIn;
                all &= isIn;
            }
            if (all && landed < 0)
            {
                if (inSince < 0) inSince = sw.ElapsedMilliseconds;
                else if (sw.ElapsedMilliseconds - inSince >= 300) landed = (int)inSince;
            }
            else if (!all) inSince = -1;
            if (landed >= 0 && sw.ElapsedMilliseconds > Math.Max(landed + 2500, 3000)) break;   // stayed: enough
            if (sw.ElapsedMilliseconds >= nextStep)
            {
                nextStep += 1000;
                await keeper.StepAsync(plan, sink);
            }
            await Task.Delay(20);
        }
        if (landed >= 0 && windows.Select((h, c) => WindowFinder.VisibleRectOf(h) == plan.Slots[c].Rect).Any(x => !x)) landed = -1;
        return new Timeline(landed, bounces, lines);
    }

    /// <summary>6 "next" presses 250 ms apart, through SwapQueue like RunSwapAction: the windows must end where the last run swap put them.</summary>
    private static async Task<bool> FastAsync(LayoutProfile home, int mainSlot, List<nint> windows, SlotKeeper keeper, IReadOnlyList<MonitorInfo> monitors, Log log)
    {
        var queue = new SwapQueue(TimeSpan.FromSeconds(2));
        var pos = WindowSwap.Home(windows.Count);
        var cursor = -1;
        var order = Enumerable.Range(0, windows.Count).ToList();
        SquadPlan? plan = null;
        int ran = 0, dropped = 0;
        void Run()
        {
            (var client, cursor) = WindowSwap.Step(pos, mainSlot, order, _ => true, cursor, forward: true);
            pos = WindowSwap.ToMain(pos, client, mainSlot);
            plan = new SquadPlan(WindowSwap.Apply(home, pos), monitors, rename: false);
            for (var c = 0; c < windows.Count; c++) plan.Place(windows[c], c);
            keeper.Settle(TimeSpan.FromSeconds(5));
            queue.Started();
            ran++;
        }
        var sw = Stopwatch.StartNew();
        long nextPress = 0, nextStep = 1000, inSince = -1;
        var presses = 0;
        var sink = new Quiet();
        while (sw.ElapsedMilliseconds < 20000)
        {
            if (presses < 6 && sw.ElapsedMilliseconds >= nextPress)
            {
                presses++;
                nextPress += 250;
                switch (queue.Ask("next"))
                {
                    case SwapQueue.Offer.RunNow: Run(); break;
                    case SwapQueue.Offer.Dropped: dropped++; break;
                }
            }
            if (queue.Busy && plan is not null && keeper.AllLanded(plan))
            {
                if (inSince < 0) inSince = sw.ElapsedMilliseconds;
                else if (sw.ElapsedMilliseconds - inSince >= 300) { queue.Landed(); inSince = -1; }
            }
            else inSince = -1;
            if (queue.Next() is not null) Run();
            if (presses == 6 && !queue.Busy && queue.WaitingCount == 0) break;
            if (sw.ElapsedMilliseconds >= nextStep && plan is not null)
            {
                nextStep += 1000;
                await keeper.StepAsync(plan, sink);
            }
            await Task.Delay(20);
        }
        var end = plan is null ? null : await WatchAsync(plan, windows, keeper, TimeSpan.FromSeconds(4));
        var ok = end is { LandedMs: >= 0, Bounces: 0 } && ran == 6 - dropped && dropped <= 6;
        log.Info($"  fast presses: 6 presses 250 ms apart → {ran} swaps run one after another, {dropped} skipped (queue full); end: {(end?.LandedMs >= 0 ? "landed" : "NOT landed")}, back and forth {end?.Bounces}  {(ok ? "OK" : "WRONG")}");
        if (!ok && end is not null) foreach (var line in end.Lines) log.Info("      " + line);
        return ok;
    }

    /// <summary>The map's drag-swap: slots <paramref name="a"/> and <paramref name="b"/> trade geometry; roles stay.</summary>
    private static LayoutProfile SwapSlots(LayoutProfile layout, int a, int b)
    {
        var slots = layout.Slots.ToList();
        LayoutSlot sa = slots[a], sb = slots[b];
        slots[a] = sa with { Monitor = sb.Monitor, X = sb.X, Y = sb.Y, W = sb.W, H = sb.H };
        slots[b] = sb with { Monitor = sa.Monitor, X = sa.X, Y = sa.Y, W = sa.W, H = sa.H };
        return layout with { Slots = slots };
    }

    private static void Put(List<Saved> saved)
    {
        foreach (var s in saved)
        {
            if (!WindowFinder.Exists(s.Handle)) continue;
            var borderless = (WindowFinder.Describe(s.Handle).Style & 0x00C00000) == 0;
            WindowPlacer.Place(s.Handle, s.Visible, borderless, s.Topmost ? ZOrder.Top : ZOrder.Normal);
        }
    }

    private static int Abort(List<Saved> saved, Log log)
    {
        Put(saved);
        log.Warn("Stopped: the mouse or keyboard was used. Every window was put back where it was.");
        return 2;
    }

    private static long Area(PixelRect r) => (long)r.Width * r.Height;

    private sealed class Quiet : IStatusSink
    {
        public void Report(StatusLevel level, string message) { }
    }
}

/// <summary>When the mouse or keyboard was last used (GetLastInputInfo).</summary>
internal static class Idle
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LastInput { public uint Size; public uint Time; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInput info);

    public static uint LastInputTick()
    {
        var l = new LastInput { Size = 8 };
        return GetLastInputInfo(ref l) ? l.Time : 0;
    }

    public static double Seconds() => unchecked((uint)Environment.TickCount - LastInputTick()) / 1000.0;
}
