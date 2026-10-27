namespace WoWBoxer.Layouts.Core.Tests;

public class ProfileJsonTests
{
    [Theory]
    [InlineData(PresetKind.Side)]
    [InlineData(PresetKind.Overlay)]
    [InlineData(PresetKind.Grid)]
    public void Json_round_trip(PresetKind kind)
    {
        var profile = Presets.Create(kind, 5, TestMonitors.Reference);

        Assert.Equal(profile, ProfileJson.Deserialize(ProfileJson.Serialize(profile)));
    }

    [Fact]
    public void Class_and_preset_round_trip()
    {
        var profile = Presets.Create(PresetKind.Side, 3, TestMonitors.Reference);
        profile = profile with { Slots = profile.Slots.Select(s => s with { Class = "paladin", Name = "Tank" }).ToList() };

        var json = ProfileJson.Serialize(profile);

        Assert.Contains("\"class\": \"paladin\"", json);
        Assert.Contains("\"preset\": \"side\"", json);
        Assert.Equal(profile, ProfileJson.Deserialize(json));
    }

    [Fact]
    public void Share_code_round_trip()
    {
        var profile = Presets.Create(PresetKind.Dual, 4, TestMonitors.Setups["dual-mixed"]);

        var code = ProfileJson.ToShareCode(profile);

        Assert.StartsWith("WBX1:", code);
        Assert.Equal(profile, ProfileJson.FromShareCode(code));
    }

    [Fact]
    public void Json_uses_the_shared_format()
    {
        var json = ProfileJson.Serialize(Presets.Create(PresetKind.Overlay, 3, TestMonitors.Reference));

        Assert.Contains("\"format\": \"wowboxer-layout/1\"", json);
        Assert.Contains("\"clients\": 3", json);
        Assert.Contains("\"z\": \"top\"", json);
        Assert.Contains("\"fit\": \"shrink-anchor\"", json);
        Assert.Contains("\"minAspect\": 1.25", json);
    }

    [Fact]
    public void Reads_the_example_from_the_plan()
    {
        const string json = """
            { "format": "wowboxer-layout/1", "name": "Overlay 2", "clients": 2,
              "slots": [
                { "n": 1, "role": "main", "monitor": "primary", "x": 0, "y": 0, "w": 100, "h": 100, "z": "bottom" },
                { "n": 2, "role": "follower", "monitor": "primary", "x": 8.3, "y": 73, "w": 19, "h": 23, "z": "top" } ],
              "options": { "borderless": true, "minAspect": 1.25, "fit": "shrink-anchor", "useTaskbarArea": false } }
            """;

        var profile = ProfileJson.Deserialize(json);

        Assert.Equal(2, profile.Slots.Count);
        Assert.Equal(ZOrder.Top, profile.Slots[1].Z);
        Assert.Equal(8.3, profile.Slots[1].X);
        Assert.False(profile.Options.ResizeNudge, "missing options keep their defaults (nudge off)");
    }

    [Theory]
    [InlineData("""{ "format": "other/1", "clients": 1, "slots": [ { "n": 1, "w": 100, "h": 100 } ] }""", "Unknown format")]
    [InlineData("""{ "clients": 2, "slots": [ { "n": 1, "w": 100, "h": 100 } ] }""", "\"clients\" is 2")]
    [InlineData("""{ "clients": 1, "slots": [ { "n": 1, "x": 50, "w": 60, "h": 100 } ] }""", "outside its monitor")]
    [InlineData("""{ "clients": 2, "slots": [ { "n": 1, "w": 50, "h": 100 }, { "n": 3, "w": 50, "h": 100 } ] }""", "without gaps")]
    [InlineData("""{ "clients": 1, "slots": [ """, "Not a valid layout file")]
    public void Bad_profiles_give_readable_errors(string json, string expected)
    {
        var e = Assert.Throws<FormatException>(() => ProfileJson.Deserialize(json));
        Assert.Contains(expected, e.Message);
    }

    [Fact]
    public void Bad_share_codes_give_readable_errors()
    {
        Assert.Contains("starts with", Assert.Throws<FormatException>(() => ProfileJson.FromShareCode("hello")).Message);
        Assert.Contains("damaged", Assert.Throws<FormatException>(() => ProfileJson.FromShareCode("WBX1:***")).Message);
    }
}
