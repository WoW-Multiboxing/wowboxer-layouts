namespace WoWBoxer.Layouts.Core;

public enum AlignEdge { Left, Center, Right, Top, Middle, Bottom }

/// <summary>
/// Advanced editing on the "Your screens" map: operations on several windows at once, in physical pixels
/// (virtual-screen coordinates, so they work across monitors and mixed DPI). The first rectangle is always the
/// REFERENCE (the window selected first). Every operation returns the new rectangles in the same order; the caller
/// stores them back with <see cref="SlotEditing.WithRect"/>, which also keeps each window inside its monitor.
/// </summary>
public static class SlotArrange
{
    /// <summary>Same width and/or height as the reference; every window keeps its top-left corner.</summary>
    public static IReadOnlyList<PixelRect> MatchSize(IReadOnlyList<PixelRect> rects, bool width, bool height)
    {
        if (rects.Count < 2) return rects;
        var r0 = rects[0];
        return rects.Select(r => r with { Width = width ? r0.Width : r.Width, Height = height ? r0.Height : r.Height }).ToList();
    }

    /// <summary>
    /// Aligns to the reference window – or, with one window, to <paramref name="area"/> (its monitor's usable area).
    /// Left/Center/Right move horizontally, Top/Middle/Bottom vertically; sizes never change.
    /// </summary>
    public static IReadOnlyList<PixelRect> Align(IReadOnlyList<PixelRect> rects, AlignEdge edge, PixelRect area)
    {
        if (rects.Count == 0) return rects;
        var to = rects.Count == 1 ? area : rects[0];
        return rects.Select(r => edge switch
        {
            AlignEdge.Left => r with { X = to.X },
            AlignEdge.Center => r with { X = to.X + (to.Width - r.Width) / 2 },
            AlignEdge.Right => r with { X = to.Right - r.Width },
            AlignEdge.Top => r with { Y = to.Y },
            AlignEdge.Middle => r with { Y = to.Y + (to.Height - r.Height) / 2 },
            _ => r with { Y = to.Bottom - r.Height },
        }).ToList();
    }

    /// <summary>
    /// Equal gaps between the windows, left to right (or top to bottom) in their current order. The first and last
    /// window stay where they are; needs three or more windows.
    /// </summary>
    public static IReadOnlyList<PixelRect> Distribute(IReadOnlyList<PixelRect> rects, bool horizontal)
    {
        if (rects.Count < 3) return rects;
        var order = Enumerable.Range(0, rects.Count).OrderBy(i => horizontal ? rects[i].X : rects[i].Y).ThenBy(i => i).ToList();
        int Start(PixelRect r) => horizontal ? r.X : r.Y;
        int Size(PixelRect r) => horizontal ? r.Width : r.Height;
        var first = rects[order[0]];
        var last = rects[order[^1]];
        var span = Start(last) + Size(last) - Start(first);
        var used = order.Sum(i => Size(rects[i]));
        var gaps = order.Count - 1;
        var free = span - used;   // may be negative (overlap): then they overlap evenly
        var result = rects.ToArray();
        var pos = Start(first);
        for (var k = 0; k < order.Count; k++)
        {
            var i = order[k];
            var r = rects[i];
            result[i] = horizontal ? r with { X = pos } : r with { Y = pos };
            // spread the remainder pixel by pixel, so the last window ends exactly where it did
            var gap = free / gaps + (k < Math.Abs(free % gaps) ? Math.Sign(free) : 0);
            pos += Size(r) + gap;
        }
        return result;
    }

    /// <summary>
    /// Fill row (or column): the windows become the same size and fill <paramref name="area"/>'s width (or height) edge to
    /// edge in their current order, with <paramref name="gap"/> pixels between them. The other direction is the
    /// reference's (its top and height for a row, its left and width for a column), kept inside the area.
    /// </summary>
    public static IReadOnlyList<PixelRect> Fill(IReadOnlyList<PixelRect> rects, PixelRect area, bool row, int gap = 0)
    {
        if (rects.Count == 0) return rects;
        gap = Math.Max(0, gap);
        var n = rects.Count;
        var order = Enumerable.Range(0, n).OrderBy(i => row ? rects[i].X : rects[i].Y).ThenBy(i => i).ToList();
        var length = row ? area.Width : area.Height;
        var total = Math.Max(n, length - gap * (n - 1));
        var size = total / n;
        var extra = total % n;   // the first windows get one pixel more, so the last one ends exactly at the edge
        var r0 = SlotEditing.ClampInto(rects[0], area);
        var result = new PixelRect[n];
        var pos = row ? area.X : area.Y;
        for (var k = 0; k < n; k++)
        {
            var s = size + (k < extra ? 1 : 0);
            result[order[k]] = row ? new PixelRect(pos, r0.Y, s, r0.Height) : new PixelRect(r0.X, pos, r0.Width, s);
            pos += s + gap;
        }
        return result;
    }

    /// <summary>
    /// Each window to the given aspect (4:3 = 1.333) keeping its top-left corner and width – or, if it would then
    /// leave its monitor's usable area (<paramref name="areaOf"/>), keeping the height that fits.
    /// </summary>
    public static IReadOnlyList<PixelRect> FitAspect(IReadOnlyList<PixelRect> rects, double aspect, Func<PixelRect, PixelRect> areaOf)
    {
        return rects.Select(r =>
        {
            var area = areaOf(r);
            var w = r.Width;
            var h = (int)Math.Round(w / aspect);
            if (r.Y + h > area.Bottom)
            {
                h = Math.Max(SlotEditing.MinSlotSize, area.Bottom - r.Y);
                w = (int)Math.Round(h * aspect);
            }
            return r with { Width = Math.Max(SlotEditing.MinSlotSize, w), Height = Math.Max(SlotEditing.MinSlotSize, h) };
        }).ToList();
    }

    /// <summary>Resizing with the aspect locked: the drag decides the width (or the height for top/bottom handles).</summary>
    public static PixelRect ResizeLocked(PixelRect start, ResizeEdges edges, int dx, int dy)
    {
        var free = SlotEditing.Drag(start, edges, dx, dy);
        if (edges == ResizeEdges.All || start.Height == 0) return free;
        var aspect = (double)start.Width / start.Height;
        var byWidth = edges.HasFlag(ResizeEdges.Left) || edges.HasFlag(ResizeEdges.Right);
        int w = free.Width, h = free.Height;
        if (byWidth) h = (int)Math.Round(w / aspect);
        else w = (int)Math.Round(h * aspect);
        if (w < SlotEditing.MinSlotSize || h < SlotEditing.MinSlotSize) return start;
        // the corner opposite the handle stays put
        var x = edges.HasFlag(ResizeEdges.Left) ? start.Right - w : start.X;
        var y = edges.HasFlag(ResizeEdges.Top) ? start.Bottom - h : start.Y;
        return new PixelRect(x, y, w, h);
    }

    /// <summary>X, Y, W, H shared by every rectangle, or null where they differ (the inspector shows those blank).</summary>
    public static (int? X, int? Y, int? W, int? H) Common(IReadOnlyList<PixelRect> rects)
    {
        if (rects.Count == 0) return (null, null, null, null);
        int? Same(Func<PixelRect, int> f) => rects.All(r => f(r) == f(rects[0])) ? f(rects[0]) : null;
        return (Same(r => r.X), Same(r => r.Y), Same(r => r.Width), Same(r => r.Height));
    }

    /// <summary>The windows a rubber band touches (any overlap), in their order.</summary>
    public static IReadOnlyList<int> InBand(IReadOnlyList<PixelRect> rects, PixelRect band) =>
        Enumerable.Range(0, rects.Count).Where(i => Overlaps(rects[i], band)).ToList();

    private static bool Overlaps(PixelRect a, PixelRect b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

    /// <summary>The rectangle between two drag points (any direction).</summary>
    public static PixelRect Band(int x1, int y1, int x2, int y2) => new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
}

/// <summary>
/// Which windows are selected on the map, in the order they were picked: the first one is the reference for
/// match size and align. Click selects one; Ctrl/Shift+click adds or removes; a rubber band selects what it touches.
/// </summary>
public sealed class SlotSelection
{
    private readonly List<int> _items = [];

    public IReadOnlyList<int> Items => _items;
    public int Reference => _items.Count > 0 ? _items[0] : -1;
    public bool Contains(int i) => _items.Contains(i);

    /// <summary>A plain click: only this window (a click on a window that is already selected keeps the group, for dragging it).</summary>
    public void Click(int index, bool keepGroupIfSelected = false)
    {
        if (keepGroupIfSelected && _items.Contains(index)) return;
        _items.Clear();
        if (index >= 0) _items.Add(index);
    }

    /// <summary>Ctrl/Shift+click: adds the window, or removes it if it was selected.</summary>
    public void Toggle(int index)
    {
        if (index < 0) return;
        if (!_items.Remove(index)) _items.Add(index);
    }

    /// <summary>Rubber band: those windows; with Ctrl/Shift they are added to the current selection.</summary>
    public void Band(IEnumerable<int> indices, bool add)
    {
        if (!add) _items.Clear();
        foreach (var i in indices)
            if (!_items.Contains(i)) _items.Add(i);
    }

    public void All(int count)
    {
        var keep = _items.Where(i => i < count).ToList();
        _items.Clear();
        _items.AddRange(keep);
        for (var i = 0; i < count; i++)
            if (!_items.Contains(i)) _items.Add(i);
    }

    public void Clear() => _items.Clear();

    /// <summary>Drops indices past the window count (after fewer clients).</summary>
    public void Trim(int count) => _items.RemoveAll(i => i >= count);

    public void Set(IEnumerable<int> indices)
    {
        _items.Clear();
        foreach (var i in indices)
            if (!_items.Contains(i)) _items.Add(i);
    }
}
