namespace WoWBoxer.Layouts.Core.Tests;

public class ProfileCaptureTests
{
    // The owner's Windows Snap setup that draws WoW correctly (visible rectangles, title bars on).
    private static readonly PixelRect[] SnapSetup = [new(0, 0, 1753, 1392), new(1753, 0, 807, 696), new(1753, 696, 807, 696)];

    [Fact]
    public void Captured_layout_resolves_back_to_the_same_pixels()
    {
        var profile = ProfileCapture.FromWindows(SnapSetup, TestMonitors.Reference, borderless: false);

        var placed = LayoutEngine.Resolve(profile, TestMonitors.Reference);

        Assert.Equal(SnapSetup, placed.Select(p => p.Rect));
        Assert.False(profile.Options.Borderless);
        Assert.Empty(ProfileValidator.Validate(profile));
        Assert.Equal(profile, ProfileJson.Deserialize(ProfileJson.Serialize(profile)));
    }

    [Fact]
    public void Windows_on_the_second_monitor_keep_their_monitor()
    {
        var monitors = TestMonitors.Setups["dual-mixed"];
        PixelRect[] windows = [new(0, 0, 2560, 1380), new(-1920, 360, 960, 768)];

        var placed = LayoutEngine.Resolve(ProfileCapture.FromWindows(windows, monitors, borderless: true), monitors);

        Assert.Equal("2", placed[1].Slot.Monitor);
        Assert.Equal(windows, placed.Select(p => p.Rect));
    }

    [Fact]
    public void A_window_hanging_over_the_edge_is_clamped_to_the_work_area()
    {
        var profile = ProfileCapture.FromWindows([new PixelRect(-7, 0, 1767, 1399)], TestMonitors.Reference, borderless: false);

        Assert.Equal(new PixelRect(0, 0, 1760, 1392), LayoutEngine.Resolve(profile, TestMonitors.Reference)[0].Rect);
    }
}
