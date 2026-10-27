namespace WoWBoxer.Layouts.Core;

/// <summary>Turns windows the user arranged by hand (Snap, mouse ...) into a profile that reproduces them.</summary>
public static class ProfileCapture
{
    /// <param name="windows">Visible window rectangles in slot order (slot 1 = main).</param>
    /// <param name="borderless">Whether the windows had no title bar when captured; the profile keeps that.</param>
    public static LayoutProfile FromWindows(IReadOnlyList<PixelRect> windows, IReadOnlyList<MonitorInfo> monitors, bool borderless, string name = "Captured")
    {
        if (windows.Count == 0) throw new ArgumentException("No windows to capture.", nameof(windows));

        var slots = windows.Select((rect, i) =>
        {
            var monitor = MonitorOf(rect, monitors);
            var area = monitor.WorkArea;
            // Clamp to the work area: a window hanging a few pixels over the edge still makes a valid slot.
            var x = Math.Clamp(rect.X, area.X, area.Right);
            var y = Math.Clamp(rect.Y, area.Y, area.Bottom);
            var right = Math.Clamp(rect.Right, x, area.Right);
            var bottom = Math.Clamp(rect.Bottom, y, area.Bottom);
            return new LayoutSlot
            {
                N = i + 1,
                Role = i == 0 ? SlotRole.Main : SlotRole.Follower,
                Monitor = monitor.IsPrimary ? "primary" : monitor.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                X = Math.Round(Geometry.Percent(x - area.X, area.Width), 4),
                Y = Math.Round(Geometry.Percent(y - area.Y, area.Height), 4),
                W = Math.Round(Geometry.Percent(right - x, area.Width), 4),
                H = Math.Round(Geometry.Percent(bottom - y, area.Height), 4),
                Fit = FitMode.None,   // reproduce exactly what the user made
            };
        }).ToList();

        return new LayoutProfile { Name = name, Clients = slots.Count, Slots = slots, Options = new LayoutOptions { Borderless = borderless } };
    }

    /// <summary>The monitor that holds the window's centre (primary if the centre is off-screen).</summary>
    public static MonitorInfo MonitorOf(PixelRect rect, IReadOnlyList<MonitorInfo> monitors)
    {
        int cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
        return monitors.FirstOrDefault(m => cx >= m.Bounds.X && cx < m.Bounds.Right && cy >= m.Bounds.Y && cy < m.Bounds.Bottom)
               ?? Monitors.Primary(monitors);
    }
}
