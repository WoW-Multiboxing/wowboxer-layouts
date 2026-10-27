namespace WoWBoxer.Layouts.Core;

/// <summary>A slot resolved to pixels on a concrete monitor. <see cref="MonitorFallback"/> = its monitor was missing, primary used.</summary>
public sealed record PlacedSlot(LayoutSlot Slot, MonitorInfo Monitor, PixelRect Rect, bool MonitorFallback);

/// <summary>Turns a profile (% of a work area) into pixel rectangles.</summary>
public static class LayoutEngine
{
    public static IReadOnlyList<PlacedSlot> Resolve(LayoutProfile profile, IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (monitors.Count == 0) throw new ArgumentException("No monitors.", nameof(monitors));

        return profile.Slots
            .OrderBy(s => s.N)
            .Select(slot =>
            {
                var monitor = Monitors.Find(monitors, slot.Monitor);
                var fallback = monitor is null;
                monitor ??= Monitors.Primary(monitors);
                var rect = Resolve(slot, monitor.Area(profile.Options.UseTaskbarArea), profile.Options, monitor.Dpi);
                return new PlacedSlot(slot, monitor, rect, fallback);
            })
            .ToList();
    }

    public static PixelRect Resolve(LayoutSlot slot, PixelRect area, LayoutOptions options, int dpi = 96)
    {
        // Round each edge, not the size, so neighbouring slots share an edge with no gap or overlap.
        var x = area.X + Geometry.Round(area.Width * slot.X / 100);
        var y = area.Y + Geometry.Round(area.Height * slot.Y / 100);
        var right = area.X + Geometry.Round(area.Width * (slot.X + slot.W) / 100);
        var bottom = area.Y + Geometry.Round(area.Height * (slot.Y + slot.H) / 100);
        return Fit(new PixelRect(x, y, right - x, bottom - y), slot.Fit ?? options.Fit, slot.Anchor, options.MinAspect, FrameSize(options, dpi));
    }

    /// <summary>
    /// What the window frame takes from the game: with title bars (the default) the 1 px side borders and the
    /// 31 px caption + 1 px bottom border, scaled with DPI (measured on Windows 11: 870×696 → 868×664). Borderless: nothing.
    /// </summary>
    public static (int Width, int Height) FrameSize(LayoutOptions options, int dpi = 96) =>
        options.Borderless ? (0, 0) : (Geometry.Round(2 * dpi / 96.0), Geometry.Round(32 * dpi / 96.0));

    /// <summary>The size the game actually draws at inside a window of this (visible) size.</summary>
    public static (int Width, int Height) GameArea(PixelRect window, (int Width, int Height) frame) =>
        (Math.Max(0, window.Width - frame.Width), Math.Max(0, window.Height - frame.Height));

    /// <summary>
    /// Shrinks the height of a rectangle whose game area is narrower than <paramref name="minAspect"/> and anchors it.
    /// <paramref name="frame"/> is what the window frame takes from the game (see <see cref="FrameSize"/>).
    /// </summary>
    public static PixelRect Fit(PixelRect rect, FitMode mode, Anchor anchor, double minAspect, (int Width, int Height) frame = default)
    {
        var (gameW, gameH) = GameArea(rect, frame);
        if (mode == FitMode.None || gameH <= 0 || minAspect <= 0 || (double)gameW / gameH >= minAspect) return rect;
        var fitH = Geometry.Floor(gameW / minAspect) + frame.Height;
        var y = anchor switch
        {
            Anchor.Bottom => rect.Bottom - fitH,
            Anchor.Center => rect.Y + (rect.Height - fitH) / 2,
            _ => rect.Y,
        };
        return rect with { Y = y, Height = fitH };
    }
}
