namespace WoWBoxer.Layouts.Core.Tests;

public class GameDetectionTests
{
    [Theory]
    [InlineData("WowClassic", true)]
    [InlineData("Wow", true)]
    [InlineData("WowT", true)]
    [InlineData("WoWBoxer.Layouts", false)]   // our own app must never be arranged
    [InlineData("wbx", false)]
    [InlineData("WowVoiceProxy", false)]
    [InlineData("WowErrorReporter", false)]
    [InlineData("Battle.net", false)]
    [InlineData(null, false)]
    public void Process_names(string? name, bool expected) => Assert.Equal(expected, GameDetection.IsWowProcessName(name));

    [Theory]
    [InlineData(@"C:\Program Files (x86)\World of Warcraft\_forever_\WowForever.exe", true)]
    [InlineData(@"D:\Games\World of Warcraft\_classic_era_\SomethingNew.exe", true)]
    [InlineData(@"C:\Program Files (x86)\World of Warcraft\World of Warcraft Launcher.exe", false)]
    [InlineData(@"C:\Program Files (x86)\World of Warcraft\_retail_\BlizzardError.exe", false)]
    [InlineData(@"C:\Tools\Wow.exe", false)]
    public void Install_paths(string path, bool expected) => Assert.Equal(expected, GameDetection.IsWowPath(path));

    // Exe names are shared between games (WowClassic.exe: Era + Anniversary; Wow.exe: Forever + Retail): the folder decides.
    [Theory]
    [InlineData(@"C:\Program Files (x86)\World of Warcraft\_classic_era_\WowClassic.exe", WowGame.ClassicEra)]
    [InlineData(@"C:\Program Files (x86)\World of Warcraft\_forever_\Wow.exe", WowGame.Forever)]
    [InlineData(@"D:\WoW\_anniversary_\WowClassic.exe", WowGame.Anniversary)]
    [InlineData(@"C:\Program Files (x86)\World of Warcraft\_retail_\Wow.exe", WowGame.Retail)]
    [InlineData(@"C:\Program Files (x86)\World of Warcraft\_classic_beta_\WowB.exe", WowGame.Other)]
    [InlineData(@"C:\Program Files (x86)\World of Warcraft\_CLASSIC_ERA_\WowClassic.exe", WowGame.ClassicEra)]
    [InlineData(@"C:\Tools\Wow.exe", WowGame.Unknown)]
    [InlineData("", WowGame.Unknown)]
    [InlineData(null, WowGame.Unknown)]
    public void Game_from_install_folder(string? path, WowGame expected) => Assert.Equal(expected, GameDetection.GameOf(path));

    [Fact]
    public void Layouts_remember_their_game_and_old_files_are_classic_era()
    {
        var old = ProfileJson.Deserialize(ProfileJson.Serialize(Presets.Create(PresetKind.Side, 3, TestMonitors.Reference)));
        Assert.Null(old.Game);
        Assert.Equal(WowGame.ClassicEra, old.GameOrDefault);
        var forever = old with { Game = WowGame.Forever };
        var json = ProfileJson.Serialize(forever);
        Assert.Contains("\"game\": \"forever\"", json);
        Assert.Equal(WowGame.Forever, ProfileJson.Deserialize(json).GameOrDefault);
        Assert.Equal(forever, ProfileJson.FromShareCode(ProfileJson.ToShareCode(forever)));
    }
}
