using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Core.Tests;

/// <summary>Reading the WoWBoxer AddOn's bar measurement and the characters' bar switches (read only).</summary>
public class BarBridgeTests
{
    // the shape the addon really writes (Forever, 2026-11-06; names made up), with diag.bars – another table – before it
    private const string Forever = """

        WoWBoxerDB = {
        ["diag"] = {
        ["bars"] = { ["counts"] = { ["placed"] = 12, }, ["items"] = { { "WBX K1", 63, "placed", "macro:WBX K1", }, }, },
        },
        ["bridge"] = {
        ["bars"] = {
        ["covered"] = 0,
        ["scale"] = 0.64,
        ["window"] = { ["w"] = 2560, ["h"] = 1369, },
        ["outside"] = 0,
        ["measuredAt"] = 1794169030,
        ["fitsAtMin"] = false,
        ["version"] = 1,
        ["minScale"] = 0.64,
        ["game"] = "forever",
        ["bars"] = {
        { ["covered"] = 0, ["label"] = "Action Bar 1", ["outside"] = 0, ["left"] = 521, ["top"] = 47, ["key"] = "main", ["anchor"] = "center/bottom", ["right"] = 1083, ["bottom"] = 2, ["buttons"] = 12, }, -- [1]
        { ["covered"] = 0, ["label"] = "Bottom Left bar (bar 2)", ["outside"] = 0, ["left"] = 543, ["top"] = 117, ["key"] = "bottomLeft", ["anchor"] = "center/bottom", ["right"] = 1105, ["bottom"] = 72, ["buttons"] = 12, }, -- [2]
        { ["covered"] = 0, ["label"] = "Bottom Right bar (bar 3)", ["outside"] = 0, ["left"] = 1127, ["top"] = 117, ["key"] = "bottomRight", ["anchor"] = "center/bottom", ["right"] = 1689, ["bottom"] = 72, ["buttons"] = 12, }, -- [3]
        },
        ["ui"] = { ["w"] = 2244, ["h"] = 1200, },
        },
        ["character"] = "Testa",
        ["class"] = "WARLOCK",
        ["level"] = 15,
        },
        }
        WoWBoxerCharDB = nil
        """;

    [Fact]
    public void Reads_the_real_forever_measurement_relative_to_the_bottom_centre()
    {
        var b = BridgeBars.Parse(Forever)!;
        Assert.Equal("Testa", b.Character);
        Assert.Equal("forever", b.Game);
        Assert.Equal(3, b.Bars.Count);
        var main = b.Bars[0];
        Assert.Equal(("main", BarAnchorH.Center, BarAnchorV.Bottom), (main.Key, main.H, main.V));
        Assert.Equal((-601.0, 2.0, -39.0, 47.0), (main.X0, main.Y0, main.X1, main.Y1));
        Assert.Equal((5.0, 567.0), (b.Bars[2].X0, b.Bars[2].X1));
        // the built-in Forever default is this very measurement
        foreach (var box in b.Bars) Assert.Equal(ActionBars.ForeverDefault.First(d => d.Key == box.Key) with { Label = box.Label }, box);
    }

    // live Forever log 2026-11-09, a 870x696 follower (UI 960x768 at 1.00): the game pushed bar 1 to the left edge and
    // bar 3 to the right edge, on top of 4 buttons of bar 2
    private const string ForeverSqueezed = """
        WoWBoxerDB = {
        ["bridge"] = {
        ["bars"] = {
        ["covered"] = 8, ["scale"] = 1, ["window"] = { ["w"] = 870, ["h"] = 696, }, ["outside"] = 0, ["version"] = 1, ["game"] = "forever",
        ["bars"] = {
        { ["label"] = "Action Bar 1", ["left"] = 0, ["top"] = 47, ["key"] = "main", ["anchor"] = "center/bottom", ["right"] = 562, ["bottom"] = 2, ["outside"] = 0, ["covered"] = 0, ["buttons"] = 12, },
        { ["label"] = "Bottom Left bar (bar 2)", ["left"] = 22, ["top"] = 117, ["key"] = "bottomLeft", ["anchor"] = "center/bottom", ["right"] = 584, ["bottom"] = 72, ["outside"] = 0, ["covered"] = 4, ["buttons"] = 12, },
        { ["label"] = "Bottom Right bar (bar 3)", ["left"] = 398, ["top"] = 117, ["key"] = "bottomRight", ["anchor"] = "center/bottom", ["right"] = 960, ["bottom"] = 72, ["outside"] = 0, ["covered"] = 4, ["buttons"] = 12, },
        },
        ["ui"] = { ["w"] = 960, ["h"] = 768, },
        },
        ["character"] = "Testb",
        },
        }
        """;

    [Fact]
    public void A_squeezed_measurement_gives_the_bars_but_not_their_places()
    {
        var b = BridgeBars.Parse(ForeverSqueezed)!;
        Assert.True(b.Squeezed);
        Assert.False(BridgeBars.Parse(Forever)!.Squeezed);
        var r = BarSetup.For("acc", "Testb", WowGame.Forever, null, b);
        Assert.Equal(["main", "menu", "bottomLeft", "bottomRight"], r.Bars.Select(x => x.Key));
        Assert.Equal(ActionBars.ForeverDefault.Where(d => r.Bars.Any(x => x.Key == d.Key)), r.Bars);
        Assert.StartsWith("default positions", r.Source);
        // and so the follower gets a scale that fits instead of "none"
        Assert.Equal(0.79, ActionBars.FitScale(r.Bars, 870.0 / 696));
        // the addon's own flag (1.2.0-test.4) counts too, also when the totals look clean
        Assert.True(BridgeBars.Parse(Forever.Replace("[\"version\"] = 1,", "[\"version\"] = 1, [\"squeezed\"] = true,"))!.Squeezed);
    }

    [Fact]
    public void No_bridge_bars_means_none()
    {
        Assert.Null(BridgeBars.Parse("WoWBoxerDB = { [\"diag\"] = { [\"bars\"] = { [\"counts\"] = {} } } }"));
        Assert.Null(BridgeBars.Parse("WoWBoxerDB = { [\"bridge\"] = { [\"version\"] = 1, [\"character\"] = \"X\" } }"));
        Assert.Null(BridgeBars.Parse("not lua at all {{{"));
        Assert.Null(BridgeBars.Parse(""));
    }

    [Fact]
    public void Lua_reader_handles_data_only()
    {
        var all = LuaData.ParseFile("""
            -- comment
            A = { 1, "two", true, nil, ["k"] = -1.5e-3, x = { [3] = 'q\"uo\nte' }, ["n"] = 0x10, }
            B = false
            """);
        var a = all["A"];
        Assert.Equal(1.0, LuaData.Number(a, "1"));
        Assert.Equal("two", LuaData.Text(a, "2"));
        Assert.Equal(true, LuaData.Get(a, "3"));
        Assert.Null(LuaData.Get(a, "4"));
        Assert.Equal(-0.0015, LuaData.Number(a, "k")!.Value, 6);
        Assert.Equal("q\"uo\nte", LuaData.Text(a, "x", "3"));
        Assert.Equal(16.0, LuaData.Number(a, "n"));
        Assert.Equal(false, all["B"]);
    }

    [Fact]
    public void Era_keeps_the_1024_main_bar_even_when_the_addon_measured_only_its_buttons()
    {
        var measured = new BridgeBars("X", "era", 1024, 768, 1,
            [new BarBox("main", "Action Bar 1", BarAnchorH.Center, BarAnchorV.Bottom, -504, 4, -6, 40),
             new BarBox("bottomLeft", "Bottom Left bar (bar 2)", BarAnchorH.Center, BarAnchorV.Bottom, -504, 53, -6, 89)], null);
        var bars = BarSetup.For("acc", "X", WowGame.ClassicEra, 1, measured).Bars;
        Assert.Equal(ActionBars.EraDefault[0], bars.Single(b => b.Key == "main"));
        Assert.Equal(-504, bars.Single(b => b.Key == "bottomLeft").X0);
    }

    [Fact]
    public void Switches_say_which_bars_the_addon_says_where()
    {
        var bridge = BridgeBars.Parse(Forever)!;
        // the character turned the right bars on after the last measurement: those come from the default layout
        var r = BarSetup.For("acc", "Testa", WowGame.Forever, 15, bridge);
        Assert.Equal(["main", "menu", "bottomLeft", "bottomRight", "right", "left"], r.Bars.Select(b => b.Key));
        Assert.Equal("measured by the WoWBoxer AddOn", r.Source);
        // bar 3 turned off again: it does not count, even though it was measured
        Assert.Equal(["main", "menu", "bottomLeft"], BarSetup.For("acc", "Testa", WowGame.Forever, 1, bridge).Bars.Select(b => b.Key));
        // no switches found: the measured bars (and the bag and menu buttons)
        Assert.Equal(4, BarSetup.For("acc", "Testa", WowGame.Forever, null, bridge).Bars.Count);
        // a measurement from another game is not used
        Assert.Equal("bar 1 only (no settings found)", BarSetup.For("acc", null, WowGame.ClassicEra, null, bridge).Source);
    }

    [Fact]
    public void Reads_an_install_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "wbx-bars-" + Guid.NewGuid().ToString("N"));
        try
        {
            var acc = Path.Combine(root, "WTF", "Account", "ACC1");
            Directory.CreateDirectory(Path.Combine(acc, "93", "Testa-Realm"));
            Directory.CreateDirectory(Path.Combine(acc, "SavedVariables"));
            File.WriteAllText(Path.Combine(acc, "93", "Testa-Realm", "config-cache.wtf"), "SET enableMultiActionBars \"3\"\r\n");
            File.WriteAllText(Path.Combine(acc, "SavedVariables", "WoWBoxer.lua"), Forever);
            var acc2 = Path.Combine(root, "WTF", "Account", "ACC2", "Realm", "Other");
            Directory.CreateDirectory(acc2);
            File.WriteAllText(Path.Combine(acc2, "config-cache.wtf"), "SET enableMultiActionBars \"15\"\r\n");
            var accounts = WtfInfo.Read(root);
            var bars = BarSetup.Read(root, WowGame.Forever, accounts);
            Assert.Equal("Testa-Realm", bars["ACC1"].Character);
            Assert.Equal(4, bars["ACC1"].Bars.Count);
            Assert.Equal("measured by the WoWBoxer AddOn", bars["ACC1"].Source);
            Assert.Equal(6, bars["ACC2"].Bars.Count);
            Assert.Equal("the character's settings, default positions", bars["ACC2"].Source);
            Assert.Equal("1, bags, 2, 3, right, right 2", bars["ACC2"].Names);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
