namespace WoWBoxer.Layouts.Core.Tests;

public class PresetTests
{
    public static TheoryData<PresetKind, int, string> AllCombinations()
    {
        var data = new TheoryData<PresetKind, int, string>();
        foreach (var kind in Enum.GetValues<PresetKind>())
            foreach (var setup in kind == PresetKind.Dual ? TestMonitors.DualSetups : TestMonitors.Setups.Keys)
                for (var n = Presets.MinClients; n <= Presets.MaxClients; n++)
                    data.Add(kind, n, setup);
        return data;
    }

    private static IReadOnlyList<PlacedSlot> Place(PresetKind kind, int n, string setup)
    {
        var monitors = TestMonitors.Setups[setup];
        return LayoutEngine.Resolve(Presets.Create(kind, n, monitors), monitors);
    }

    [Fact]
    public void Side_3_matches_the_PowerShell_launcher_on_the_reference_setup()
    {
        var placed = LayoutEngine.Resolve(Presets.Create(PresetKind.Side, 3, TestMonitors.Reference), TestMonitors.Reference);

        Assert.Equal(new PixelRect(0, 0, 1690, 1392), placed[0].Rect);
        Assert.Equal(new PixelRect(1690, 0, 870, 696), placed[1].Rect);
        Assert.Equal(new PixelRect(1690, 696, 870, 696), placed[2].Rect);
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Every_slot_is_inside_its_monitor_work_area(PresetKind kind, int n, string setup)
    {
        var placed = Place(kind, n, setup);

        Assert.Equal(n, placed.Count);
        Assert.All(placed, p =>
        {
            Assert.False(p.MonitorFallback);
            Assert.True(p.Rect.Width > 0 && p.Rect.Height > 0, $"slot {p.Slot.N} has no size");
            Assert.True(p.Monitor.WorkArea.Contains(p.Rect), $"slot {p.Slot.N} {p.Rect} is outside {p.Monitor.WorkArea}");
        });
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Followers_are_at_least_5_to_4(PresetKind kind, int n, string setup)
    {
        var placed = Place(kind, n, setup);

        if (kind == PresetKind.Free) return;   // free windows keep whatever size the user drags
        Assert.All(placed.Where(p => p.Slot.Role == SlotRole.Follower),
            p => Assert.True(p.Rect.Aspect >= 1.25 - 0.005, $"slot {p.Slot.N} {p.Rect} aspect {p.Rect.Aspect:F3}"));
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Windows_do_not_overlap_except_overlays_on_the_main(PresetKind kind, int n, string setup)
    {
        var placed = Place(kind, n, setup);
        if (kind == PresetKind.Free) return;   // a cascade overlaps on purpose
        var checkedSlots = kind == PresetKind.Overlay ? placed.Skip(1).ToList() : placed.ToList();

        for (var i = 0; i < checkedSlots.Count; i++)
            for (var j = i + 1; j < checkedSlots.Count; j++)
                Assert.False(checkedSlots[i].Rect.Overlaps(checkedSlots[j].Rect),
                    $"slot {checkedSlots[i].Slot.N} {checkedSlots[i].Rect} overlaps slot {checkedSlots[j].Slot.N} {checkedSlots[j].Rect}");
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Presets_are_valid_profiles(PresetKind kind, int n, string setup)
    {
        var profile = Presets.Create(kind, n, TestMonitors.Setups[setup]);

        Assert.Empty(ProfileValidator.Validate(profile));
        Assert.Single(profile.Slots, s => s.Role == SlotRole.Main);
        Assert.Equal(SlotRole.Main, profile.Slots[0].Role);
    }

    [Theory]
    [InlineData("1080p")]
    [InlineData("21:9")]
    public void Side_main_shares_its_right_edge_with_the_followers(string setup)
    {
        for (var n = 2; n <= 8; n++)
        {
            var placed = Place(PresetKind.Side, n, setup);
            var firstColumn = placed.Skip(1).Min(p => p.Rect.X);
            Assert.Equal(placed[0].Rect.Right, firstColumn);
            Assert.Equal(placed[0].Monitor.WorkArea.Height, placed[0].Rect.Height);
        }
    }

    [Fact]
    public void Overlay_followers_float_inside_the_main_window_and_are_topmost()
    {
        var placed = Place(PresetKind.Overlay, 5, "1080p");

        Assert.Equal(ZOrder.Bottom, placed[0].Slot.Z);
        Assert.All(placed.Skip(1), p =>
        {
            Assert.Equal(ZOrder.Top, p.Slot.Z);
            Assert.True(placed[0].Rect.Contains(p.Rect));
            Assert.True(p.Rect.Y > placed[0].Rect.Height / 2, "followers sit in the lower half");
        });
    }

    [Fact]
    public void Dual_puts_main_on_one_monitor_and_followers_on_the_other()
    {
        var placed = Place(PresetKind.Dual, 4, "dual-mixed");

        Assert.Equal(1, placed[0].Monitor.Index);
        Assert.Equal(placed[0].Monitor.WorkArea, placed[0].Rect);
        Assert.All(placed.Skip(1), p => Assert.Equal(2, p.Monitor.Index));
    }

    [Fact]
    public void Presets_remember_their_kind_for_reset()
    {
        Assert.Equal(PresetKind.Overlay, Presets.Create(PresetKind.Overlay, 4, TestMonitors.Reference).Preset);
    }

    [Fact]
    public void Dual_with_one_monitor_is_refused()
    {
        var e = Assert.Throws<ArgumentException>(() => Presets.Create(PresetKind.Dual, 3, TestMonitors.Reference));
        Assert.Contains("two monitors", e.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void Client_counts_outside_2_to_8_are_refused(int n) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Presets.Create(PresetKind.Grid, n, TestMonitors.Reference));

    [Fact]
    public void Grid_4_on_16_9_is_two_by_two()
    {
        var cells = Presets.GridCells(4, new PixelRect(0, 0, 1920, 1080), 1.25);

        Assert.Equal([new(0, 0, 960, 540), new(960, 0, 960, 540), new(0, 540, 960, 540), new(960, 540, 960, 540)], cells);
    }

    [Fact]
    public void Grid_centres_a_short_last_row()
    {
        var cells = Presets.GridCells(3, new PixelRect(0, 0, 1920, 1080), 1.25);

        Assert.Equal(new PixelRect(480, 540, 960, 540), cells[2]);
    }

    [Fact]
    public void Main_can_go_on_the_second_monitor()
    {
        var monitors = TestMonitors.Setups["dual-1080p"];
        var placed = LayoutEngine.Resolve(Presets.Create(PresetKind.Side, 3, monitors, monitor: "2"), monitors);

        Assert.All(placed, p => Assert.Equal(2, p.Monitor.Index));
        Assert.Equal(1920, placed[0].Rect.X);
    }
}
