using System.Diagnostics;
using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>The Diagnose report (same for wbx and the app): what Windows sees, then one test placement per window.</summary>
public static class Diagnostics
{
    /// <param name="tryPlacement">False ("Report a problem"): only look – no window is moved.</param>
    public static async Task RunAsync(SquadPlan plan, IReadOnlyList<WindowInfo> open, IStatusSink log, string tool, string dpiAwareness, bool tryPlacement = true)
    {
        log.Info($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}   {tool}   {RuntimeInformation.FrameworkDescription}   {RuntimeInformation.OSDescription}");
        log.Info($"Runs as administrator: {plan.ToolIsAdmin}   DPI: {dpiAwareness}");
        log.Info($"Path: {Environment.ProcessPath}");
        plan.Describe(log);

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

        log.Title("Windows of those processes (class / title / size / style):");
        foreach (var h in WindowFinder.TopWindows())
        {
            var w = WindowFinder.Describe(h);
            var path = ProcessInfo.ImagePath(w.Pid);
            if (!GameDetection.IsWowProcessName(w.ProcessName) && !w.ProcessName.StartsWith("Battle.net", StringComparison.OrdinalIgnoreCase)
                && !w.ClassName.StartsWith("GxWindow", StringComparison.Ordinal) && !GameDetection.IsWowPath(path)) continue;
            log.Info($"  {w.ProcessName}  [{path}]  class \"{w.ClassName}\"  title \"{w.Title}\"  {w.Rect}  style 0x{w.Style:X8}");
        }

        log.Title("WoW display settings (Config.wtf, read-only):");
        DescribeConfigs(open.Select(w => ProcessInfo.ImagePath(w.Pid)), log);
        foreach (var w in open)
        {
            var (cw, ch) = WindowFinder.ClientSizeOf(w.Handle);
            log.Info($"  window pid {w.Pid}: game renders {cw}x{ch}, {((w.Style & 0x00C00000) == 0 ? "borderless" : "title bar")}");
        }

        if (!tryPlacement)
        {
            log.Title($"Game windows recognised (oldest first): {open.Count}");
            foreach (var w in open) log.Info($"  pid {w.Pid}  {WindowFinder.VisibleRectOf(w.Handle)}");
            return;
        }
        log.Title($"Game windows recognised (oldest first): {open.Count}");
        var n = Math.Min(open.Count, plan.Slots.Count);
        for (var i = 0; i < n; i++)
        {
            var h = open[i].Handle;
            var target = plan.Slots[i].Rect;
            var before = WindowFinder.VisibleRectOf(h);
            if (plan.Profile.Options.Borderless) WindowPlacer.MakeBorderless(h);
            var error = WindowPlacer.TryMove(h, target);
            await Task.Delay(700);
            log.Info($"  {plan.Slots[i].Slot.Label}: before {before}  wanted {target}  after {WindowFinder.VisibleRectOf(h)}  move result {error}  (0 = ok, 5 = access denied)");
        }
    }

    /// <summary>Display settings from each client install's Config.wtf (read-only).</summary>
    public static void DescribeConfigs(IEnumerable<string> exePaths, IStatusSink log)
    {
        foreach (var exe in exePaths.Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = WowConfig.ConfigPathFor(exe);
            if (!File.Exists(path))
            {
                log.Warn($"  {path}: not found");
                continue;
            }
            var values = WowConfig.Parse(File.ReadAllText(path));
            log.Info($"  {path}  (shared by every client started from this folder)");
            foreach (var (name, meaning) in WowConfig.DisplayCvars)
                log.Info($"    {name,-24} {(values.TryGetValue(name, out var v) ? $"\"{v}\"" : "(not set)"),-14} {meaning}");
        }
    }
}
