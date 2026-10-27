namespace WoWBoxer.Layouts.Core;

/// <summary>One monitor as Windows reports it. <see cref="Index"/> is 1-based, in EnumDisplayMonitors order.</summary>
public sealed record MonitorInfo(int Index, string DeviceName, bool IsPrimary, PixelRect Bounds, PixelRect WorkArea, int Dpi = 96)
{
    public int ScalePercent => (int)Math.Round(Dpi * 100.0 / 96);

    public PixelRect Area(bool useTaskbarArea) => useTaskbarArea ? Bounds : WorkArea;
}

public static class Monitors
{
    /// <summary>Finds the monitor for a slot's monitor value: "primary", a 1-based index or a device name. Null if not found.</summary>
    public static MonitorInfo? Find(IReadOnlyList<MonitorInfo> monitors, string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Equals("primary", StringComparison.OrdinalIgnoreCase))
            return Primary(monitors);
        if (int.TryParse(reference, out var index))
            return monitors.FirstOrDefault(m => m.Index == index);
        return monitors.FirstOrDefault(m => m.DeviceName.Equals(reference, StringComparison.OrdinalIgnoreCase));
    }

    public static MonitorInfo Primary(IReadOnlyList<MonitorInfo> monitors) =>
        monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault()
        ?? throw new InvalidOperationException("No monitors.");
}
