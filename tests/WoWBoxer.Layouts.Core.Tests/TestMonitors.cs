namespace WoWBoxer.Layouts.Core.Tests;

/// <summary>Common monitor setups. Work areas assume a bottom taskbar (48 px at 100 %, scaled with DPI).</summary>
public static class TestMonitors
{
    public static MonitorInfo Single(int width, int height, int dpi = 96, int taskbar = 48) =>
        new(1, @"\\.\DISPLAY1", true, new PixelRect(0, 0, width, height), new PixelRect(0, 0, width, height - taskbar * dpi / 96), dpi);

    /// <summary>The owner's reference setup: 2560×1440, work area 2560×1392.</summary>
    public static IReadOnlyList<MonitorInfo> Reference => [Single(2560, 1440)];

    public static readonly Dictionary<string, IReadOnlyList<MonitorInfo>> Setups = new()
    {
        ["1080p"] = [Single(1920, 1080)],
        ["1440p-125"] = [Single(2560, 1440, 120)],
        ["21:9"] = [Single(3440, 1440)],
        ["4K-150"] = [Single(3840, 2160, 144)],
        ["dual-1080p"] =
        [
            Single(1920, 1080),
            new(2, @"\\.\DISPLAY2", false, new PixelRect(1920, 0, 1920, 1080), new PixelRect(1920, 0, 1920, 1080)),
        ],
        // Mixed DPI, secondary on the left (negative coordinates) and lower.
        ["dual-mixed"] =
        [
            Single(2560, 1440, 120),
            new(2, @"\\.\DISPLAY2", false, new PixelRect(-1920, 360, 1920, 1080), new PixelRect(-1920, 360, 1920, 1032)),
        ],
        // Three mixed screens: a 1080p on the left (100 %), the primary 1440p (125 %), a 4K on the right (150 %);
        // Windows numbers them out of order and the left one is lower.
        ["triple-mixed"] =
        [
            new(1, @"\\.\DISPLAY1", false, new PixelRect(2560, 0, 3840, 2160), new PixelRect(2560, 0, 3840, 2088), 144) { DeviceId = "MON-4K" },
            new(2, @"\\.\DISPLAY2", true, new PixelRect(0, 0, 2560, 1440), new PixelRect(0, 0, 2560, 1380), 120) { DeviceId = "MON-1440" },
            new(3, @"\\.\DISPLAY3", false, new PixelRect(-1920, 200, 1920, 1080), new PixelRect(-1920, 200, 1920, 1032)) { DeviceId = "MON-1080" },
        ],
    };

    public static IEnumerable<string> SingleSetups => Setups.Where(s => s.Value.Count == 1).Select(s => s.Key);
    public static IEnumerable<string> DualSetups => Setups.Where(s => s.Value.Count > 1).Select(s => s.Key);
}
