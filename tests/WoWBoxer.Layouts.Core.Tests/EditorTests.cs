namespace WoWBoxer.Layouts.Core.Tests;

public class SlotEditingTests
{
    private static readonly IReadOnlyList<MonitorInfo> Dual = TestMonitors.Setups["dual-1080p"];

    [Fact]
    public void Move_keeps_the_size()
    {
        var r = SlotEditing.Drag(new PixelRect(100, 100, 400, 300), ResizeEdges.All, 50, -20);

        Assert.Equal(new PixelRect(150, 80, 400, 300), r);
    }

    [Fact]
    public void Resize_from_the_top_left_corner_keeps_the_bottom_right()
    {
        var r = SlotEditing.Drag(new PixelRect(100, 100, 400, 300), ResizeEdges.Left | ResizeEdges.Top, 30, 40);

        Assert.Equal(new PixelRect(130, 140, 370, 260), r);
    }

    [Fact]
    public void Resize_never_goes_below_the_minimum_size()
    {
        var r = SlotEditing.Drag(new PixelRect(100, 100, 400, 300), ResizeEdges.Right, -1000, 0);

        Assert.Equal(SlotEditing.MinSlotSize, r.Width);
        Assert.Equal(100, r.X);
    }

    [Fact]
    public void Move_snaps_to_the_nearest_edge_without_resizing()
    {
        // right edge at 1915 is 5 px from the monitor edge at 1920
        var r = SlotEditing.Snap(new PixelRect(1515, 3, 400, 300), ResizeEdges.All, [0, 1920], [0, 1040], 8);

        Assert.Equal(new PixelRect(1520, 0, 400, 300), r);
    }

    [Fact]
    public void Nothing_snaps_outside_the_tolerance()
    {
        var r = new PixelRect(500, 500, 400, 300);

        Assert.Equal(r, SlotEditing.Snap(r, ResizeEdges.All, [0, 1920], [0, 1040], 8));
    }

    [Fact]
    public void Resize_snaps_only_the_dragged_edge()
    {
        var r = SlotEditing.Snap(new PixelRect(100, 100, 853, 300), ResizeEdges.Right, [960], [], 8);

        Assert.Equal(new PixelRect(100, 100, 860, 300), r);
    }

    [Fact]
    public void Snap_lines_include_other_slots_and_work_areas()
    {
        var (x, y) = SlotEditing.SnapLines([new PixelRect(10, 20, 30, 40)], Dual, useTaskbarArea: false);

        Assert.Contains(10, x);
        Assert.Contains(40, x);
        Assert.Contains(1920, x);
        Assert.Contains(3840, x);
        Assert.Contains(60, y);
        Assert.Contains(1032, y);
    }

    [Fact]
    public void Dropping_a_slot_on_another_monitor_moves_it_there()
    {
        var slot = new LayoutSlot { N = 2, X = 10, Y = 10, W = 25, H = 25 };

        var moved = SlotEditing.WithRect(slot, new PixelRect(2000, 100, 480, 270), Dual, new LayoutOptions());

        Assert.Equal("2", moved.Monitor);
        Assert.Equal(new PixelRect(2000, 100, 480, 270), SlotEditing.Raw(moved, Dual[1], new LayoutOptions()));
    }

    [Fact]
    public void A_slot_hanging_over_the_edge_is_pulled_inside()
    {
        var slot = SlotEditing.WithRect(new LayoutSlot { N = 1 }, new PixelRect(1800, 900, 400, 300), [Dual[0]], new LayoutOptions());

        Assert.Equal(new PixelRect(1520, 732, 400, 300), SlotEditing.Raw(slot, Dual[0], new LayoutOptions()));
    }
}

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "wbx-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ProfileStore _store;

    public ProfileStoreTests() => _store = new ProfileStore(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static LayoutProfile Named(string name) => Presets.Create(PresetKind.Grid, 2, TestMonitors.Reference) with { Name = name };

    [Fact]
    public void Save_and_load_all_sorted_by_name()
    {
        _store.Save(Named("Trio"));
        _store.Save(Named("duo"));

        Assert.Equal(["duo", "Trio"], _store.LoadAll().Select(p => p.Profile.Name));
    }

    [Fact]
    public void Renaming_moves_the_file()
    {
        var old = _store.Save(Named("Old"));

        var renamed = _store.Save(Named("New"), old);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(renamed));
        Assert.Single(_store.LoadAll());
    }

    [Fact]
    public void Unique_names_never_overwrite()
    {
        _store.Save(Named("Trio"));

        Assert.Equal("Trio (2)", _store.UniqueName("Trio"));
        Assert.Equal("Trio", _store.UniqueName("Trio", _store.PathFor("Trio")));
    }

    [Fact]
    public void Broken_files_are_reported_not_thrown()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "broken.json"), "{ nope");
        _store.Save(Named("Good"));
        var errors = new List<string>();

        var all = _store.LoadAll(errors);

        Assert.Single(all);
        Assert.Single(errors);
    }

    [Fact]
    public void File_names_are_safe() => Assert.Equal("a_b_c.json", ProfileStore.FileNameFor("a/b:c"));
}
