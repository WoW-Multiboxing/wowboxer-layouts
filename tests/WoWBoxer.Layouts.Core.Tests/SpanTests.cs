namespace WoWBoxer.Layouts.Core.Tests;

/// <summary>"Main window over both monitors" and the Team Screen's topology data (wowboxer-teamscreen docs/topology.md).</summary>
public class SpanTests
{
    /// <summary>The owner's PC: G27qe-20 2560×1440 (taskbar 48 px) and Acer XB240H 1920×1080 to its right, top edges level.</summary>
    private static readonly IReadOnlyList<MonitorInfo> Owner =
    [
        new(1, @"\\.\DISPLAY1", true, new PixelRect(0, 0, 2560, 1440), new PixelRect(0, 0, 2560, 1392)),
        new(2, @"\\.\DISPLAY2", false, new PixelRect(2560, 0, 1920, 1080), new PixelRect(2560, 0, 1920, 1032)),
    ];

    private static LayoutProfile Spanning(PresetKind kind = PresetKind.Side, int clients = 3, LayoutOptions? options = null) =>
        Presets.Create(kind, clients, Owner, options: options, mainSpans: true);

    [Fact]
    public void Main_covers_both_monitors_followers_stay_on_monitor_one_on_top()
    {
        var profile = Spanning();
        var placed = LayoutEngine.Resolve(profile, Owner);
        var oneMonitor = LayoutEngine.Resolve(Presets.Create(PresetKind.Side, 3, Owner), Owner);

        Assert.True(profile.MainSpans);
        Assert.Equal(new PixelRect(0, 0, 4480, 1392), placed[0].Rect);   // bounding rectangle of both work areas
        Assert.Equal(ZOrder.Bottom, placed[0].Slot.Z);
        Assert.Equal(oneMonitor[1].Rect, placed[1].Rect);                // followers as today
        Assert.Equal(oneMonitor[2].Rect, placed[2].Rect);
        Assert.All(placed.Skip(1), p => Assert.Equal(ZOrder.Top, p.Slot.Z));
        Assert.Equal(2, profile.Screens!.Count);
        Assert.Empty(ProfileValidator.Validate(profile));   // the slot itself stays inside monitor 1
    }

    [Fact]
    public void Survives_a_json_round_trip_and_a_swap()
    {
        var profile = Spanning();
        var back = ProfileJson.Deserialize(ProfileJson.Serialize(profile));
        Assert.Equal(profile, back);
        Assert.True(back.MainSpans);

        // swap slots 1 and 2: the client now in the main slot gets the span
        var swapped = WindowSwap.Apply(profile, [1, 0, 2]);
        var placed = LayoutEngine.Resolve(swapped, Owner);
        Assert.Equal(new PixelRect(0, 0, 4480, 1392), placed[1].Rect);
        Assert.False(placed[0].Slot.Span);
    }

    [Fact]
    public void Only_for_one_party_side_or_overlay_and_two_monitors()
    {
        Assert.True(Presets.CanSpan(PresetKind.Overlay, 5, 2));
        Assert.False(Presets.CanSpan(PresetKind.Grid, 3, 2));
        Assert.False(Presets.CanSpan(PresetKind.Side, 6, 2));
        Assert.False(Presets.CanSpan(PresetKind.Side, 3, 1));
        Assert.False(Presets.ForScreens(PresetKind.Grid, 3, Owner, mainSpans: true).MainSpans);
        Assert.Throws<ArgumentException>(() => Presets.Create(PresetKind.Side, 3, [Owner[0]], mainSpans: true));
    }

    [Fact]
    public void Without_monitor_two_the_main_fills_monitor_one()
    {
        var placed = LayoutEngine.Resolve(Spanning(), [Owner[0]]);
        Assert.Equal(Owner[0].WorkArea, placed[0].Rect);
        Assert.Null(Topology.For(Spanning(), placed, [Owner[0]]));
    }

    [Fact]
    public void Topology_framed_window_on_the_owners_pc()
    {
        var profile = Spanning();
        var t = Topology.For(profile, LayoutEngine.Resolve(profile, Owner), Owner)!;
        // window 4480×1392 minus the frame (2 × 1 px sides, 31 px caption + 1 px bottom); client area starts at 1,31
        Assert.Equal(4478, t.WindowWidth);
        Assert.Equal(1360, t.WindowHeight);
        Assert.Equal(new PixelRect(2559, -31, 1920, 1032), t.Team);
        Assert.Equal("WoWBoxerTopology = { schema = 1, window = { w = 4478, h = 1360 }, team = { x = 2559, y = -31, w = 1920, h = 1032 } }\r\n", t.LuaText());
    }

    [Fact]
    public void Topology_borderless_and_with_the_taskbar_area()
    {
        var options = new LayoutOptions { Borderless = true, UseTaskbarArea = true };
        var profile = Spanning(PresetKind.Overlay, 3, options);
        var t = Topology.For(profile, LayoutEngine.Resolve(profile, Owner), Owner)!;
        Assert.Equal((4480, 1440), (t.WindowWidth, t.WindowHeight));
        Assert.Equal(new PixelRect(2560, 0, 1920, 1080), t.Team);   // the docs' example exactly
    }

    [Fact]
    public void Topology_with_monitor_two_on_the_left()
    {
        IReadOnlyList<MonitorInfo> left =
        [
            new(1, @"\\.\DISPLAY1", true, new PixelRect(0, 0, 2560, 1440), new PixelRect(0, 0, 2560, 1440)),
            new(2, @"\\.\DISPLAY2", false, new PixelRect(-1920, 360, 1920, 1080), new PixelRect(-1920, 360, 1920, 1080)),
        ];
        var profile = Presets.Create(PresetKind.Side, 3, left, options: new LayoutOptions { Borderless = true }, mainSpans: true);
        var placed = LayoutEngine.Resolve(profile, left);
        Assert.Equal(new PixelRect(-1920, 0, 4480, 1440), placed[0].Rect);
        Assert.Equal(new PixelRect(0, 360, 1920, 1080), Topology.For(profile, placed, left)!.Team);
    }

    [Fact]
    public void No_topology_without_a_spanning_main()
    {
        var profile = Presets.Create(PresetKind.Side, 3, Owner);
        Assert.Null(Topology.For(profile, LayoutEngine.Resolve(profile, Owner), Owner));
    }

    [Fact]
    public void Toc_is_the_documented_one()
    {
        var toc = Topology.TocText();
        Assert.StartsWith("## Interface: 16001, 11509, 20505\r\n", toc);
        Assert.Contains(Topology.GeneratedMark, toc);
        Assert.EndsWith("WoWBoxerTopology.lua\r\n", toc);
    }

    [Fact]
    public void Editing_the_main_by_hand_ends_the_span()
    {
        var profile = Spanning();
        var edited = SlotEditing.WithRect(profile.Slots[0], new PixelRect(100, 100, 1200, 900), Owner, profile.Options);
        Assert.False(edited.Span);
    }
}

public class SpanCustomTests
{
    private static readonly IReadOnlyList<MonitorInfo> Owner =
    [
        new(1, @"\\.\DISPLAY1", true, new PixelRect(0, 0, 2560, 1440), new PixelRect(0, 0, 2560, 1392)),
        new(2, @"\\.\DISPLAY2", false, new PixelRect(2560, 0, 1920, 1080), new PixelRect(2560, 0, 1920, 1032)),
    ];

    /// <summary>The owner's hand-made trio: two small windows top/bottom right, the swap main (slot 3) big on the left.</summary>
    private static LayoutProfile Trio() => new()
    {
        Name = "Trio",
        Clients = 3,
        Preset = PresetKind.Custom,
        SwapMainSlots = [3],
        Options = new LayoutOptions { Borderless = true },
        Slots =
        [
            new LayoutSlot { N = 1, Role = SlotRole.Main, X = 66.0156, Y = 0, W = 33.9844, H = 50 },
            new LayoutSlot { N = 2, X = 66.0156, Y = 50, W = 33.9844, H = 50 },
            new LayoutSlot { N = 3, X = 0, Y = 0, W = 66.0156, H = 100, Fit = FitMode.None },
        ],
    };

    [Fact]
    public void Custom_layout_spans_its_swap_main_and_nothing_else_moves()
    {
        var before = LayoutEngine.Resolve(Trio(), Owner);
        var spanned = Presets.SpanMain(Trio(), Owner, on: true);
        var placed = LayoutEngine.Resolve(spanned, Owner);

        Assert.True(spanned.Slots[2].Span);
        Assert.Equal(new PixelRect(0, 0, 4480, 1392), placed[2].Rect);
        Assert.Equal(before[0].Rect, placed[0].Rect);
        Assert.Equal(before[1].Rect, placed[1].Rect);
        Assert.Equal(ZOrder.Top, placed[0].Slot.Z);
        Assert.Equal(ZOrder.Bottom, placed[2].Slot.Z);
        Assert.Equal(2, spanned.Screens!.Count);
        Assert.Equal(new PixelRect(2560, 0, 1920, 1032), Topology.For(spanned, placed, Owner)!.Team);

        var off = Presets.SpanMain(spanned, Owner, on: false);
        Assert.False(off.MainSpans);
        Assert.Equal(before[2].Rect, LayoutEngine.Resolve(off, Owner)[2].Rect);
    }

    [Fact]
    public void Without_a_swap_main_the_biggest_window_spans_and_one_monitor_changes_nothing()
    {
        Assert.True(Presets.SpanMain(Trio() with { SwapMainSlots = null }, Owner, on: true).Slots[2].Span);
        var trio = Trio();
        Assert.Same(trio, Presets.SpanMain(trio, [Owner[0]], on: true));
    }
}
