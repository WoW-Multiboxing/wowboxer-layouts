namespace WoWBoxer.Layouts.Core;

/// <summary>How the windows are laid out inside an area: one row, one column, or a grid.</summary>
public enum TileArrangement { Row, Column, Grid }

/// <summary>Options for filling an area: arrangement, grid size (0 = automatic), gap in pixels, order.</summary>
/// <param name="ByNumber">Tile in client-number order instead of the windows' current left-to-right / top-to-bottom order.</param>
public sealed record TileOptions(TileArrangement Arrangement = TileArrangement.Grid, int Rows = 0, int Columns = 0, int Gap = 0, bool ByNumber = false);

/// <summary>
/// Advanced editing: filling a custom area with the selected windows (in addition to Fill row / column for the whole
/// monitor) – the largest free space, the selection's own span, or a rectangle the user draws or types. Physical pixels.
/// </summary>
public static class AreaFill
{
    // ---------- free space ----------

    /// <summary>
    /// The maximal free rectangles of <paramref name="area"/> (not covered by any of <paramref name="obstacles"/>, which
    /// may overlap each other and stick out of the area). Maximal = not inside another free rectangle. Largest first.
    /// </summary>
    public static IReadOnlyList<PixelRect> FreeRects(PixelRect area, IEnumerable<PixelRect> obstacles, int minSize = 1)
    {
        var blocks = obstacles.Select(o => Intersect(o, area)).Where(o => o is { Width: > 0, Height: > 0 }).Select(o => o!.Value).ToList();
        var xs = blocks.SelectMany(b => new[] { b.X, b.Right }).Append(area.X).Append(area.Right).Distinct().Order().ToArray();
        var ys = blocks.SelectMany(b => new[] { b.Y, b.Bottom }).Append(area.Y).Append(area.Bottom).Distinct().Order().ToArray();
        int nx = xs.Length - 1, ny = ys.Length - 1;
        // cell (i, j) = [xs[i], xs[i+1]) × [ys[j], ys[j+1]); covered if any block covers it
        var covered = new int[nx + 1, ny + 1];   // 2-D prefix sums of covered cells
        for (var i = 0; i < nx; i++)
            for (var j = 0; j < ny; j++)
            {
                var c = blocks.Any(b => b.X <= xs[i] && xs[i + 1] <= b.Right && b.Y <= ys[j] && ys[j + 1] <= b.Bottom) ? 1 : 0;
                covered[i + 1, j + 1] = c + covered[i, j + 1] + covered[i + 1, j] - covered[i, j];
            }
        int Count(int i0, int j0, int i1, int j1) => covered[i1, j1] - covered[i0, j1] - covered[i1, j0] + covered[i0, j0];

        var free = new List<PixelRect>();
        for (var i0 = 0; i0 < nx; i0++)
            for (var j0 = 0; j0 < ny; j0++)
                for (var i1 = i0 + 1; i1 <= nx; i1++)
                {
                    if (Count(i0, j0, i1, j0 + 1) > 0) break;   // the first row of this width is blocked: wider is blocked too
                    for (var j1 = j0 + 1; j1 <= ny; j1++)
                    {
                        if (Count(i0, j0, i1, j1) > 0) break;
                        // maximal: cannot grow by one cell in any direction
                        var grow = (i0 > 0 && Count(i0 - 1, j0, i0, j1) == 0) || (i1 < nx && Count(i1, j0, i1 + 1, j1) == 0)
                            || (j0 > 0 && Count(i0, j0 - 1, i1, j0) == 0) || (j1 < ny && Count(i0, j1, i1, j1 + 1) == 0);
                        if (grow) continue;
                        var r = new PixelRect(xs[i0], ys[j0], xs[i1] - xs[i0], ys[j1] - ys[j0]);
                        if (r.Width >= minSize && r.Height >= minSize) free.Add(r);
                    }
                }
        return free.Distinct().OrderByDescending(r => (long)r.Width * r.Height).ThenBy(r => r.Y).ThenBy(r => r.X).ToList();
    }

    /// <summary>
    /// "Fill empty space": of the free rectangles (on every monitor), the one the selection is mostly in – by overlap
    /// with the selected windows – or, when the selection overlaps none, the largest. Null when there is no free space.
    /// </summary>
    public static PixelRect? EmptySpaceFor(IReadOnlyList<PixelRect> selected, IEnumerable<(PixelRect Area, IReadOnlyList<PixelRect> Others)> monitors, int minSize = SlotEditing.MinSlotSize)
    {
        var candidates = monitors.SelectMany(m => FreeRects(m.Area, m.Others, minSize)).ToList();
        if (candidates.Count == 0) return null;
        long Overlap(PixelRect f) => selected.Sum(s => Intersect(s, f) is { } i ? (long)i.Width * i.Height : 0);
        return candidates.OrderByDescending(Overlap).ThenByDescending(f => (long)f.Width * f.Height).First();
    }

    // ---------- the selection's own span ----------

    /// <summary>"Fill selection span": from the leftmost/topmost edge to the rightmost/bottommost edge of the selection.</summary>
    public static PixelRect Span(IReadOnlyList<PixelRect> rects)
    {
        int l = rects.Min(r => r.X), t = rects.Min(r => r.Y), rr = rects.Max(r => r.Right), b = rects.Max(r => r.Bottom);
        return new PixelRect(l, t, rr - l, b - t);
    }

    // ---------- tiling ----------

    /// <summary>
    /// Tiles the windows into <paramref name="area"/>: equal cells (to the pixel, the last row/column ends exactly at the
    /// edge), in the windows' current order (left-to-right for a row, top-to-bottom for a column, reading order for a
    /// grid) or by client number. Returns the new rectangles in the input order.
    /// </summary>
    public static IReadOnlyList<PixelRect> Tile(IReadOnlyList<PixelRect> rects, PixelRect area, TileOptions o)
    {
        var n = rects.Count;
        if (n == 0) return rects;
        var (rows, cols) = GridFor(n, area, o);
        var order = Order(rects, o).ToList();
        var gap = Math.Max(0, o.Gap);
        var xs = Cuts(area.X, area.Width, cols, gap);
        var ys = Cuts(area.Y, area.Height, rows, gap);
        var result = new PixelRect[n];
        for (var k = 0; k < n; k++)
        {
            int c, r;
            if (o.Arrangement == TileArrangement.Column) { c = k / rows; r = k % rows; }
            else { r = k / cols; c = k % cols; }
            result[order[k]] = new PixelRect(xs[c].Start, ys[r].Start, xs[c].Size, ys[r].Size);
        }
        return result;
    }

    /// <summary>Rows × columns for the arrangement: a row is 1 × n, a column n × 1; a grid uses the set size or the best one.</summary>
    public static (int Rows, int Columns) GridFor(int n, PixelRect area, TileOptions o)
    {
        if (o.Arrangement == TileArrangement.Row) return (1, n);
        if (o.Arrangement == TileArrangement.Column) return (n, 1);
        if (o.Rows > 0 && o.Columns > 0 && o.Rows * o.Columns >= n) return (o.Rows, o.Columns);
        if (o.Columns > 0) return ((n + o.Columns - 1) / o.Columns, o.Columns);
        if (o.Rows > 0) return (o.Rows, (n + o.Rows - 1) / o.Rows);
        // automatic: cells as close to 4:3 as possible (the whole Classic action bar), few empty cells
        var best = (Rows: 1, Columns: n);
        var bestScore = double.MinValue;
        for (var cols = 1; cols <= n; cols++)
        {
            var rows = (n + cols - 1) / cols;
            if ((rows - 1) * cols >= n) continue;   // a whole empty row
            var aspect = (double)area.Width / cols / Math.Max(1.0, (double)area.Height / rows);
            var shape = Math.Min(aspect / (4.0 / 3), (4.0 / 3) / aspect);
            var score = shape - 0.08 * (rows * cols - n);
            if (score > bestScore + 1e-9)
            {
                bestScore = score;
                best = (rows, cols);
            }
        }
        return best;
    }

    /// <summary>The order windows are placed in: by client number, or by their current position.</summary>
    public static IEnumerable<int> Order(IReadOnlyList<PixelRect> rects, TileOptions o)
    {
        var idx = Enumerable.Range(0, rects.Count);
        if (o.ByNumber) return idx;
        return o.Arrangement switch
        {
            TileArrangement.Row => idx.OrderBy(i => rects[i].X).ThenBy(i => rects[i].Y).ThenBy(i => i),
            TileArrangement.Column => idx.OrderBy(i => rects[i].Y).ThenBy(i => rects[i].X).ThenBy(i => i),
            // reading order: by the centre's row band (windows whose centres are within a quarter height count as one row)
            _ => ReadingOrder(rects),
        };
    }

    private static IEnumerable<int> ReadingOrder(IReadOnlyList<PixelRect> rects)
    {
        var left = Enumerable.Range(0, rects.Count).OrderBy(i => rects[i].Y + rects[i].Height / 2).ThenBy(i => rects[i].X).ToList();
        var result = new List<int>();
        while (left.Count > 0)
        {
            var top = left[0];
            var band = Math.Max(1, rects[top].Height / 4);
            var cy = rects[top].Y + rects[top].Height / 2;
            var row = left.Where(i => Math.Abs(rects[i].Y + rects[i].Height / 2 - cy) <= band).OrderBy(i => rects[i].X).ToList();
            result.AddRange(row);
            left.RemoveAll(row.Contains);
        }
        return result;
    }

    /// <summary>Splits a length into <paramref name="count"/> parts with gaps; the first parts get the leftover pixels.</summary>
    private static (int Start, int Size)[] Cuts(int start, int length, int count, int gap)
    {
        var total = Math.Max(count, length - gap * (count - 1));
        var size = total / count;
        var extra = total % count;
        var cuts = new (int, int)[count];
        var pos = start;
        for (var k = 0; k < count; k++)
        {
            var s = size + (k < extra ? 1 : 0);
            cuts[k] = (pos, s);
            pos += s + gap;
        }
        return cuts;
    }

    // ---------- typed areas ----------

    /// <summary>A typed area in % of the monitor's usable area (0–100 each) to pixels, kept inside it.</summary>
    public static PixelRect FromPercent(double x, double y, double w, double h, PixelRect area)
    {
        int l = area.X + Geometry.Round(area.Width * Math.Clamp(x, 0, 100) / 100), t = area.Y + Geometry.Round(area.Height * Math.Clamp(y, 0, 100) / 100);
        int r = area.X + Geometry.Round(area.Width * Math.Clamp(x + w, 0, 100) / 100), b = area.Y + Geometry.Round(area.Height * Math.Clamp(y + h, 0, 100) / 100);
        return new PixelRect(l, t, Math.Max(0, r - l), Math.Max(0, b - t));
    }

    public static PixelRect? Intersect(PixelRect a, PixelRect b)
    {
        int l = Math.Max(a.X, b.X), t = Math.Max(a.Y, b.Y), r = Math.Min(a.Right, b.Right), bo = Math.Min(a.Bottom, b.Bottom);
        return r > l && bo > t ? new PixelRect(l, t, r - l, bo - t) : null;
    }
}
