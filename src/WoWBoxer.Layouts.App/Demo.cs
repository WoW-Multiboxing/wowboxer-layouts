using System.IO;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// "--demo": the app with made-up monitors (2560×1440 primary + 1920×1080), a made-up squad whose windows are all in
/// place, and its own settings, layouts and logs in a temporary folder. Nothing real is read or changed (see
/// <see cref="Sandbox"/>), no update check, no network, no tray icon, and it runs next to an open WoWBoxer.
/// Used for the website's screenshots (tools/make-screenshots.ps1).
/// </summary>
internal static class Demo
{
    public static bool On { get; private set; }

    /// <summary>Settings, layouts, logs and the links cache of this demo run (deleted when it ends).</summary>
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), $"WoWBoxer-demo-{Environment.ProcessId}");

    public static IReadOnlyList<MonitorInfo> Monitors { get; } =
    [
        new(1, @"\\.\DISPLAY1", true, new PixelRect(0, 0, 2560, 1440), new PixelRect(0, 0, 2560, 1392)) { DeviceId = "DEMO-MONITOR-1" },
        new(2, @"\\.\DISPLAY2", false, new PixelRect(2560, 0, 1920, 1080), new PixelRect(2560, 0, 1920, 1032)) { DeviceId = "DEMO-MONITOR-2" },
    ];

    /// <summary>Made-up characters (no real player's names) with their classes, slot 1 first.</summary>
    public static IReadOnlyList<(string Name, string Class)> Squad { get; } =
    [
        ("Aldren", "paladin"), ("Brisa", "priest"), ("Corvel", "mage"), ("Dunmar", "warlock"), ("Elowen", "druid"),
        ("Faroth", "warrior"), ("Gwynna", "shaman"), ("Hesk", "rogue"), ("Ilvara", "hunter"), ("Joskel", "mage"),
    ];

    private static DateTime _clock = new(2026, 11, 4, 20, 15, 0);

    /// <summary>The time on activity lines: a fixed evening in demo mode (same screenshots every run), else now.</summary>
    public static DateTime Now() => On ? _clock = _clock.AddSeconds(3) : DateTime.Now;

    /// <summary>Switches demo mode on (before any window or setting is created) and returns its settings.</summary>
    public static AppSettings Start()
    {
        On = true;
        Sandbox.Start(Monitors);
        Sandbox.SetWindowCount(3);
        Sandbox.Hover = new HoverSettings(true, HoverSettings.DefaultDelayMs, true);
        Sandbox.UiScale = new UiScaleSetting(true, 0.8);
        Directory.CreateDirectory(Root);
        return new AppSettings
        {
            FirstRunDone = true,
            LastSeenVersion = ViewModels.MainViewModel.AppVersion,
            StartPlacingOnOpen = true,
            LastTab = "Setup",
            GameExe = Sandbox.GameExe,
            BattleNetExe = Sandbox.BattleNetExe,
            HoverOriginal = new HoverSettings(false, 0, false),
        };
    }

    public static void End()
    {
        if (!On) return;
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a temporary folder; Windows cleans it up eventually
        }
    }
}
