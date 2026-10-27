using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Core.Tests;

public class UiScaleTests
{
    [Fact]
    public void Missing_lines_mean_automatic()
    {
        var s = UiScale.Read("SET portal \"EU\"\r\nSET gxMaximize \"0\"\r\n");
        Assert.False(s.Use);
        Assert.Equal(1, s.Effective);
        Assert.Equal("automatic", s.Display);
    }

    [Fact]
    public void Reads_use_and_scale()
    {
        var s = UiScale.Read("SET useUiScale \"1\"\r\nSET GxMaximize \"1\"\r\nSET uiScale \"0.64\"\r\n");
        Assert.True(s.Use);
        Assert.Equal(0.64, s.Effective, 3);
    }

    [Fact]
    public void Scale_without_use_is_ignored()
    {
        Assert.Equal(1, UiScale.Read("SET uiScale \"0.7\"\n").Effective);
    }

    [Theory]
    [InlineData(830.0 / 664, 0.93)]    // owner's followers (5:4 game area)
    [InlineData(1726.0 / 1360, 0.95)]  // owner's main
    [InlineData(4.0 / 3, 1.0)]
    [InlineData(16.0 / 9, 1.0)]
    [InlineData(0.6, 0.64)]            // WoW's minimum
    public void Scale_for_the_whole_bar(double aspect, double expected)
    {
        Assert.Equal(expected, UiScale.ForFullBar(aspect), 3);
    }

    [Fact]
    public void The_written_scale_really_shows_the_bar()
    {
        foreach (var aspect in new[] { 1.0, 1.1, 1.2, 1.25, 1.269, 1.3, 1.33 })
        {
            var scale = UiScale.ForFullBar(aspect);
            Assert.True(UiScale.EffectiveAspect(aspect, new UiScaleSetting(true, scale)) >= 4.0 / 3 - 1e-9, $"aspect {aspect} scale {scale}");
        }
    }

    [Theory]
    [InlineData(1.25, "Buttons clipped")]          // owner's follower screenshot: no left gryphon, cut mid-bags
    [InlineData(1.31, "Gryphon ends clipped")]     // 868x664 field test
    [InlineData(1726.0 / 1360, "Gryphon ends clipped")]  // owner's main: every button incl. backpack visible
    [InlineData(1.344, "Full action bar")]         // 1.25 at UI Scale 0.93
    public void Verdicts_match_the_field_tests(double effective, string expected) => Assert.Equal(expected, UiScale.BarVerdict(effective));

    [Fact]
    public void Squad_gets_the_smallest_scale_or_none()
    {
        Assert.Equal(0.93, UiScale.ForSquad([1726.0 / 1360, 830.0 / 664, 830.0 / 664])!.Value, 3);
        Assert.Null(UiScale.ForSquad([1.5, 1.8]));
    }

    [Fact]
    public void Writing_replaces_existing_lines_and_keeps_the_rest()
    {
        const string before = "SET portal \"EU\"\r\nSET useUiScale \"0\"\r\nSET uiScale \"1\"\r\nSET gxMaximize \"0\"\r\n";
        var after = UiScale.WithScale(before, use: true, 0.93);
        Assert.Equal("SET portal \"EU\"\r\nSET useUiScale \"1\"\r\nSET uiScale \"0.93\"\r\nSET gxMaximize \"0\"\r\n", after);
    }

    [Fact]
    public void Writing_appends_missing_lines()
    {
        var after = UiScale.WithScale("SET portal \"EU\"", use: true, 0.9);
        Assert.Equal("SET portal \"EU\"\nSET useUiScale \"1\"\nSET uiScale \"0.90\"\n", after);
        var read = UiScale.Read(after);
        Assert.True(read.Use);
        Assert.Equal(0.9, read.Scale, 3);
    }
}
