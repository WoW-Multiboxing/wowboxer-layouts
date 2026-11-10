using System.Globalization;
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
        var open = WindowFinder.FindGameWindows(o.Game);
        log.Info($"WoW windows open right now: {open.Count}");
        foreach (var w in open)
        {
            var (cw, ch) = WindowFinder.ClientSizeOf(w.Handle);
            var borderless = (w.Style & 0x00C00000) == 0;   // no WS_CAPTION
            log.Info($"  {w.ProcessName}.exe  pid {w.Pid}  {GameDetection.DisplayName(w.Game)}  class \"{w.ClassName}\"  window {w.Rect}  visible {WindowFinder.VisibleRectOf(w.Handle)}  top-most {WindowPlacer.IsTopmost(w.Handle)}");
            log.Info($"      game renders {cw}x{ch} (aspect {(ch == 0 ? 0 : (double)cw / ch):F3})  {(borderless ? "borderless" : "title bar")}  style 0x{w.Style:X8}");
            log.Info($"      {BarLine(w, cw, ch)}");
        }
        return 0;
    }

    /// <summary>The WoW clients' entries in Windows' volume mixer (level, mute) per output device. Read-only.</summary>
    public static int Audio(CliOptions o, Log log)
    {
        var open = WindowFinder.FindGameWindows(o.Game);
        var pids = open.ToDictionary(w => w.Pid, w => w);
        using var mixer = (AudioMixer)AudioMixer.Create();
        var sessions = mixer.Sessions();
        log.Info($"WoW windows open: {open.Count}; audio sessions on all output devices: {sessions.Count}");
        foreach (var w in open.OrderBy(w => w.Rect.X).ThenBy(w => w.Rect.Y))
        {
            var mine = sessions.Where(s => s.Pid == w.Pid).ToList();
            var state = mine.Count == 0 ? "no audio session yet (it has not played a sound)" : string.Join(" · ", mine.Select(s => s.State.ToString()));
            log.Info($"  {w.ProcessName}.exe  pid {w.Pid}  window {WindowFinder.VisibleRectOf(w.Handle)}  sound {state}");
        }
        foreach (var s in sessions.Where(s => s.Pid != 0 && !pids.ContainsKey(s.Pid)).GroupBy(s => s.Pid))
            log.Info($"  (not a WoW window) pid {s.Key} {ProcessName(s.Key)}  {string.Join(" · ", s.Select(x => x.State.ToString()))}");
        return o.Rounds > 0 ? FollowTest(o.Rounds, open, mixer, log) : 0;
    }

    /// <summary>
    /// The real-mixer test of "sound follows the main window": each round another client is the main one (in client
    /// order, like next-swaps); after each, exactly that client must be unmuted at its own level and every other program
    /// unchanged. Ends by putting every client back. Moves no window, sends nothing to the game.
    /// </summary>
    private static int FollowTest(int rounds, IReadOnlyList<WindowInfo> open, AudioMixer mixer, Log log)
    {
        var clients = open.Where(w => w.ExePath.Length > 0).GroupBy(w => w.Pid).ToDictionary(g => g.Key, g => g.First().ExePath);
        if (clients.Count < 2)
        {
            log.Warn("Needs at least 2 open WoW clients.");
            return 1;
        }
        var before = mixer.Sessions().ToDictionary(s => s.Id, s => s);
        var originals = new Dictionary<string, AudioState>();
        var follower = new SoundFollower(originals);
        var order = open.OrderBy(w => w.Rect.X).ThenBy(w => w.Rect.Y).Select(w => w.Pid).Distinct().ToList();
        var failures = 0;
        for (var r = 0; r < rounds; r++)
        {
            var main = order[(r + 1) % order.Count];
            follower.Update(mixer, main, clients, 0);
            Thread.Sleep(150);
            var now = mixer.Sessions();
            var loud = now.Where(s => clients.ContainsKey(s.Pid) && !s.State.Muted).Select(s => s.Pid).Distinct().ToList();
            var others = now.Where(s => !clients.ContainsKey(s.Pid) && before.TryGetValue(s.Id, out var b) && !b.State.SameAs(s.State)).ToList();
            var ok = loud.Count == 1 && loud[0] == main && others.Count == 0;
            if (!ok) failures++;
            log.Info($"  round {r + 1,2}: main pid {main}  playing {string.Join(", ", loud)}  other programs changed {others.Count}  {(ok ? "OK" : "WRONG")}");
        }
        var back = follower.Restore(mixer, pid => clients.GetValueOrDefault(pid, ""));
        Thread.Sleep(150);
        var restored = mixer.Sessions().Where(s => before.TryGetValue(s.Id, out var b) && !b.State.SameAs(s.State)).ToList();
        log.Info($"  restored {back} session(s); differing from before the test: {restored.Count}");
        if (restored.Count > 0) failures++;
        if (failures == 0) log.Ok($"Sound follows the main window: {rounds} rounds, always exactly the main client playing; every level put back.");
        else log.Warn($"{failures} problem(s).");
        return failures == 0 ? 0 : 1;
    }

    private static string ProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return "?";
        }
    }

    /// <summary>
    /// Auto-fit's view of the bars: per account which bars are on (character's config-cache.wtf) and where (the WoWBoxer
    /// AddOn's measurement or the game's default layout), and the UI Scale at which all of them fit the reference game
    /// areas and the open windows. Reads files only.
    /// </summary>
    public static int Bars(CliOptions o, Log log)
    {
        foreach (var game in o.Game is { } only ? [only] : new[] { WowGame.ClassicEra, WowGame.Forever })
        {
            var exe = GameInstall.FindExe(null, game);
            log.Line();
            log.Line($"{GameDetection.DisplayName(game)}: {exe ?? "not installed"}", ConsoleColor.Yellow);
            if (exe is null) continue;
            var setting = UiScaleFix.Read(exe);
            log.Info($"UI Scale in Config.wtf: {setting.Display}");
            var folder = Path.GetDirectoryName(exe)!;
            var open = WindowFinder.FindGameWindows(game).Select(w => WindowFinder.ClientSizeOf(w.Handle)).Where(s => s.Width > 0 && s.Height > 0).ToList();
            (int W, int H)[] reference = [(868, 664), (830, 664), (1688, 1360), (2558, 1360)];
            var n = 0;
            foreach (var (_, bars) in BarSetup.Read(folder, game, WtfInfo.Read(folder)))
            {
                log.Info($"  account {++n}: bars {bars.Names} ({bars.Source})");   // no account names in logs
                foreach (var (w, h) in reference.Concat(open.Select(s => (s.Width, s.Height))))
                {
                    var aspect = (double)w / h;
                    var drawn = ActionBars.DrawnScale(game, setting, h);
                    var now = ActionBars.Describe(ActionBars.Check(bars.Bars, aspect, drawn));
                    var fit = ActionBars.FitScale(bars.Bars, aspect);
                    log.Info($"      {w}x{h}: at {drawn:0.00} {(now.Length == 0 ? "all fit" : now)}; fits all at {(fit is { } f ? f.ToString("0.00", CultureInfo.InvariantCulture) : "none (not even 0.64)")}");
                }
            }
        }
        return 0;
    }

    /// <summary>Gives every WoW window its title bar and borders back (undo borderless). Moves nothing.</summary>
    public static int Restore(CliOptions o, Log log)
    {
        var open = WindowFinder.FindGameWindows(o.Game);
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
        var open = WindowFinder.FindGameWindows(o.Game);
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

    /// <summary>Saves a PNG of what each WoW window draws (read-only), oldest first: slot1.png, slot2.png ...</summary>
    public static int Snap(CliOptions o, Log log)
    {
        var open = WindowFinder.FindGameWindows(o.Game);
        if (open.Count == 0)
        {
            log.Warn("No World of Warcraft windows found.");
            return 1;
        }
        var folder = o.OutFolder ?? Path.Combine(Log.Folder, "snaps", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        for (var i = 0; i < open.Count; i++)
        {
            var shot = WindowCapture.Capture(open[i].Handle);
            if (shot is null)
            {
                log.Warn($"Window {i + 1}: could not capture.");
                continue;
            }
            var path = Path.Combine(folder, $"slot{i + 1}.png");
            WindowCapture.SavePng(shot, path);
            log.Ok($"Window {i + 1}: {shot.Width}x{shot.Height} ({shot.Method}) -> {path}");
            log.Info($"   {BarLine(open[i], shot.Width, shot.Height)}");
        }
        return 0;
    }

    /// <summary>The action bar as this window really shows it: its game area and the UI Scale in its install's Config.wtf.</summary>
    private static string BarLine(WindowInfo w, int width, int height)
    {
        if (height == 0) return "action bar: unknown (no game area)";
        var scale = w.ExePath.Length > 0 ? UiScaleFix.Read(w.ExePath) : UiScaleSetting.Automatic;
        var effective = UiScale.EffectiveAspect((double)width / height, scale);
        var fix = effective >= 4.0 / 3 - 0.001 ? "" : $"  (fix: UI Scale {UiScale.ForFullBar((double)width / height):0.00} or lower)";
        return $"action bar: {UiScale.BarVerdict(effective)}  [UI Scale {scale.Display}]{fix}";
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
        var plan = Squad.Create(o, defaultClients: WindowFinder.FindGameWindows(o.Game ?? WowGame.ClassicEra).Count);
        var open = WindowFinder.FindGameWindows(plan.Game);   // only the layout's game: a Forever window never joins a Classic layout
        if (open.Count == 0)
        {
            log.Warn($"No {GameDetection.DisplayName(plan.Game)} windows found. Start the games first (or run \"wbx diagnose\").");
            return 1;
        }
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
        log.Title(SquadWatcher.BattleNetInstructions(plan.Game == WowGame.Forever));
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
        var open = WindowFinder.FindGameWindows(o.Game);
        var plan = Squad.Create(o, defaultClients: open.Count > 0 ? open.Count : 3);
        await Diagnostics.RunAsync(plan, open, log, $"wbx {Version}", dpiAwareness);
        log.Line();
        log.Ok($"Saved to {log.FilePath}");
        return 0;
    }
}
