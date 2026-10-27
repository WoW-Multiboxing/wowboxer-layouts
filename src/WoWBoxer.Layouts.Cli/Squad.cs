using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.Cli;

/// <summary>The layout for this run, resolved to pixels, plus the placement steps every command shares.</summary>
internal sealed class Squad
{
    public LayoutProfile Profile { get; }
    public IReadOnlyList<MonitorInfo> Monitors { get; }
    public IReadOnlyList<PlacedSlot> Slots { get; }
    public bool ToolIsAdmin { get; } = ProcessInfo.CurrentElevation() == Elevation.Elevated;
    private readonly bool _rename;

    private Squad(LayoutProfile profile, IReadOnlyList<MonitorInfo> monitors, bool rename)
    {
        Profile = profile;
        Monitors = monitors;
        Slots = LayoutEngine.Resolve(profile, monitors);
        _rename = rename;
    }

    public static Squad Create(CliOptions o, int defaultClients)
    {
        var monitors = MonitorReader.GetMonitors();
        if (monitors.Count == 0) throw new InvalidOperationException("Windows reports no monitors.");

        LayoutProfile profile;
        if (o.ProfilePath is not null)
            profile = ProfileJson.Deserialize(File.ReadAllText(o.ProfilePath));
        else if (o.ShareCode is not null)
            profile = ProfileJson.FromShareCode(o.ShareCode);
        else
        {
            var options = new LayoutOptions { MinAspect = o.MinAspect, UseTaskbarArea = o.UseTaskbarArea, Borderless = o.Borderless ?? false, ResizeNudge = o.ResizeNudge };
            var clients = Math.Clamp(o.Clients ?? defaultClients, Presets.MinClients, Presets.MaxClients);
            try
            {
                profile = Presets.Create(o.Preset, clients, monitors, o.Monitor, options);
            }
            catch (ArgumentException e) when (e is not ArgumentOutOfRangeException)
            {
                throw new UsageException(e.Message);
            }
        }

        // Command-line switches win over the file, so a saved layout can be tried without borders etc.
        profile = profile with
        {
            Options = profile.Options with
            {
                Borderless = o.Borderless ?? profile.Options.Borderless,
                ResizeNudge = profile.Options.ResizeNudge && o.ResizeNudge,
            },
            Slots = profile.Slots.Select((s, i) => i < o.Names.Count ? s with { Name = o.Names[i] } : s).ToList(),
        };
        return new Squad(profile, monitors, rename: o.Names.Count > 0);
    }

    public void ShowPlan(Log log)
    {
        foreach (var m in Monitors)
            log.Highlight($"Monitor {m.Index}{(m.IsPrimary ? " (primary)" : "")}  {m.DeviceName}  screen {m.Bounds}  work area {m.WorkArea}  scale {m.ScalePercent} %");
        log.Info($"Layout \"{Profile.Name}\"  min aspect {Profile.Options.MinAspect}  borderless {Profile.Options.Borderless}  taskbar area {Profile.Options.UseTaskbarArea}");
        foreach (var s in Slots)
        {
            var z = s.Slot.Z == ZOrder.Top ? "  on top" : "";
            var fallback = s.MonitorFallback ? $"  (monitor {s.Slot.Monitor} not found - using primary)" : "";
            log.Info($"{s.Slot.N}. {s.Slot.Label,-10} -> {s.Rect}  monitor {s.Monitor.Index}{z}{fallback}");
        }
    }

    public PlaceResult Place(nint h, int slot) =>
        WindowPlacer.Place(h, Slots[slot].Rect, Profile.Options.Borderless, Slots[slot].Slot.Z);

    public bool IsPlaced(nint h, int slot) => WindowPlacer.IsAt(h, Slots[slot].Rect);

    public Task NudgeAsync(nint h, int slot) =>
        Profile.Options.ResizeNudge ? WindowPlacer.NudgeAsync(h, Slots[slot].Rect) : Task.CompletedTask;

    /// <summary>Retitles a placed window once ("WoW - Paladin") when names were given.</summary>
    public void Retitle(nint h, int slot, HashSet<(nint, int)> done)
    {
        if (!_rename || Slots[slot].Slot.Name is not { } name || !done.Add((h, slot))) return;
        WindowPlacer.SetTitle(h, "WoW - " + name);
    }

    /// <summary>Explains why a window did not move. No auto-elevation: we tell the user what to do.</summary>
    public void ExplainNotPlaced(Log log, nint h, int slot, PixelRect before)
    {
        var now = WindowFinder.RectOf(h);
        var elevation = ProcessInfo.ElevationOf(WindowFinder.PidOf(h));
        log.Warn($"{Slots[slot].Slot.Label} window did not move into place.");
        log.Detail($"   wanted {Slots[slot].Rect}   is {now}   was {before}   game elevated: {elevation}   wbx admin: {ToolIsAdmin}");
        if (elevation != Elevation.Normal && !ToolIsAdmin)
        {
            log.Warn("WoW seems to run as administrator, so Windows does not let wbx move it.");
            log.Warn("Fix: close WoW and Battle.net, then start Battle.net normally (not \"Run as administrator\";");
            log.Warn("also check Battle.net.exe > Properties > Compatibility). Then start the games again.");
            log.Warn("Last resort: run wbx from a terminal opened as administrator.");
        }
        else if (now != before)
            log.Warn("The window moved, but the game changed its size again. In WoW set Display Mode = Windowed.");
        else
            log.Warn("Run \"wbx diagnose\" with the games open and send the report.");
    }
}
