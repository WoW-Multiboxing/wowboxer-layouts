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
        squad.Describe(log);
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
        squad.Describe(log);
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
        var plan = Squad.Create(o, defaultClients: open.Count);
        plan.Describe(log);
        log.Line();
        return await Arranger.ArrangeAsync(plan, open, log) ? 0 : 1;
    }

    /// <summary>Start Battle.net, then place each WoW window as it appears (the launcher's Start mode).</summary>
    public static async Task<int> StartAsync(CliOptions o, Log log, CancellationToken cancel)
    {
        var plan = Squad.Create(o, defaultClients: 3);
        plan.Describe(log);
        if (o.StartBattleNet) SquadWatcher.EnsureBattleNet(log, o.BattleNetExe, plan.ToolIsAdmin);

        log.Line();
        log.Title(SquadWatcher.BattleNetInstructions(o.Game == Game.Forever));
        log.Title("Then choose the account and press Play - in this order:");
        for (var i = 0; i < plan.Slots.Count; i++)
        {
            var account = i < o.Accounts.Count ? o.Accounts[i] : $"account {i + 1}";
            log.Title($"   {i + 1}. {plan.Slots[i].Slot.Label}   ({account})");
        }
        log.Title("Each game window snaps into place as soon as it appears. Leave this window open (Ctrl+C stops).");
        log.Line();

        var ok = await new SquadWatcher(plan, log).RunAsync(TimeSpan.FromMinutes(o.WatchMinutes), cancel);
        return ok ? 0 : 1;
    }

    /// <summary>Write a report of what Windows sees, then try one synchronous placement per game window.</summary>
    public static async Task<int> DiagnoseAsync(CliOptions o, Log log, string dpiAwareness)
    {
        log.Info($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}   wbx {Version}   {RuntimeInformation.FrameworkDescription}   {RuntimeInformation.OSDescription}");
        log.Info($"wbx runs as administrator: {ProcessInfo.CurrentElevation() == Elevation.Elevated}   DPI: {dpiAwareness}");
        log.Info($"wbx path: {Environment.ProcessPath}");

        var open = WindowFinder.FindGameWindows();
        var squad = Squad.Create(o, defaultClients: open.Count > 0 ? open.Count : 3);
        squad.Describe(log);

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
