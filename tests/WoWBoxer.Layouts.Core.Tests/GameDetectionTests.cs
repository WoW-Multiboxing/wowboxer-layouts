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

    private const string Era = @"C:\WoW\_classic_era_", Forever = @"C:\WoW\_forever_";
    private static readonly DateTime T0 = new(2026, 10, 27, 20, 0, 0);

    [Fact]
    public void Squad_follows_the_install_with_most_windows()
    {
        Assert.Null(GameDetection.SquadInstall([]));
        Assert.Equal(Era, GameDetection.SquadInstall([(Forever, T0), (Era, T0.AddMinutes(1)), (Era, T0.AddMinutes(2))]));
        Assert.Equal(Forever, GameDetection.SquadInstall([(Forever, T0.AddMinutes(3)), (Era, T0.AddMinutes(1)), (Forever, T0.AddMinutes(2))]));
    }

    [Fact]
    public void Squad_tie_goes_to_the_install_opened_first()
    {
        Assert.Equal(Forever, GameDetection.SquadInstall([(Era, T0.AddMinutes(2)), (Forever, T0.AddMinutes(1))]));
        Assert.Equal(Era, GameDetection.SquadInstall([(Era, T0), (Forever, T0.AddMinutes(1)), (Forever, T0.AddMinutes(5)), (Era.ToUpperInvariant(), T0.AddMinutes(9))]));
    }

    [Fact]
    public void Squad_stays_with_its_install_on_a_tie()
    {
        // 3 Forever + 2 Era with Forever followed, then one Forever client closes: 2 vs 2 – the squad stays with Forever.
        (string, DateTime)[] tie = [(Era, T0), (Era, T0.AddMinutes(1)), (Forever, T0.AddMinutes(2)), (Forever, T0.AddMinutes(3))];
        Assert.Equal(Forever, GameDetection.SquadInstall(tie, current: Forever));
        Assert.Equal(Era, GameDetection.SquadInstall(tie, current: Era));
        Assert.Equal(Era, GameDetection.SquadInstall(tie));   // no squad yet: the install opened first
        // more windows still win over the current one
        Assert.Equal(Forever, GameDetection.SquadInstall([(Era, T0), (Forever, T0.AddMinutes(1)), (Forever, T0.AddMinutes(2))], current: Era));
    }
}
