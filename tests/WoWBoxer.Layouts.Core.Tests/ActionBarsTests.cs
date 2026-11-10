using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Core.Tests;

/// <summary>Auto-fit for every enabled bar (owner bug 2026-11-06: bars 2 and 3 on, buttons missing in small windows).</summary>
public class ActionBarsTests
{
    // game areas (client area without the title bar) from the owner's layouts
    private const double SmallFollower = 830.0 / 664;    // 1.25
    private const double Follower = 868.0 / 664;         // 870x696 window, 1.31
    private const double Main = 1688.0 / 1360;           // 1690x1392 window, 1.24
    private const double Wide = 2558.0 / 1360;          // one client on the whole monitor, 1.88

    public static readonly TheoryData<string, int> Sets = new()
    {
        { "bar 1", 0 },
        { "bars 1+2", 1 },
        { "bars 1+2+3", 3 },
        { "bars 1+2+3 + right bars", 15 },
    };

    private static IReadOnlyList<BarBox> Bars(WowGame game, int mask) => ActionBars.Layout(game, ActionBars.EnabledKeys(mask));

    [Theory]
    [MemberData(nameof(Sets))]
    public void Era_large_windows_fit_everything_at_scale_1(string set, int mask)
    {
        Assert.Equal(1.0, ActionBars.FitScale(Bars(WowGame.ClassicEra, mask), Wide));
        Assert.True(ActionBars.Fits(Bars(WowGame.ClassicEra, mask), 4.0 / 3, 1), set);
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void Era_bars_2_3_and_right_need_no_more_than_the_main_bar(string set, int mask)
    {
        // bars 2 and 3 sit inside the 1024-unit main bar, the right bars above it: the verified 4:3 rule still decides
        foreach (var aspect in new[] { 1.0, 1.1, SmallFollower, Main, 1.3, Follower, 1.33 })
            Assert.True(UiScale.ForFullBar(aspect) == ActionBars.FitScale(Bars(WowGame.ClassicEra, mask), aspect), $"{set} at {aspect}");
        Assert.Equal(0.93, ActionBars.FitScale(Bars(WowGame.ClassicEra, mask), SmallFollower));   // the owner's 0.93
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void Forever_large_windows_fit_everything(string set, int mask)
    {
        var bars = Bars(WowGame.Forever, mask);
        Assert.Equal(1.0, ActionBars.FitScale(bars, Wide));
        // and at the scale Forever draws a 1360-pixel-high window at by itself (0.64)
        Assert.True(ActionBars.Fits(bars, Main, ActionBars.DrawnScale(WowGame.Forever, UiScaleSetting.Automatic, 1360)), set);
    }

    [Theory]
    [InlineData(0, 0.83)]    // bar 1 lies left of centre: 601 units to its left end
    [InlineData(1, 0.83)]
    [InlineData(3, 0.83)]    // bars 2 and 3 side by side, 1146 units together
    [InlineData(15, 0.81)]   // the right bars reach down onto bar 3 until the UI is 948 units high
    public void Forever_small_windows_lose_buttons_at_their_own_scale_and_get_one_that_fits(int mask, double expected)
    {
        var bars = Bars(WowGame.Forever, mask);
        // a 664-pixel-high Forever window draws at 1.00 by itself: bar 1 is cut on the left (the bug)
        var drawn = ActionBars.DrawnScale(WowGame.Forever, UiScaleSetting.Automatic, 664);
        Assert.Equal(1.0, drawn);
        var check = ActionBars.Check(bars, Follower, drawn);
        Assert.Contains(check.Outside, b => b.Key == "main");
        Assert.Equal(expected, ActionBars.FitScale(bars, Follower));
    }

    [Fact]
    public void Forever_right_bars_cover_bar_3_in_a_short_wide_window()
    {
        var withRight = Bars(WowGame.Forever, 15);
        var check = ActionBars.Check(withRight, 1.5, 0.95);
        Assert.Contains(check.Covered, b => b.Key == "right");
        Assert.Contains(check.Covered, b => b.Key == "bottomRight");
        Assert.Equal(0.95, ActionBars.FitScale(Bars(WowGame.Forever, 3), 1.5));
        Assert.Equal(0.86, ActionBars.FitScale(withRight, 1.5));   // right bars clear of bar 3
    }

    [Theory]
    [InlineData(WowGame.ClassicEra, 0)]
    [InlineData(WowGame.ClassicEra, 15)]
    [InlineData(WowGame.Forever, 0)]
    [InlineData(WowGame.Forever, 3)]
    [InlineData(WowGame.Forever, 15)]
    public void The_fit_scale_is_the_largest_that_fits(WowGame game, int mask)
    {
        var bars = Bars(game, mask);
        foreach (var aspect in new[] { 0.9, 1.0, SmallFollower, Main, Follower, 1.4, 1.5, 1.6, Wide })
        {
            var fit = ActionBars.FitScale(bars, aspect);
            if (fit is null)
            {
                Assert.False(ActionBars.Fits(bars, aspect, UiScale.Min), $"{game} {mask} {aspect}");
                continue;
            }
            Assert.InRange(fit.Value, UiScale.Min, UiScale.Max);
            Assert.True(ActionBars.Fits(bars, aspect, fit.Value));
            if (fit < UiScale.Max) Assert.False(ActionBars.Fits(bars, aspect, fit.Value + 0.01), $"{game} {mask} {aspect}: {fit} + 0.01 fits too");
            for (var s = fit.Value; s >= UiScale.Min - 1e-9; s -= 0.01)   // and every smaller scale fits as well
                Assert.True(ActionBars.Fits(bars, aspect, s), $"{game} {mask} {aspect} at {s}");
        }
    }

    [Theory]
    [InlineData(WowGame.ClassicEra)]
    [InlineData(WowGame.Forever)]
    public void A_very_narrow_window_does_not_fit_even_at_the_minimum(WowGame game)
    {
        var bars = Bars(game, 3);
        Assert.Null(ActionBars.FitScale(bars, 0.8));
        Assert.False(ActionBars.Check(bars, 0.8, UiScale.Min).Fits);
        Assert.Contains("Action Bar 1", ActionBars.Describe(ActionBars.Check(bars, 0.8, UiScale.Min)));
    }

    [Fact]
    public void Forever_bag_buttons_are_cut_in_the_owners_follower_window()
    {
        // live 2026-11-09, 870x696 at UI Scale 1: bar 3 lay on 4 buttons of bar 2, the last bag was cut
        var check = ActionBars.Check(Bars(WowGame.Forever, 3), 870.0 / 696, 1);
        Assert.Contains(check.Outside, b => b.Key == "bottomRight");
        Assert.Contains(check.Outside, b => b.Key == "menu");
        Assert.Equal(0.79, ActionBars.FitScale(Bars(WowGame.Forever, 3), 870.0 / 696));
    }

    [Fact]
    public void A_bar_at_the_bottom_edge_by_design_is_not_cut()
    {
        // Forever puts bar 1 two units above the bottom edge; the margin there is what the game gives it
        Assert.True(ActionBars.Fits(Bars(WowGame.Forever, 0), Wide, 1));
    }

    [Fact]
    public void Enabled_bars_come_from_the_bitmask()
    {
        Assert.Equal(["main", "menu"], ActionBars.EnabledKeys(0));
        Assert.Equal(["main", "menu", "bottomLeft", "bottomRight"], ActionBars.EnabledKeys(3));
        Assert.Equal(["main", "menu", "bottomLeft", "bottomRight", "right", "left"], ActionBars.EnabledKeys(15));
        // Classic Era's bag and menu buttons are part of its 1024-unit main bar
        Assert.Equal(["main", "bottomLeft", "bottomRight"], ActionBars.Layout(WowGame.ClassicEra, ActionBars.EnabledKeys(3)).Select(b => b.Key));
        Assert.Equal(3, ActionBars.ReadMask("SET lockActionBars \"0\"\r\nSET enableMultiActionBars \"3\"\r\n"));
        Assert.Null(ActionBars.ReadMask("SET lockActionBars \"0\"\r\n"));
    }

    [Fact]
    public void Drawn_scale_per_game()
    {
        Assert.Equal(1, ActionBars.DrawnScale(WowGame.ClassicEra, UiScaleSetting.Automatic, 664));
        Assert.Equal(1, ActionBars.DrawnScale(WowGame.ClassicEra, UiScaleSetting.Automatic, 1360));
        Assert.Equal(0.64, ActionBars.DrawnScale(WowGame.Forever, UiScaleSetting.Automatic, 1369), 3);
        Assert.Equal(768.0 / 1000, ActionBars.DrawnScale(WowGame.Forever, UiScaleSetting.Automatic, 1000), 3);
        Assert.Equal(1, ActionBars.DrawnScale(WowGame.Forever, UiScaleSetting.Automatic, 664));
        Assert.Equal(0.8, ActionBars.DrawnScale(WowGame.Forever, new UiScaleSetting(true, 0.8), 1369), 3);
    }
    [Fact]
    public void Bags_and_menu_on_bar_1_count_as_covered()
    {
        // owner 2026-11-10 (addon bridge, 870x696 at scale 1): bags/menu at 445-960 lay on bar 1 at 0-562
        BarBox main = new("main", "Action Bar 1", BarAnchorH.Left, BarAnchorV.Bottom, 4, 2, 562, 47);
        BarBox menu = new("menu", "Bag and menu buttons", BarAnchorH.Left, BarAnchorV.Bottom, 445, 2, 956, 47);
        var check = ActionBars.Check([main, menu], 960.0 / 768, 1);
        Assert.Contains(check.Covered, b => b.Key == "menu");
        Assert.Contains(check.Covered, b => b.Key == "main");
    }

    [Fact]
    public void Tall_menu_frames_touching_the_bar_above_do_not_count()
    {
        BarBox menu = new("menu", "Bag and menu buttons", BarAnchorH.Center, BarAnchorV.Bottom, -30, 2, 587, 90);
        BarBox bar3 = new("bottomRight", "Bottom Right bar (bar 3)", BarAnchorH.Center, BarAnchorV.Bottom, 5, 72, 567, 117);
        Assert.True(ActionBars.Check([menu, bar3], 2.0, 0.64).Fits);
    }

    [Fact]
    public void Forever_follower_870x696_needs_a_scale_where_bags_clear_bar_1()
    {
        var bars = ActionBars.Layout(WowGame.Forever, ActionBars.EnabledKeys(3));
        var fit = ActionBars.FitScale(bars, 870.0 / 696)!.Value;
        Assert.InRange(fit, 0.64, 0.80);
        Assert.False(ActionBars.Fits(bars, 870.0 / 696, 1));
    }
}
