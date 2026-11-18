namespace WoWBoxer.Layouts.Core.Tests;

public class OffhandSpanTests
{
    private static readonly MonitorInfo Main = TestMonitors.Single(1920, 1080, dpi: 120);
    private static MonitorInfo Screen(int index, int x, int y, int width = 1920, int height = 1080) =>
        new(index, $"DISPLAY{index}", false, new(x, y, width, height), new(x, y, width, height - 48));

    [Theory]
    [InlineData(1920, 0, 50, 0, 0, 2880, 1080, "left", "33.3%")]
    [InlineData(-1920, 0, 25, -480, 0, 2400, 1080, "right", "20%")]
    [InlineData(0, -1080, 50, 0, -540, 1920, 1620, "bottom", "33.3%")]
    [InlineData(0, 1080, 100, 0, 0, 1920, 2160, "top", "50%")]
    public void Extends_only_into_the_near_side_of_the_chosen_display(int x, int y, int share,
        int left, int top, int width, int height, string side, string seam)
    {
        var workspace = Screen(4, x, y);
        Assert.True(OffhandSpan.TryCreate(Main, workspace, [Main, workspace], share, out var bounds, out var error), error);
        Assert.Equal(new PixelRect(left, top, width, height), bounds);
        Assert.Equal($"Mainhand {side} · workspace seam {seam} · aspect 16:9", OffhandSpan.Calibration(Main, workspace, bounds));
    }

    [Fact]
    public void Uses_physical_pixels_on_offset_mixed_resolution_displays_and_leaves_other_displays_free()
    {
        var workspace = Screen(4, 1920, -400, 2560, 1440);
        var all = new[] { Screen(2, -1920, 0), workspace, Main, Screen(3, 0, -1080) };
        Assert.True(OffhandSpan.TryCreate(Main, workspace, all, 50, out var bounds, out var error), error);
        Assert.Equal(new PixelRect(0, 0, 3200, 1080), bounds);
        Assert.Contains("outside the selected display", OffhandSpan.Calibration(Main, workspace, bounds));
    }

    [Theory]
    [InlineData(1920, 1080)] // corner contact
    [InlineData(3840, 0)]    // a gap or an intervening screen
    [InlineData(0, 0)]       // cloned display
    public void Requires_a_shared_edge(int x, int y)
    {
        var workspace = Screen(2, x, y);
        Assert.False(OffhandSpan.TryCreate(Main, workspace, [Main, workspace], 100, out _, out _));
    }

    [Fact]
    public void Rejects_covering_another_display_in_the_rectangular_window()
    {
        var workspace = Screen(2, 1920, 300);
        var third = Screen(3, 1920, -780);
        Assert.False(OffhandSpan.TryCreate(Main, workspace, [Main, workspace, third], 25, out _, out var error));
        Assert.Contains("third display", error);
    }

    [Fact]
    public void Requires_two_current_displays_and_a_calibratable_workspace_size()
    {
        var workspace = Screen(2, 1920, 0);
        Assert.False(OffhandSpan.TryCreate(Main, Main, [Main], 50, out _, out _));
        Assert.False(OffhandSpan.TryCreate(Main, workspace, [Main], 50, out _, out _));
        Assert.False(OffhandSpan.TryCreate(Main, workspace, [Main, workspace], 1, out _, out _));
        Assert.False(OffhandSpan.TryCreate(Main, workspace, [Main, workspace], 101, out _, out _));
    }
}
