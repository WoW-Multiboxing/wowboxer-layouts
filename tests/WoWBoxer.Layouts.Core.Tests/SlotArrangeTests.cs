namespace WoWBoxer.Layouts.Core.Tests;

public class SlotArrangeTests
{
    private static readonly PixelRect A = new(100, 100, 800, 600), B = new(1000, 200, 400, 300), C = new(1500, 50, 600, 500);

    // ---------- match size ----------

    [Fact]
    public void Same_size_takes_the_references_size_and_keeps_each_top_left()
    {
        var r = SlotArrange.MatchSize([A, B, C], width: true, height: true);
        Assert.Equal(A, r[0]);
        Assert.Equal(new PixelRect(1000, 200, 800, 600), r[1]);
        Assert.Equal(new PixelRect(1500, 50, 800, 600), r[2]);
    }

    [Fact]
    public void Same_width_and_same_height_change_one_dimension()
    {
        Assert.Equal(new PixelRect(1000, 200, 800, 300), SlotArrange.MatchSize([A, B], true, false)[1]);
        Assert.Equal(new PixelRect(1000, 200, 400, 600), SlotArrange.MatchSize([A, B], false, true)[1]);
    }

    [Fact]
    public void One_window_has_nothing_to_match() => Assert.Equal([A], SlotArrange.MatchSize([A], true, true));

    // ---------- align ----------

    [Theory]
    [InlineData(AlignEdge.Left, 100, 200)]
    [InlineData(AlignEdge.Center, 300, 200)]
    [InlineData(AlignEdge.Right, 500, 200)]
    [InlineData(AlignEdge.Top, 1000, 100)]
    [InlineData(AlignEdge.Middle, 1000, 250)]
    [InlineData(AlignEdge.Bottom, 1000, 400)]
    public void Align_to_the_reference(AlignEdge edge, int x, int y)
    {
        var r = SlotArrange.Align([A, B], edge, new PixelRect(0, 0, 2560, 1392));
        Assert.Equal(A, r[0]);
        Assert.Equal(new PixelRect(x, y, 400, 300), r[1]);
    }

    [Theory]
    [InlineData(AlignEdge.Left, 0, 200)]
    [InlineData(AlignEdge.Right, 2160, 200)]
    [InlineData(AlignEdge.Center, 1080, 200)]
    [InlineData(AlignEdge.Bottom, 1000, 1092)]
    [InlineData(AlignEdge.Middle, 1000, 546)]
    public void One_window_aligns_to_its_monitor(AlignEdge edge, int x, int y) =>
        Assert.Equal(new PixelRect(x, y, 400, 300), SlotArrange.Align([B], edge, new PixelRect(0, 0, 2560, 1392))[0]);

    // ---------- distribute ----------

    [Fact]
    public void Distribute_horizontally_gives_equal_gaps_and_keeps_the_outer_windows()
    {
        PixelRect a = new(0, 0, 200, 100), b = new(250, 0, 200, 100), c = new(1000, 0, 200, 100);
        var r = SlotArrange.Distribute([c, a, b], horizontal: true);   // any selection order: by position
        Assert.Equal(c, r[0]);
        Assert.Equal(a, r[1]);
        Assert.Equal(500, r[2].X);   // gaps 300 and 300
        Assert.Equal(r[2].X - (a.X + a.Width), c.X - (r[2].X + r[2].Width));
    }

    [Fact]
    public void Distribute_spreads_leftover_pixels_and_ends_exactly()
    {
        PixelRect a = new(0, 0, 100, 100), b = new(0, 150, 100, 100), c = new(0, 140, 100, 100), d = new(0, 1001, 100, 100);
        var r = SlotArrange.Distribute([a, b, c, d], horizontal: false);
        var ys = r.OrderBy(x => x.Y).ToList();
        Assert.Equal(0, ys[0].Y);
        Assert.Equal(1001, ys[^1].Y);
        var gaps = Enumerable.Range(1, 3).Select(i => ys[i].Y - ys[i - 1].Bottom).ToList();
        Assert.True(gaps.Max() - gaps.Min() <= 1);
    }

    [Fact]
    public void Distribute_needs_three_windows() => Assert.Equal([A, B], SlotArrange.Distribute([A, B], true));

    // ---------- fill row / column ----------

    [Fact]
    public void Fill_row_three_windows_cover_the_monitor_width_edge_to_edge()
    {
        var area = new PixelRect(0, 0, 2560, 1392);
        var r = SlotArrange.Fill([new PixelRect(900, 100, 600, 500), new PixelRect(0, 700, 300, 300), new PixelRect(2000, 0, 400, 400)], area, row: true);
        var byX = r.OrderBy(x => x.X).ToList();
        Assert.Equal(0, byX[0].X);
        Assert.Equal(2560, byX[^1].Right);
        Assert.All(byX.Skip(1).Zip(byX), p => Assert.Equal(p.Second.Right, p.First.X));   // no gaps
        Assert.True(byX.Max(x => x.Width) - byX.Min(x => x.Width) <= 1);
        Assert.All(r, x => Assert.Equal((100, 500), (x.Y, x.Height)));   // the reference's row
        // the order stays: the leftmost window before is the leftmost after
        Assert.Equal(0, r[1].X);
    }

    [Fact]
    public void Fill_column_with_a_gap()
    {
        var area = new PixelRect(1690, 0, 870, 1392);
        var r = SlotArrange.Fill([new PixelRect(1690, 0, 870, 696), new PixelRect(1690, 700, 870, 600)], area, row: false, gap: 8);
        Assert.Equal(new PixelRect(1690, 0, 870, 692), r[0]);
        Assert.Equal(new PixelRect(1690, 700, 870, 692), r[1]);
        Assert.Equal(1392, r[1].Bottom);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fill_uses_the_usable_area_with_or_without_the_taskbar(bool useTaskbarArea)
    {
        var m = TestMonitors.Single(1920, 1080);
        var area = m.Area(useTaskbarArea);
        var r = SlotArrange.Fill([new PixelRect(0, 0, 500, 500), new PixelRect(600, 0, 500, 500)], area, row: false);
        Assert.Equal(area.Bottom, r.Max(x => x.Bottom));
        Assert.Equal(area.Y, r.Min(x => x.Y));
    }

    // ---------- fit 4:3, locked aspect ----------

    [Fact]
    public void Fit_4_3_keeps_width_and_top_left()
    {
        var r = SlotArrange.FitAspect([new PixelRect(0, 0, 1200, 400)], 4.0 / 3, _ => new PixelRect(0, 0, 2560, 1392));
        Assert.Equal(new PixelRect(0, 0, 1200, 900), r[0]);
    }

    [Fact]
    public void Fit_4_3_shrinks_to_the_height_that_fits()
    {
        var r = SlotArrange.FitAspect([new PixelRect(0, 900, 1200, 300)], 4.0 / 3, _ => new PixelRect(0, 0, 2560, 1392));
        Assert.Equal(new PixelRect(0, 900, 656, 492), r[0]);
    }

    [Fact]
    public void Locked_aspect_resize_keeps_the_ratio_and_the_opposite_corner()
    {
        var start = new PixelRect(100, 100, 800, 600);
        var r = SlotArrange.ResizeLocked(start, ResizeEdges.Right | ResizeEdges.Bottom, 200, 0);
        Assert.Equal(new PixelRect(100, 100, 1000, 750), r);
        var l = SlotArrange.ResizeLocked(start, ResizeEdges.Left | ResizeEdges.Top, 80, 0);
        Assert.Equal(new PixelRect(180, 160, 720, 540), l);
        Assert.Equal(900, l.Right);
        var t = SlotArrange.ResizeLocked(start, ResizeEdges.Top, 0, -60);
        Assert.Equal(new PixelRect(100, 40, 880, 660), t);
    }

    // ---------- inspector, rubber band ----------

    [Fact]
    public void Inspector_shows_only_shared_values()
    {
        var (x, y, w, h) = SlotArrange.Common([new PixelRect(0, 0, 800, 600), new PixelRect(0, 700, 800, 300)]);
        Assert.Equal((0, (int?)null, 800, (int?)null), (x, y, w, h));
    }

    [Fact]
    public void Rubber_band_selects_what_it_touches_in_any_direction()
    {
        var band = SlotArrange.Band(1100, 600, 850, 150);
        Assert.Equal(new PixelRect(850, 150, 250, 450), band);
        Assert.Equal([0, 1], SlotArrange.InBand([A, B, C], band));
    }

    // ---------- through the layout: 1–3 monitors, mixed DPI ----------

    public static TheoryData<string> AllSetups() => [.. TestMonitors.Setups.Keys];

    [Theory]
    [MemberData(nameof(AllSetups))]
    public void Fill_row_on_each_monitor_lands_exactly_in_the_layout(string setup)
    {
        var monitors = TestMonitors.Setups[setup];
        var options = new LayoutOptions();
        foreach (var m in monitors)
        {
            var area = m.Area(options.UseTaskbarArea);
            var start = new[] { new PixelRect(area.X + 10, area.Y + 10, 400, 300), new PixelRect(area.X + 500, area.Y + 40, 300, 200), new PixelRect(area.X + 900, area.Y, 350, 250) };
            var filled = SlotArrange.Fill(start, area, row: true);
            var slots = filled.Select((r, i) => SlotEditing.WithRect(new LayoutSlot { N = i + 1 }, r, monitors, options)).ToList();
            var back = slots.Select(s => SlotEditing.Raw(s, Monitors.Find(monitors, s.Monitor)!, options)).ToList();
            Assert.Equal(area.X, back.Min(r => r.X));
            Assert.Equal(area.Right, back.Max(r => r.Right));
            Assert.All(back, r => Assert.True(Math.Abs(r.Width - area.Width / 3) <= 1, $"{setup}: {r}"));
        }
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public void Aligned_windows_stay_on_their_monitors(string setup)
    {
        var monitors = TestMonitors.Setups[setup];
        var options = new LayoutOptions();
        var m = monitors[^1];
        var area = m.Area(false);
        var rects = new[] { new PixelRect(area.X + 100, area.Y + 100, 600, 400), new PixelRect(area.X + 900, area.Y + 300, 300, 200) };
        var aligned = SlotArrange.Align(rects, AlignEdge.Top, area);
        var slots = aligned.Select((r, i) => SlotEditing.WithRect(new LayoutSlot { N = i + 1 }, r, monitors, options)).ToList();
        Assert.All(slots, s => Assert.Equal(m.IsPrimary ? "primary" : m.Index.ToString(), s.Monitor));
        var back = slots.Select(s => SlotEditing.Raw(s, Monitors.Find(monitors, s.Monitor)!, options)).ToList();
        Assert.Equal(back[0].Y, back[1].Y);
    }
}

public class SlotSelectionTests
{
    [Fact]
    public void Click_selects_one_and_ctrl_click_adds_and_removes()
    {
        var s = new SlotSelection();
        s.Click(2);
        Assert.Equal([2], s.Items);
        s.Toggle(0);
        s.Toggle(1);
        Assert.Equal([2, 0, 1], s.Items);
        Assert.Equal(2, s.Reference);   // the first selected stays the reference
        s.Toggle(0);
        Assert.Equal([2, 1], s.Items);
        s.Toggle(2);
        Assert.Equal(1, s.Reference);
    }

    [Fact]
    public void Clicking_a_selected_window_to_drag_keeps_the_group()
    {
        var s = new SlotSelection();
        s.Set([0, 1, 2]);
        s.Click(1, keepGroupIfSelected: true);
        Assert.Equal([0, 1, 2], s.Items);
        s.Click(1);
        Assert.Equal([1], s.Items);
    }

    [Fact]
    public void Band_replaces_or_adds()
    {
        var s = new SlotSelection();
        s.Click(3);
        s.Band([0, 1], add: true);
        Assert.Equal([3, 0, 1], s.Items);
        s.Band([2], add: false);
        Assert.Equal([2], s.Items);
    }

    [Fact]
    public void Select_all_keeps_the_reference_first_and_clear_empties()
    {
        var s = new SlotSelection();
        s.Click(2);
        s.All(4);
        Assert.Equal([2, 0, 1, 3], s.Items);
        s.Trim(2);
        Assert.Equal([0, 1], s.Items);
        s.Clear();
        Assert.Equal(-1, s.Reference);
    }
}

public class CustomMigrationTests
{
    private static string OldJson()
    {
        var p = Presets.ForScreens(PresetKind.Custom, 3, TestMonitors.Reference) with { Name = "Old free" };
        return ProfileJson.Serialize(p).Replace("\"preset\": \"custom\"", "\"preset\": \"free\"");
    }

    [Fact]
    public void Old_free_for_all_files_read_as_custom()
    {
        var json = OldJson();
        Assert.Contains("\"free\"", json);
        Assert.True(ProfileJson.IsLegacy(json));
        Assert.Equal(PresetKind.Custom, ProfileJson.Deserialize(json).Preset);
        Assert.Contains("\"preset\": \"custom\"", ProfileJson.Serialize(ProfileJson.Deserialize(json)));
    }

    [Fact]
    public void Old_share_codes_import_as_custom()
    {
        var code = ProfileJson.SharePrefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(OldJson()));
        Assert.Equal(PresetKind.Custom, ProfileJson.FromShareCode(code).Preset);
    }

    [Fact]
    public void The_store_migrates_old_files_on_load()
    {
        var dir = Directory.CreateTempSubdirectory("wbx-migrate-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "Old free.json"), OldJson());
            var store = new ProfileStore(dir.FullName);
            var loaded = store.LoadAll();
            Assert.Equal(PresetKind.Custom, Assert.Single(loaded).Profile.Preset);
            Assert.DoesNotContain("\"free\"", File.ReadAllText(loaded[0].Path));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Current_files_are_not_legacy() =>
        Assert.False(ProfileJson.IsLegacy(ProfileJson.Serialize(Presets.ForScreens(PresetKind.Side, 3, TestMonitors.Reference))));
}
