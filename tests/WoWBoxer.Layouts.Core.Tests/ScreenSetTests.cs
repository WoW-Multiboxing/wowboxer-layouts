namespace WoWBoxer.Layouts.Core.Tests;

public class ScreenSetTests
{
    private static readonly IReadOnlyList<MonitorInfo> Triple = TestMonitors.Setups["triple-mixed"];

    [Fact]
    public void Order_is_primary_first_then_left_to_right()
    {
        Assert.Equal([2, 3, 1], ScreenSet.Ordered(Triple).Select(m => m.Index));
    }

    [Fact]
    public void Choices_list_every_set_with_the_default_first()
    {
        var two = ScreenSet.Choices(Triple, 2);

        Assert.Equal(3, two.Count);
        Assert.Equal([2, 3], two[0].Select(m => m.Index));   // primary + the left one
        Assert.Single(ScreenSet.Choices(Triple, 3));
        Assert.Equal(3, ScreenSet.Choices(Triple, 1).Count);
        Assert.Empty(ScreenSet.Choices(Triple, 4));
    }

    [Fact]
    public void Names_say_where_each_monitor_is()
    {
        Assert.Equal("Primary + left + right · 2560×1440 + 1920×1080 + 3840×2160", ScreenSet.Describe(ScreenSet.Ordered(Triple), Triple));
        Assert.Equal("left", ScreenSet.Position(Triple[2], Triple));
    }

    [Fact]
    public void Screens_are_found_again_by_device_id_when_windows_renumbers_them()
    {
        var profile = Presets.ForScreens(PresetKind.Side, 10, [Triple[1], Triple[0]]);   // party 1 on the primary, party 2 on the 4K
        // after a restart Windows numbers them differently and the 4K became primary
        IReadOnlyList<MonitorInfo> now =
        [
            Triple[0] with { Index = 3, IsPrimary = true },
            Triple[1] with { Index = 1, IsPrimary = false },
            Triple[2] with { Index = 2 },
        ];

        var (chosen, missing) = ScreenSet.Resolve(profile, now);
        var remapped = ScreenSet.Remap(profile, now);

        Assert.False(missing);
        Assert.Equal(["MON-1440", "MON-4K"], chosen.Select(m => m.DeviceId));
        var placed = LayoutEngine.Resolve(remapped, now);
        Assert.All(placed.Take(5), p => Assert.Equal("MON-1440", p.Monitor.DeviceId));
        Assert.All(placed.Skip(5), p => Assert.Equal("MON-4K", p.Monitor.DeviceId));
        Assert.Equal(LayoutEngine.Resolve(profile, Triple).Select(p => p.Rect), placed.Select(p => p.Rect));
        Assert.Same(remapped, ScreenSet.Remap(remapped, now));   // nothing more to change
    }

    [Fact]
    public void Without_ids_screens_are_found_by_place_and_size_then_by_size()
    {
        var refs = ScreenSet.Ordered(Triple).Select(m => ScreenSet.ToRef(m) with { Id = null }).ToList();
        // the left monitor moved up a little: no exact place any more, but its size is unique
        IReadOnlyList<MonitorInfo> now = [Triple[0], Triple[1], Triple[2] with { Bounds = Triple[2].Bounds with { Y = 0 } }];

        Assert.Equal([2, 3, 1], ScreenSet.Match(refs, now).Select(m => m!.Index));
    }

    [Fact]
    public void An_unplugged_screen_is_reported_missing()
    {
        var profile = Presets.ForScreens(PresetKind.Grid, 6, ScreenSet.Ordered(Triple));

        var (chosen, missing) = ScreenSet.Resolve(profile, [Triple[1]]);

        Assert.True(missing);
        Assert.Equal([2], chosen.Select(m => m.Index));
        Assert.Same(profile, ScreenSet.Remap(profile, [Triple[1]]));
    }

    [Fact]
    public void Older_layouts_use_the_monitors_their_slots_are_on()
    {
        var monitors = TestMonitors.Setups["dual-mixed"];
        var profile = Presets.Create(PresetKind.Dual, 4, monitors) with { Screens = null };

        Assert.Equal([1, 2], ScreenSet.Resolve(profile, monitors).Chosen.Select(m => m.Index));
        Assert.Equal([1], ScreenSet.Used(Presets.Create(PresetKind.Grid, 4, monitors), monitors).Select(m => m.Index));
    }

    [Fact]
    public void Screens_round_trip_in_json_and_share_codes_leave_the_ids_out()
    {
        var profile = Presets.ForScreens(PresetKind.Side, 7, ScreenSet.Ordered(Triple), split: PartySplit.Stacked);

        Assert.Equal(profile, ProfileJson.Deserialize(ProfileJson.Serialize(profile)));
        var shared = ProfileJson.FromShareCode(ProfileJson.ToShareCode(profile));
        Assert.All(shared.Screens!, s => Assert.Null(s.Id));
        Assert.Equal(profile.Slots, shared.Slots);
        Assert.Equal([2, 3, 1], ScreenSet.Match(shared.Screens!, Triple).Select(m => m!.Index));
    }
}
