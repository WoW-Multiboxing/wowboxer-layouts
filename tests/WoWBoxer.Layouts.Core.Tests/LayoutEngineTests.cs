namespace WoWBoxer.Layouts.Core.Tests;

public class LayoutEngineTests
{
    private static readonly PixelRect Area = new(0, 0, 2560, 1392);

    [Theory]
    [InlineData(Anchor.Top, 0)]
    [InlineData(Anchor.Center, 348)]
    [InlineData(Anchor.Bottom, 696)]
    public void Narrow_slot_shrinks_to_5_to_4_and_is_anchored(Anchor anchor, int expectedY)
    {
        // 870 x 1392 is far narrower than 5:4, so the height becomes 870 / 1.25 = 696.
        var slot = new LayoutSlot { N = 1, X = 66.015625, Y = 0, W = 33.984375, H = 100, Anchor = anchor };

        var rect = LayoutEngine.Resolve(slot, Area, new LayoutOptions());

        Assert.Equal(new PixelRect(1690, expectedY, 870, 696), rect);
    }

    [Fact]
    public void Fit_none_keeps_the_rectangle()
    {
        var slot = new LayoutSlot { N = 1, W = 50, H = 100, Fit = FitMode.None };

        Assert.Equal(new PixelRect(0, 0, 1280, 1392), LayoutEngine.Resolve(slot, Area, new LayoutOptions()));
    }

    [Fact]
    public void Min_aspect_is_a_setting()
    {
        var slot = new LayoutSlot { N = 1, W = 50, H = 100 };

        var rect = LayoutEngine.Resolve(slot, Area, new LayoutOptions { MinAspect = 0.8 });

        Assert.Equal(new PixelRect(0, 0, 1280, 1392), rect);
    }

    [Fact]
    public void Offset_work_area_is_respected()
    {
        var area = new PixelRect(-1920, 360, 1920, 1032);
        var slot = new LayoutSlot { N = 1, X = 50, Y = 50, W = 50, H = 50 };

        Assert.Equal(new PixelRect(-960, 876, 960, 516), LayoutEngine.Resolve(slot, area, new LayoutOptions()));
    }

    [Fact]
    public void Neighbouring_slots_with_odd_sizes_share_an_edge()
    {
        var thirds = Enumerable.Range(0, 3)
            .Select(i => LayoutEngine.Resolve(new LayoutSlot { X = 100.0 / 3 * i, W = 100.0 / 3, H = 100, Fit = FitMode.None }, new PixelRect(0, 0, 1001, 500), new LayoutOptions()))
            .ToList();

        Assert.Equal(thirds[0].Right, thirds[1].X);
        Assert.Equal(thirds[1].Right, thirds[2].X);
        Assert.Equal(1001, thirds[2].Right);
    }

    [Fact]
    public void Missing_monitor_falls_back_to_primary_and_says_so()
    {
        var profile = new LayoutProfile { Clients = 1, Slots = [new LayoutSlot { N = 1, Monitor = "3", W = 100, H = 100 }] };

        var placed = LayoutEngine.Resolve(profile, TestMonitors.Reference);

        Assert.True(placed[0].MonitorFallback);
        Assert.Equal(1, placed[0].Monitor.Index);
    }

    [Fact]
    public void Taskbar_area_can_be_used()
    {
        var profile = new LayoutProfile { Clients = 1, Slots = [new LayoutSlot { N = 1, W = 100, H = 100, Fit = FitMode.None }], Options = new LayoutOptions { UseTaskbarArea = true } };

        Assert.Equal(new PixelRect(0, 0, 2560, 1440), LayoutEngine.Resolve(profile, TestMonitors.Reference)[0].Rect);
    }
}
