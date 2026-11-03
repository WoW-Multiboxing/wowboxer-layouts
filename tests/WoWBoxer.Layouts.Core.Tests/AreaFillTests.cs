namespace WoWBoxer.Layouts.Core.Tests;

public class FreeSpaceTests
{
    private static readonly PixelRect Screen = new(0, 0, 2560, 1392);

    [Fact]
    public void An_empty_monitor_is_all_free() => Assert.Equal([Screen], AreaFill.FreeRects(Screen, []));

    [Fact]
    public void Window_on_the_left_half_leaves_the_right_half()
    {
        var free = AreaFill.FreeRects(Screen, [new PixelRect(0, 0, 1280, 1392)]);
        Assert.Equal([new PixelRect(1280, 0, 1280, 1392)], free);
    }

    [Fact]
    public void A_window_in_the_middle_leaves_four_maximal_strips()
    {
        var free = AreaFill.FreeRects(Screen, [new PixelRect(1000, 500, 500, 400)]);
        Assert.Equal(4, free.Count);
        Assert.Contains(new PixelRect(0, 0, 1000, 1392), free);
        Assert.Contains(new PixelRect(1500, 0, 1060, 1392), free);
        Assert.Contains(new PixelRect(0, 0, 2560, 500), free);
        Assert.Contains(new PixelRect(0, 900, 2560, 492), free);
        Assert.Equal(new PixelRect(1500, 0, 1060, 1392), free[0]);   // largest first
    }

    [Fact]
    public void Overlapping_windows_and_windows_sticking_out_are_handled()
    {
        var free = AreaFill.FreeRects(Screen, [new PixelRect(-300, 0, 1000, 800), new PixelRect(500, 300, 800, 2000)]);
        Assert.All(free, f => Assert.True(f.X >= 0 && f.Right <= 2560 && f.Y >= 0 && f.Bottom <= 1392));
        Assert.Contains(new PixelRect(1300, 0, 1260, 1392), free);
        Assert.Contains(new PixelRect(0, 800, 500, 592), free);
        Assert.DoesNotContain(free, f => AreaFill.Intersect(f, new PixelRect(500, 300, 800, 2000)) is not null);
    }

    [Fact]
    public void No_free_space_when_covered()
    {
        Assert.Empty(AreaFill.FreeRects(Screen, [new PixelRect(0, 0, 1280, 1392), new PixelRect(1280, 0, 1280, 1392)]));
        Assert.Null(AreaFill.EmptySpaceFor([new PixelRect(0, 0, 100, 100)], [(Screen, (IReadOnlyList<PixelRect>)[Screen])]));
    }

    [Fact]
    public void Slivers_below_the_minimum_size_do_not_count() =>
        Assert.Empty(AreaFill.FreeRects(Screen, [new PixelRect(0, 0, 2500, 1392)], minSize: SlotEditing.MinSlotSize));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Free_space_follows_the_usable_area_with_or_without_the_taskbar(bool useTaskbarArea)
    {
        var m = TestMonitors.Single(1920, 1080);
        var area = m.Area(useTaskbarArea);
        var free = AreaFill.FreeRects(area, [new PixelRect(0, 0, 960, 1080)]);
        Assert.Equal(area.Bottom, Assert.Single(free).Bottom);
    }

    [Fact]
    public void Empty_space_is_the_one_the_selection_is_mostly_in()
    {
        // a window in the middle: the selection sits mostly at the bottom, so the bottom strip wins (not the largest)
        var others = (IReadOnlyList<PixelRect>)[new PixelRect(1000, 500, 500, 400)];
        var picked = AreaFill.EmptySpaceFor([new PixelRect(100, 1000, 300, 300), new PixelRect(1700, 1000, 300, 300)], [(Screen, others)]);
        Assert.Equal(new PixelRect(0, 900, 2560, 492), picked);
    }

    [Fact]
    public void Empty_space_without_overlap_is_the_largest()
    {
        var others = (IReadOnlyList<PixelRect>)[new PixelRect(0, 0, 1280, 1392)];
        Assert.Equal(new PixelRect(1280, 0, 1280, 1392), AreaFill.EmptySpaceFor([new PixelRect(10, 10, 200, 200)], [(Screen, others)]));
    }

    public static TheoryData<string> AllSetups() => [.. TestMonitors.Setups.Keys];

    [Theory]
    [MemberData(nameof(AllSetups))]
    public void Every_monitor_counts_and_the_selections_monitor_wins(string setup)
    {
        var monitors = TestMonitors.Setups[setup];
        var areas = monitors.Select(m => (m.WorkArea, (IReadOnlyList<PixelRect>)[])).ToList();
        var last = monitors[^1].WorkArea;
        var picked = AreaFill.EmptySpaceFor([new PixelRect(last.X + 50, last.Y + 50, 400, 300)], areas);
        Assert.Equal(last, picked);
    }
}

public class TileTests
{
    private static readonly PixelRect Half = new(1280, 0, 1280, 1392);

    [Fact]
    public void Four_windows_fill_the_right_half_as_a_2x2_grid()
    {
        PixelRect[] sel = [new(100, 100, 300, 200), new(500, 100, 300, 200), new(100, 400, 300, 200), new(500, 400, 300, 200)];
        var r = AreaFill.Tile(sel, Half, new TileOptions());
        Assert.Equal(new PixelRect(1280, 0, 640, 696), r[0]);
        Assert.Equal(new PixelRect(1920, 0, 640, 696), r[1]);
        Assert.Equal(new PixelRect(1280, 696, 640, 696), r[2]);
        Assert.Equal(new PixelRect(1920, 696, 640, 696), r[3]);
    }

    [Theory]
    [InlineData(TileArrangement.Row, 1, 5)]
    [InlineData(TileArrangement.Column, 5, 1)]
    public void Row_and_column(TileArrangement a, int rows, int cols)
    {
        var sel = Enumerable.Range(0, 5).Select(i => new PixelRect(i * 100, 0, 90, 90)).ToList();
        var r = AreaFill.Tile(sel, Half, new TileOptions(a));
        Assert.Equal((rows, cols), AreaFill.GridFor(5, Half, new TileOptions(a)));
        Assert.Equal(Half.X, r.Min(x => x.X));
        Assert.Equal(Half.Right, r.Max(x => x.Right));
        Assert.Equal(Half.Bottom, r.Max(x => x.Bottom));
        Assert.Equal(Half.Y, r.Min(x => x.Y));
    }

    [Fact]
    public void A_set_grid_size_and_a_gap()
    {
        var sel = Enumerable.Range(0, 3).Select(i => new PixelRect(i * 100, 0, 90, 90)).ToList();
        var r = AreaFill.Tile(sel, new PixelRect(0, 0, 1000, 1000), new TileOptions(TileArrangement.Grid, Rows: 1, Columns: 3, Gap: 10));
        Assert.Equal([new PixelRect(0, 0, 327, 1000), new PixelRect(337, 0, 327, 1000), new PixelRect(674, 0, 326, 1000)], r);
    }

    [Fact]
    public void Automatic_grid_prefers_4_3_cells_and_no_empty_rows()
    {
        Assert.Equal((2, 2), AreaFill.GridFor(4, new PixelRect(0, 0, 1280, 1392), new TileOptions()));
        Assert.Equal((2, 3), AreaFill.GridFor(5, new PixelRect(0, 0, 2560, 1392), new TileOptions()));
        Assert.Equal((1, 2), AreaFill.GridFor(2, new PixelRect(0, 0, 2560, 1000), new TileOptions()));
    }

    [Fact]
    public void Order_by_position_or_by_client_number()
    {
        PixelRect[] sel = [new(900, 0, 100, 100), new(0, 0, 100, 100), new(500, 0, 100, 100)];
        var byPos = AreaFill.Tile(sel, new PixelRect(0, 0, 900, 300), new TileOptions(TileArrangement.Row));
        Assert.Equal([600, 0, 300], byPos.Select(x => x.X));   // left-to-right as they are
        var byNum = AreaFill.Tile(sel, new PixelRect(0, 0, 900, 300), new TileOptions(TileArrangement.Row, ByNumber: true));
        Assert.Equal([0, 300, 600], byNum.Select(x => x.X));   // client 1, 2, 3
    }

    [Fact]
    public void Grid_reading_order_follows_rows()
    {
        // two rows that are not perfectly level
        PixelRect[] sel = [new(800, 20, 300, 200), new(0, 0, 300, 200), new(820, 600, 300, 200), new(0, 610, 300, 200)];
        var r = AreaFill.Tile(sel, new PixelRect(0, 0, 1200, 900), new TileOptions(Rows: 2, Columns: 2));
        Assert.Equal(new PixelRect(0, 0, 600, 450), r[1]);
        Assert.Equal(new PixelRect(600, 0, 600, 450), r[0]);
        Assert.Equal(new PixelRect(0, 450, 600, 450), r[3]);
        Assert.Equal(new PixelRect(600, 450, 600, 450), r[2]);
    }

    [Fact]
    public void Span_is_from_the_first_edge_to_the_last_edge()
    {
        PixelRect[] sel = [new(300, 200, 400, 300), new(900, 100, 300, 500), new(1500, 250, 200, 200)];
        var span = AreaFill.Span(sel);
        Assert.Equal(new PixelRect(300, 100, 1400, 500), span);
        var r = AreaFill.Tile(sel, span, new TileOptions(TileArrangement.Row));
        Assert.Equal(300, r[0].X);
        Assert.Equal(1700, r[2].Right);
        Assert.True(r.Max(x => x.Width) - r.Min(x => x.Width) <= 1);
    }

    [Fact]
    public void Typed_percent_areas()
    {
        var area = new PixelRect(1920, 0, 2560, 1392);
        Assert.Equal(new PixelRect(3200, 0, 1280, 1392), AreaFill.FromPercent(50, 0, 50, 100, area));
        Assert.Equal(new PixelRect(1920, 696, 2560, 696), AreaFill.FromPercent(0, 50, 120, 80, area));   // kept inside
    }

    [Theory]
    [InlineData("1080p")]
    [InlineData("dual-mixed")]
    [InlineData("triple-mixed")]
    public void A_tiled_area_lands_exactly_through_the_layout(string setup)
    {
        var monitors = TestMonitors.Setups[setup];
        var m = monitors[^1];
        var area = AreaFill.FromPercent(50, 0, 50, 100, m.WorkArea);
        var sel = Enumerable.Range(0, 4).Select(i => new PixelRect(m.WorkArea.X + i * 50, m.WorkArea.Y, 300, 300)).ToList();
        var tiles = AreaFill.Tile(sel, area, new TileOptions());
        var options = new LayoutOptions();
        var back = tiles.Select((t, i) => SlotEditing.Raw(SlotEditing.WithRect(new LayoutSlot { N = i + 1 }, t, monitors, options), m, options)).ToList();
        Assert.All(back.Zip(tiles), p => Assert.True(Math.Abs(p.First.X - p.Second.X) <= 1 && Math.Abs(p.First.Width - p.Second.Width) <= 1, $"{setup}: {p.First} vs {p.Second}"));
    }
}
