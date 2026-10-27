namespace WoWBoxer.Layouts.Core;

[Flags]
public enum ResizeEdges { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8, All = Left | Top | Right | Bottom }

/// <summary>
/// Geometry for the layout editor, in physical pixels (virtual-screen coordinates). The UI converts mouse
/// movement to pixels and calls these; the profile still stores % of the monitor's work area.
/// </summary>
public static class SlotEditing
{
    public const int MinSlotSize = 160;

    /// <summary>The slot's rectangle before the aspect fit (what the user drags).</summary>
    public static PixelRect Raw(LayoutSlot slot, MonitorInfo monitor, LayoutOptions options) =>
        LayoutEngine.Resolve(slot with { Fit = FitMode.None }, monitor.Area(options.UseTaskbarArea), options);

    /// <summary>Stores a pixel rectangle back in the slot as % of the monitor that holds its centre.</summary>
    public static LayoutSlot WithRect(LayoutSlot slot, PixelRect rect, IReadOnlyList<MonitorInfo> monitors, LayoutOptions options)
    {
        var monitor = ProfileCapture.MonitorOf(rect, monitors);
        var area = monitor.Area(options.UseTaskbarArea);
        rect = ClampInto(rect, area);
        return slot with
        {
            Monitor = monitor.IsPrimary ? "primary" : monitor.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            X = Math.Round(Geometry.Percent(rect.X - area.X, area.Width), 4),
            Y = Math.Round(Geometry.Percent(rect.Y - area.Y, area.Height), 4),
            W = Math.Round(Geometry.Percent(rect.Width, area.Width), 4),
            H = Math.Round(Geometry.Percent(rect.Height, area.Height), 4),
        };
    }

    /// <summary>Keeps the size if it fits, then shifts the rectangle inside the area.</summary>
    public static PixelRect ClampInto(PixelRect rect, PixelRect area)
    {
        var w = Math.Min(rect.Width, area.Width);
        var h = Math.Min(rect.Height, area.Height);
        var x = Math.Clamp(rect.X, area.X, area.Right - w);
        var y = Math.Clamp(rect.Y, area.Y, area.Bottom - h);
        return new PixelRect(x, y, w, h);
    }

    /// <summary>Moves or resizes <paramref name="start"/> by a drag of (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
    public static PixelRect Drag(PixelRect start, ResizeEdges edges, int dx, int dy)
    {
        if (edges == ResizeEdges.All) return start with { X = start.X + dx, Y = start.Y + dy };
        int left = start.X, top = start.Y, right = start.Right, bottom = start.Bottom;
        if (edges.HasFlag(ResizeEdges.Left)) left = Math.Min(left + dx, right - MinSlotSize);
        if (edges.HasFlag(ResizeEdges.Right)) right = Math.Max(right + dx, left + MinSlotSize);
        if (edges.HasFlag(ResizeEdges.Top)) top = Math.Min(top + dy, bottom - MinSlotSize);
        if (edges.HasFlag(ResizeEdges.Bottom)) bottom = Math.Max(bottom + dy, top + MinSlotSize);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Snaps the moving edges to the nearest target line within <paramref name="tolerance"/> pixels.
    /// When moving (<see cref="ResizeEdges.All"/>) the whole rectangle shifts, so its size never changes.
    /// </summary>
    public static PixelRect Snap(PixelRect rect, ResizeEdges edges, IReadOnlyCollection<int> xLines, IReadOnlyCollection<int> yLines, int tolerance)
    {
        if (edges == ResizeEdges.All)
        {
            var dx = Nearest([rect.X, rect.Right], xLines, tolerance);
            var dy = Nearest([rect.Y, rect.Bottom], yLines, tolerance);
            return rect with { X = rect.X + dx, Y = rect.Y + dy };
        }
        int left = rect.X, top = rect.Y, right = rect.Right, bottom = rect.Bottom;
        if (edges.HasFlag(ResizeEdges.Left)) left += Nearest([left], xLines, tolerance);
        if (edges.HasFlag(ResizeEdges.Right)) right += Nearest([right], xLines, tolerance);
        if (edges.HasFlag(ResizeEdges.Top)) top += Nearest([top], yLines, tolerance);
        if (edges.HasFlag(ResizeEdges.Bottom)) bottom += Nearest([bottom], yLines, tolerance);
        return new PixelRect(left, top, Math.Max(right - left, 1), Math.Max(bottom - top, 1));
    }

    /// <summary>Lines to snap to: every monitor's work-area edges and the edges of the other slots.</summary>
    public static (List<int> X, List<int> Y) SnapLines(IEnumerable<PixelRect> others, IEnumerable<MonitorInfo> monitors, bool useTaskbarArea)
    {
        var rects = others.Concat(monitors.Select(m => m.Area(useTaskbarArea))).ToList();
        return (rects.SelectMany(r => new[] { r.X, r.Right }).Distinct().ToList(),
                rects.SelectMany(r => new[] { r.Y, r.Bottom }).Distinct().ToList());
    }

    /// <summary>The smallest shift (|shift| ≤ tolerance) that puts one of the edges on a line; 0 if none.</summary>
    private static int Nearest(int[] edges, IReadOnlyCollection<int> lines, int tolerance)
    {
        var best = tolerance + 1;
        var shift = 0;
        foreach (var e in edges)
            foreach (var line in lines)
            {
                var d = line - e;
                if (Math.Abs(d) < best)
                {
                    best = Math.Abs(d);
                    shift = d;
                }
            }
        return best <= tolerance ? shift : 0;
    }
}
