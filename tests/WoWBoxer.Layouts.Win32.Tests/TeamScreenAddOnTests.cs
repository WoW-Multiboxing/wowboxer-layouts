namespace WoWBoxer.Layouts.Win32.Tests;

/// <summary>The Offhand companion's add-on check, in a pretend client folder.</summary>
public sealed class TeamScreenAddOnTests : IDisposable
{
    private readonly string _install = Path.Combine(Path.GetTempPath(), "wbx-teamscreen-" + Guid.NewGuid().ToString("N"), "_forever_");
    private string AddOn => Path.Combine(_install, "Interface", "AddOns", TeamScreenAddOn.Folder);

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_install)!, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Missing_old_and_ready()
    {
        Assert.Equal(TeamScreenStatus.Missing, TeamScreenAddOn.Check([_install, ""]));
        Directory.CreateDirectory(AddOn);
        File.WriteAllText(Path.Combine(AddOn, "WoWBoxerTeamScreen.toc"), "## Title: WoWBoxer Team Screen\r\n");
        Assert.Equal(TeamScreenStatus.Old, TeamScreenAddOn.Check([_install]));
        File.WriteAllText(Path.Combine(AddOn, "GameArea.lua"), "");
        Assert.Equal(TeamScreenStatus.Ready, TeamScreenAddOn.Check([_install]));
    }
}
