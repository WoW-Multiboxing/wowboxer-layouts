using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.Cli;

internal static class Commands
{
    public static string Version =>
        typeof(Commands).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    /// <summary>Show monitors, the planned slots and the open WoW windows. Moves nothing.</summary>
    public static int Info(CliOptions o, Log log)
    {
        var squad = Squad.Create(o, defaultClients: 3);
        squad.ShowPlan(log);
        var open = WindowFinder.FindGameWindows();
        log.Info($"WoW windows open right now: {open.Count}");
        foreach (var w in open)
        {
            var (cw, ch) = WindowFinder.ClientSizeOf(w.Handle);
            var borderless = (w.Style & 0x00C00000) == 0;   // no WS_CAPTION
            log.Info($"  {w.ProcessName}.exe  pid {w.Pid}  window {w.Rect}  visible {WindowFinder.VisibleRectOf(w.Handle)}");
            log.Info($"      game renders {cw}x{ch} (aspect {(ch == 0 ? 0 : (double)cw / ch):F3})  {(borderless ? "borderless" : "title bar")}  style 0x{w.Style:X8}");
        }
        return 0;
    }

    /// <summary>Gives every WoW window its title bar and borders back (undo borderless). Moves nothing.</summary>
    public static int Restore(Log log)
    {
        var open = WindowFinder.FindGameWindows();
        if (open.Count == 0)
        {
            log.Warn("No World of Warcraft windows found.");
            return 1;
        }
        foreach (var w in open)
        {
            WindowPlacer.RestoreFrame(w.Handle);
            log.Ok($"Title bar and borders back: {w.ProcessName}.exe at {w.Rect}");
        }
        return 0;
    }

    /// <summary>Saves where the WoW windows are now (oldest = slot 1) as a layout file. Moves nothing.</summary>
    public static int Capture(CliOptions o, Log log)
    {
        var open = WindowFinder.FindGameWindows();
        if (open.Count == 0)
        {
            log.Warn("No World of Warcraft windows found. Arrange them first, then capture.");
            return 1;
        }
        var monitors = MonitorReader.GetMonitors();
        var borderless = open.All(w => (w.Style & 0x00C00000) == 0);   // no WS_CAPTION anywhere
        var name = o.SavePath is null ? "Captured" : Path.GetFileNameWithoutExtension(o.SavePath);
        var profile = ProfileCapture.FromWindows(open.Select(w => WindowFinder.VisibleRectOf(w.Handle)).ToList(), monitors, borderless, name);

        foreach (var (s, p) in profile.Slots.Zip(LayoutEngine.Resolve(profile, monitors)))
            log.Info($"{s.N}. {s.Label,-10} -> {p.Rect}  monitor {p.Monitor.Index}");
        log.Info($"Title bars: {(borderless ? "removed" : "kept")}");

        if (o.SavePath is not null)
        {
            File.WriteAllText(o.SavePath, ProfileJson.Serialize(profile));
            log.Ok($"Saved to {Path.GetFullPath(o.SavePath)}");
            log.Info($"Use it with: wbx arrange --profile \"{o.SavePath}\"   (or wbx start --profile ...)");
        }
        else
            log.Info("Add --save FILE.json to keep it.");
        log.Highlight("Share code:");
        Console.WriteLine(ProfileJson.ToShareCode(profile));
        return 0;
    }

    public static int Layout(CliOptions o, Log log)
    {
        var squad = Squad.Create(o, defaultClients: 3);
        if (o.Json)
        {
            Console.WriteLine(ProfileJson.Serialize(squad.Profile));
            return 0;
        }
        squad.ShowPlan(log);
        log.Line();
        Console.WriteLine(ProfileJson.Serialize(squad.Profile));
        log.Line();
        log.Highlight("Share code:");
        Console.WriteLine(ProfileJson.ToShareCode(squad.Profile));
        return 0;
    }

    /// <summary>Place the WoW windows that are open now, oldest first (the launcher's Arrange mode).</summary>
    public static async Task<int> ArrangeAsync(CliOptions o, Log log)
    {
        var open = WindowFinder.FindGameWindows();
        if (open.Count == 0)
        {
            log.Warn("No World of Warcraft windows found. Start the games first (or run \"wbx diagnose\").");
            return 1;
        }
        var squad = Squad.Create(o, defaultClients: open.Count);
        squad.ShowPlan(log);
        log.Line();

        var n = Math.Min(open.Count, squad.Slots.Count);
        var handles = open.Take(n).Select(w => w.Handle).ToList();
        var before = open.Take(n).Select(w => w.Rect).ToList();
        for (var i = 0; i < n; i++)
        {
            squad.Place(handles[i], i);
            log.Ok($"Placing {squad.Slots[i].Slot.Label}  ({open[i].ProcessName}.exe, was {before[i]})");
        }
        await Task.Delay(1500);
        for (var i = 0; i < n; i++) squad.Place(handles[i], i);   // second pass in case the game resized itself
        await Task.Delay(500);
        await Task.WhenAll(handles.Select((h, i) => squad.NudgeAsync(h, i)));   // make WoW redraw at the new size
        await Task.Delay(1000);

        var titled = new HashSet<(nint, int)>();
        var ok = true;
        for (var i = 0; i < n; i++)
        {
            if (squad.IsPlaced(handles[i], i))
            {
                squad.Retitle(handles[i], i, titled);
                continue;
            }
            if (ok) squad.ExplainNotPlaced(log, handles[i], i, before[i]);   // explain the first one only
            ok = false;
        }
        if (open.Count > squad.Slots.Count) log.Warn($"{open.Count - squad.Slots.Count} extra WoW window(s) left as they are.");
        if (!ok) return 1;
        log.Ok("Done.");
        return 0;
    }

    /// <summary>
    /// Start Battle.net, then place each WoW window as it appears (the launcher's Start mode). New windows
    /// fill the next free slot; placement is re-applied for 30 s because WoW resizes itself while loading.
    /// </summary>
    public static async Task<int> StartAsync(CliOptions o, Log log, CancellationToken cancel)
    {
        var squad = Squad.Create(o, defaultClients: 3);
        squad.ShowPlan(log);

        if (o.StartBattleNet)
        {
            if (BattleNet.IsRunning())
                log.Info("Battle.net is already running.");
            else if (BattleNet.FindExe(o.BattleNetExe) is { } exe)
            {
                if (squad.ToolIsAdmin)
                    log.Warn("wbx runs as administrator, so Battle.net and WoW would too. Better: run wbx without admin rights.");
                log.Info("Starting Battle.net ...");
                BattleNet.Start(exe);
            }
            else
                log.Warn("Battle.net.exe not found - start it yourself (or use --battlenet-exe).");
        }

        log.Line();
        log.Title(o.Game == Game.ClassicEra
            ? "In Battle.net: pick \"World of Warcraft Classic\", set the version dropdown to \"Classic Era\"."
            : "In Battle.net: pick \"World of Warcraft\", set the Game Version menu above Play to \"World of Warcraft: Forever\".");
        log.Title("Then choose the account and press Play - in this order:");
        for (var i = 0; i < squad.Slots.Count; i++)
        {
            var account = i < o.Accounts.Count ? o.Accounts[i] : $"account {i + 1}";
            log.Title($"   {i + 1}. {squad.Slots[i].Slot.Label}   ({account})");
        }
        log.Title("Each game window snaps into place as soon as it appears. Leave this window open (Ctrl+C stops).");
        log.Line();

        const double ReapplySeconds = 30;
        const int MissesBeforeReport = 8;
        var assigned = new Dictionary<nint, int>();      // window -> slot index
        var placedAt = new Dictionary<nint, DateTime>(); // re-applied for 30 s after it appeared
        var before = new Dictionary<nint, PixelRect>();
        var misses = new Dictionary<nint, int>();
        var nudged = new HashSet<nint>();
        var titled = new HashSet<(nint, int)>();
        var reported = false;
        var deadline = DateTime.UtcNow.AddMinutes(o.WatchMinutes);
        var lastPlaced = DateTime.UtcNow;
        var firstRound = true;

        while (DateTime.UtcNow < deadline && !cancel.IsCancellationRequested)
        {
            // Forget closed windows, so a restarted client fills the free slot again.
            foreach (var h in assigned.Keys.Where(h => !WindowFinder.Exists(h)).ToList())
            {
                log.Warn($"{squad.Slots[assigned[h]].Slot.Label} window closed - slot is free again.");
                assigned.Remove(h);
                placedAt.Remove(h);
                nudged.Remove(h);
            }

            foreach (var w in WindowFinder.FindGameWindows())
            {
                if (assigned.ContainsKey(w.Handle)) continue;
                var free = Enumerable.Range(0, squad.Slots.Count).Where(s => !assigned.ContainsValue(s)).ToList();
                if (free.Count == 0) continue;
                assigned[w.Handle] = free[0];
                placedAt[w.Handle] = lastPlaced = DateTime.UtcNow;
                before[w.Handle] = w.Rect;
                misses[w.Handle] = 0;
                log.Ok($"{(firstRound ? "Found an open window" : "New WoW window")} -> {squad.Slots[free[0]].Slot.Label}   (class \"{w.ClassName}\", {w.ProcessName}.exe)");
            }
            firstRound = false;

            foreach (var (h, slot) in assigned)
            {
                var age = (DateTime.UtcNow - placedAt[h]).TotalSeconds;
                if (age >= ReapplySeconds)
                {
                    if (nudged.Add(h)) await squad.NudgeAsync(h, slot);   // once, after loading settled
                    continue;
                }
                var ok = squad.IsPlaced(h, slot);   // checked before this pass's move: the last move had 1 s to land
                if (ok) squad.Retitle(h, slot, titled);
                squad.Place(h, slot);
                if (age >= 3 && !reported)
                {
                    misses[h] = ok ? 0 : misses[h] + 1;
                    if (misses[h] >= MissesBeforeReport)
                    {
                        reported = true;
                        squad.ExplainNotPlaced(log, h, slot, before[h]);
                    }
                }
            }

            if (assigned.Count >= squad.Slots.Count && (DateTime.UtcNow - lastPlaced).TotalSeconds > ReapplySeconds) break;
            try
            {
                await Task.Delay(1000, cancel);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        log.Line();
        var allOk = assigned.Count >= squad.Slots.Count;
        foreach (var (h, slot) in assigned)
        {
            if (squad.IsPlaced(h, slot)) continue;
            allOk = false;
            log.Warn($"{squad.Slots[slot].Slot.Label} is not in place: {WindowFinder.RectOf(h)}");
        }
        if (allOk)
        {
            log.Ok("All windows are in place. Have fun!");
            return 0;
        }
        if (assigned.Count < squad.Slots.Count)
            log.Warn($"Stopped watching ({assigned.Count} of {squad.Slots.Count} windows found). If WoW is open, run \"wbx diagnose\".");
        return 1;
    }

    /// <summary>Write a report of what Windows sees, then try one synchronous placement per game window.</summary>
    public static async Task<int> DiagnoseAsync(CliOptions o, Log log, string dpiAwareness)
    {
        log.Info($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}   wbx {Version}   {RuntimeInformation.FrameworkDescription}   {RuntimeInformation.OSDescription}");
        log.Info($"wbx runs as administrator: {ProcessInfo.CurrentElevation() == Elevation.Elevated}   DPI: {dpiAwareness}");
        log.Info($"wbx path: {Environment.ProcessPath}");

        var open = WindowFinder.FindGameWindows();
        var squad = Squad.Create(o, defaultClients: open.Count > 0 ? open.Count : 3);
        squad.ShowPlan(log);

        log.Line();
        log.Title("Processes (Battle.net / WoW / Agent):");
        foreach (var p in Process.GetProcesses().OrderBy(p => p.ProcessName))
        {
            using (p)
            {
                var name = p.ProcessName;
                if (!name.StartsWith("Battle.net", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Wow", StringComparison.OrdinalIgnoreCase)
                    && !name.StartsWith("Agent", StringComparison.OrdinalIgnoreCase)) continue;
                var pid = (uint)p.Id;
                log.Info($"  {name}  pid {pid}  elevated {ProcessInfo.ElevationOf(pid)}  {ProcessInfo.ImagePath(pid)}");
            }
        }

        log.Line();
        log.Title("Windows of those processes (class / title / size / style):");
        foreach (var h in WindowFinder.TopWindows())
        {
            var w = WindowFinder.Describe(h);
            var path = ProcessInfo.ImagePath(w.Pid);
            if (!WindowFinder.IsWowProcessName(w.ProcessName) && !w.ProcessName.StartsWith("Battle.net", StringComparison.OrdinalIgnoreCase)
                && !w.ClassName.StartsWith("GxWindow", StringComparison.Ordinal) && !WindowFinder.IsWowPath(path)) continue;
            log.Info($"  {w.ProcessName}  [{path}]  class \"{w.ClassName}\"  title \"{w.Title}\"  {w.Rect}  style 0x{w.Style:X8}");
        }

        log.Line();
        log.Title($"Game windows wbx recognises (oldest first): {open.Count}");
        var n = Math.Min(open.Count, squad.Slots.Count);
        for (var i = 0; i < n; i++)
        {
            var h = open[i].Handle;
            var target = squad.Slots[i].Rect;
            if (squad.Profile.Options.Borderless) WindowPlacer.MakeBorderless(h);
            var error = WindowPlacer.TryMove(h, target);
            await Task.Delay(700);
            log.Info($"  {squad.Slots[i].Slot.Label}: before {open[i].Rect}  wanted {target}  after {WindowFinder.RectOf(h)}  move result {error}  (0 = ok, 5 = access denied)");
        }
        log.Line();
        log.Ok($"Saved to {log.FilePath}");
        return 0;
    }
}
