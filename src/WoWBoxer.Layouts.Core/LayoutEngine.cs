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
                var rect = Resolve(slot, monitor.Area(profile.Options.UseTaskbarArea), profile.Options);
                return new PlacedSlot(slot, monitor, rect, fallback);
            })
            .ToList();
    }

    public static PixelRect Resolve(LayoutSlot slot, PixelRect area, LayoutOptions options)
    {
        // Round each edge, not the size, so neighbouring slots share an edge with no gap or overlap.
        var x = area.X + Geometry.Round(area.Width * slot.X / 100);
        var y = area.Y + Geometry.Round(area.Height * slot.Y / 100);
        var right = area.X + Geometry.Round(area.Width * (slot.X + slot.W) / 100);
        var bottom = area.Y + Geometry.Round(area.Height * (slot.Y + slot.H) / 100);
        return Fit(new PixelRect(x, y, right - x, bottom - y), slot.Fit ?? options.Fit, slot.Anchor, options.MinAspect);
    }

    /// <summary>Shrinks the height of a rectangle that is narrower than <paramref name="minAspect"/> and anchors it.</summary>
    public static PixelRect Fit(PixelRect rect, FitMode mode, Anchor anchor, double minAspect)
    {
        if (mode == FitMode.None || rect.Height <= 0 || minAspect <= 0 || rect.Aspect >= minAspect) return rect;
        var fitH = Geometry.Floor(rect.Width / minAspect);
        var y = anchor switch
        {
            Anchor.Bottom => rect.Bottom - fitH,
            Anchor.Center => rect.Y + (rect.Height - fitH) / 2,
            _ => rect.Y,
        };
        return rect with { Y = y, Height = fitH };
    }
}
