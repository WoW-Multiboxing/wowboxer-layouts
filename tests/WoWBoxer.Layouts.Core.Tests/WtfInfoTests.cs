namespace WoWBoxer.Layouts.Core.Tests;

public sealed class WtfInfoTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wbx-wtf-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Char(string account, string realm, string name, DateTime written)
    {
        var dir = Path.Combine(_root, "WTF", "Account", account, realm, name);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "config-cache.wtf");
        File.WriteAllText(file, "SET x \"1\"");
        File.SetLastWriteTimeUtc(file, written);
        Directory.SetLastWriteTimeUtc(dir, written);
        return dir;
    }

    [Fact]
    public void Lists_accounts_realms_and_characters_newest_first()
    {
        Char("ACC1", "Auberdine", "Old", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Char("ACC1", "Firemaw", "New", new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc));
        Char("ACC2", "Auberdine", "Rogue", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        Directory.CreateDirectory(Path.Combine(_root, "WTF", "Account", "ACC1", "SavedVariables"));

        var accounts = WtfInfo.Read(_root);

        Assert.Equal(["ACC1", "ACC2"], accounts.Select(a => a.Name));
        Assert.Equal("New", accounts[0].LastPlayed!.Name);
        Assert.Equal("Firemaw", accounts[0].LastPlayed!.Realm);
        Assert.Equal(2, accounts[0].Characters.Count);   // SavedVariables is not a realm
        Assert.Equal("Rogue – Auberdine", accounts[1].LastPlayed!.Display);
    }

    [Fact]
    public void Leftover_folders_without_config_or_characters_are_skipped()
    {
        Directory.CreateDirectory(Path.Combine(_root, "WTF", "Account", "LEFTOVER"));
        File.WriteAllText(Path.Combine(_root, "WTF", "Account", "LEFTOVER", "Blizzard_SavedSets.lua"), "");

        Assert.Empty(WtfInfo.Read(_root));
    }

    [Fact]
    public void Missing_install_gives_nothing() => Assert.Empty(WtfInfo.Read(Path.Combine(_root, "nope")));
}
