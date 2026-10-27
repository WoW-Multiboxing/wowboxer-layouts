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
}
