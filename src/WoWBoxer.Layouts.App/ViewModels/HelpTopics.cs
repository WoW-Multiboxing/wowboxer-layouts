namespace WoWBoxer.Layouts.App.ViewModels;

/// <summary>A common problem and what to do about it (Help tab).</summary>
public sealed record HelpTopic(string Problem, string Fix);

/// <summary>The Help tab's "Common problems": the things players actually run into, each with the fix.</summary>
public static class HelpTopics
{
    public static IReadOnlyList<HelpTopic> All { get; } =
    [
        new("Nothing happens when a WoW window opens",
            "The app is stopped (the title bar says so). Press Start placing windows, bottom right on Setup. Settings can start it whenever the app opens."),
        new("The windows do not move at all",
            "Battle.net or WoW runs as administrator, and Windows lets no normal program move it. Close them, start Battle.net normally (not \"Run as administrator\") and try again. The Activity log says \"access denied\" when this is the cause."),
        new("A window jumps back after I move it",
            "While started, the app keeps every window in its slot. Move it on the map in Setup instead, or press Stop to place windows by hand."),
        new("The ends of the action bar are cut off",
            "The game picture is narrower than 4:3. Auto-fit action bar (on by default) sets a smaller UI Scale the next time all clients are closed. Or Settings › Fix action bar now, or ▾ › Shorten this window instead in Your squad."),
        new("Forever (or another client) maximises itself again",
            "It is in \"Windowed (Fullscreen)\". In game: Esc › Options › Graphics › Display Mode › Windowed, once. Then it can be placed."),
        new("The wrong character is in a window",
            "Drag the rows in Your squad by ⋮⋮ – the real windows follow. ▾ › Identify shows which window is which."),
        new("Some WoW windows are ignored",
            "The squad follows the WoW install with the most open windows; another game's windows are left alone. \"Include\" in Your squad lets them join (until the app restarts)."),
        new("Windows activate while the mouse just passes over them",
            "Raise the hover delay (Your squad or Settings), or press Reset to default for 30 ms. Hover to activate is a Windows setting; turning it off stops it at once."),
        new("The layout is suddenly on one monitor",
            "A monitor of the layout was unplugged or off, so the app went back to 1 monitor. Choose the monitors again under Monitors on Setup once it is back (or Ctrl+Z)."),
        new("Windows warns when installing (SmartScreen)",
            "The installer is not signed yet. Click More info › Run anyway, or check the file against SHA256SUMS.txt from the download page first."),
        new("Idle clients get disconnected after about 30 minutes",
            "The game does that to characters without input. WoWBoxer sends no input, ever – so it cannot keep them online."),
    ];
}
