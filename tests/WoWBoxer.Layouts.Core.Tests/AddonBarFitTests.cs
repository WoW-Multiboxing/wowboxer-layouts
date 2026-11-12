using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Core.Tests;

/// <summary>
/// bug-2026-11-12-actionbars: the WoWBoxer AddOn fits the bars in each window (bridge.barFit), so the app leaves the
/// shared UI Scale alone, puts the player's own one back, and never goes below a sane value by itself.
/// </summary>
public class AddonBarFitTests
{
    // the shape the addon writes since 1.3.0-test.9 (names made up)
    private static string Bridge(string barFit, string mode = "scale", string scale = "0.78") => $$"""
        WoWBoxerDB = {
        ["bridge"] = {
        ["barFit"] = {{barFit}},
        ["barMode"] = "{{mode}}",
        ["barScale"] = {{scale}},
        ["bars"] = { ["version"] = 1, ["window"] = { ["w"] = 870, ["h"] = 696, }, ["ui"] = { ["w"] = 960, ["h"] = 768, }, },
        ["character"] = "Testa",
        },
        }
        """;

    [Fact]
    public void Reads_the_handshake_as_true_or_addon()
    {
        var b = BridgeFit.Parse(Bridge("true"))!;
        Assert.True(b.Addon);
        Assert.Equal("scale", b.Mode);
        Assert.Equal(0.78, b.Scale);
        Assert.Equal((870, 696), (b.WindowWidth, b.WindowHeight));
        Assert.True(BridgeFit.Parse(Bridge("\"addon\""))!.Addon);
        Assert.False(BridgeFit.Parse(Bridge("false"))!.Addon);
        Assert.False(BridgeFit.Parse(Bridge("\"app\""))!.Addon);
    }

    [Fact]
    public void An_older_addon_has_no_handshake()
    {
        Assert.Null(BridgeFit.Parse("WoWBoxerDB = { [\"bridge\"] = { [\"character\"] = \"X\" } }"));
        Assert.Null(BridgeFit.Parse(""));
        Assert.Null(BridgeFit.Parse("not lua {{{"));
    }

    [Theory]
    [InlineData("none", 870, 696, true)]
    [InlineData("scale", 1280, 1392, true)]
    [InlineData("stack", 870, 696, true)]
    [InlineData("nofit", 870, 696, false)]      // its own window: the addon's real result
    [InlineData("nofit", 874, 692, false)]
    [InlineData("nofit", 1280, 1392, null)]     // another size: the addon lays the bars out again there
    public void The_addon_result_counts_for_its_window(string mode, int w, int h, bool? expected) =>
        Assert.Equal(expected, BridgeFit.Parse(Bridge("true", mode))!.FitsIn(w, h));

    [Fact]
    public void No_result_while_the_addon_does_not_fit_the_bars() => Assert.Null(BridgeFit.Parse(Bridge("false"))!.FitsIn(870, 696));

    [Fact]
    public void Account_bars_carry_the_handshake()
    {
        var a = BarSetup.For("acc", "Testa", WowGame.Forever, 3, null) with { AddonFit = BridgeFit.Parse(Bridge("true")) };
        Assert.True(a.AddonFits);
        Assert.False(BarSetup.For("acc", "Testa", WowGame.Forever, 3, null).AddonFits);
    }

    private static readonly UiScaleSetting Auto = UiScaleSetting.Automatic, At074 = new(true, 0.74), At090 = new(true, 0.90);

    [Fact]
    public void Never_lowers_the_scale_while_the_addon_fits_any_window()
    {
        var p = UiScalePolicy.Decide(Auto, need: 0.75, alreadyFits: false, addonSlots: 1, slots: 3, restorable: false);
        Assert.Equal(UiScaleStep.Leave, p.Step);
        Assert.Contains("AddOn", p.Why);
    }

    [Fact]
    public void Puts_the_players_own_scale_back_when_the_addon_fits_every_window()
    {
        Assert.Equal(UiScaleStep.Restore, UiScalePolicy.Decide(At074, 0.75, false, addonSlots: 3, slots: 3, restorable: true).Step);
        // nothing to put back (never changed, or already back): leave it
        Assert.Equal(UiScaleStep.Leave, UiScalePolicy.Decide(At074, 0.75, false, 3, 3, restorable: false).Step);
        // some windows without the addon: not back yet, but never lower either
        Assert.Equal(UiScaleStep.Leave, UiScalePolicy.Decide(At074, 0.75, false, 2, 3, restorable: true).Step);
    }

    [Fact]
    public void Without_the_addon_the_fit_stops_at_the_sane_minimum()
    {
        var p = UiScalePolicy.Decide(Auto, need: 0.64, alreadyFits: false, addonSlots: 0, slots: 3, restorable: false);
        Assert.Equal(UiScaleStep.Lower, p.Step);
        Assert.Equal(UiScale.SaneMin, p.Scale);
        Assert.Equal(0.88, UiScalePolicy.Decide(Auto, 0.88, false, 0, 3, false).Scale);
        // already that small (the player's own 0.74): no change upwards or downwards
        Assert.Equal(UiScaleStep.Leave, UiScalePolicy.Decide(At074, 0.64, false, 0, 3, false).Step);
        Assert.Equal(UiScaleStep.Lower, UiScalePolicy.Decide(At090, 0.64, false, 0, 3, false).Step);
        Assert.Equal(UiScaleStep.Leave, UiScalePolicy.Decide(Auto, 0.64, alreadyFits: true, 0, 3, false).Step);
    }

    [Theory]
    [InlineData(null, true)]       // not even 0.64
    [InlineData(0.64, true)]
    [InlineData(0.74, true)]
    [InlineData(0.75, false)]
    [InlineData(0.9, false)]
    public void Windows_below_the_sane_minimum_need_the_addon(double? need, bool expected) => Assert.Equal(expected, UiScalePolicy.NeedsAddon(need));

    [Fact]
    public void Restoring_copies_the_backup_lines_or_removes_them()
    {
        const string now = "SET portal \"EU\"\r\nSET uiScale \"0.74\"\r\nSET useUiScale \"1\"\r\nSET gxWindow \"1\"\r\n";
        // the owner's backups have no UI Scale lines: automatic again, every other line kept
        Assert.Equal("SET portal \"EU\"\r\nSET gxWindow \"1\"\r\n", UiScale.WithOriginal(now, "SET portal \"EU\"\r\nSET gxWindow \"1\"\r\n"));
        // a player's own scale comes back as it was
        var own = UiScale.WithOriginal(now, "SET uiScale \"0.9\"\nSET useUiScale \"1\"\n");
        Assert.Equal(new UiScaleSetting(true, 0.9), UiScale.Read(own));
        Assert.Contains("SET gxWindow \"1\"", own);
        Assert.Equal(UiScaleSetting.Automatic, UiScale.Read(UiScale.WithOriginal(now, "")));
    }

    [Fact]
    public void Removing_a_line_keeps_the_others()
    {
        Assert.Equal("SET a \"1\"\nSET c \"3\"", WowConfig.WithoutValue("SET a \"1\"\nSET uiScale \"0.7\"\nSET c \"3\"", "uiScale"));
        Assert.Equal("SET a \"1\"\n", WowConfig.WithoutValue("SET a \"1\"\nSET uiScale \"0.7\"", "uiScale"));
        Assert.Equal("SET a \"1\"\n", WowConfig.WithoutValue("SET a \"1\"\n", "uiScale"));
        // the name must match the whole word
        Assert.Equal("SET uiScaleX \"1\"\n", WowConfig.WithoutValue("SET uiScaleX \"1\"\n", "uiScale"));
    }
}
