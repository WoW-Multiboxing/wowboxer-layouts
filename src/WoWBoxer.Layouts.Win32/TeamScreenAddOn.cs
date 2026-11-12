namespace WoWBoxer.Layouts.Win32;

public enum TeamScreenStatus { Missing, Old, Ready }

/// <summary>
/// Whether the WoWBoxer Team Screen add-on (the "Offhand companion") is installed in a game client: read-only, a file check.
/// Ready = it has GameArea.lua (keeps the game on monitor 1 in the stretched leader window, Team Screen 0.1.0-test.5 on).
/// </summary>
public static class TeamScreenAddOn
{
    public const string Folder = "WoWBoxerTeamScreen";

    public static TeamScreenStatus Check(IEnumerable<string> installs)
    {
        var best = TeamScreenStatus.Missing;
        foreach (var install in installs.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = Path.Combine(install, "Interface", "AddOns", Folder);
            try
            {
                if (!File.Exists(Path.Combine(dir, Folder + ".toc"))) continue;
                if (File.Exists(Path.Combine(dir, "GameArea.lua"))) return TeamScreenStatus.Ready;
                best = TeamScreenStatus.Old;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return best;
    }
}
