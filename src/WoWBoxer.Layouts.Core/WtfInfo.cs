namespace WoWBoxer.Layouts.Core;

/// <summary>A character folder under <c>WTF\Account\&lt;ACCOUNT&gt;\&lt;Realm&gt;\&lt;Character&gt;</c>.</summary>
public sealed record WtfCharacter(string Account, string Realm, string Name, DateTime LastWrite)
{
    public string Display => $"{Name} – {Realm}";
}

/// <summary>A WoW account folder with its characters, newest first.</summary>
public sealed record WtfAccount(string Name, IReadOnlyList<WtfCharacter> Characters)
{
    /// <summary>The character whose folder WoW wrote last – a hint for "last played", never a fact.</summary>
    public WtfCharacter? LastPlayed => Characters.Count > 0 ? Characters[0] : null;
}

/// <summary>
/// Read-only hints from a WoW install's WTF folder: which accounts and characters exist and which character was played
/// last (folder write time). Never writes, never reads game memory. Classes are not in these files.
/// </summary>
public static class WtfInfo
{
    private static readonly HashSet<string> NotRealms = new(StringComparer.OrdinalIgnoreCase) { "SavedVariables" };

    /// <param name="installFolder">The client folder, e.g. <c>…\World of Warcraft\_classic_era_</c>.</param>
    public static IReadOnlyList<WtfAccount> Read(string installFolder)
    {
        var accountRoot = Path.Combine(installFolder, "WTF", "Account");
        if (!Directory.Exists(accountRoot)) return [];
        var accounts = new List<WtfAccount>();
        foreach (var accountDir in SafeDirs(accountRoot))
        {
            var account = Path.GetFileName(accountDir);
            var characters = new List<WtfCharacter>();
            foreach (var realmDir in SafeDirs(accountDir).Where(d => !NotRealms.Contains(Path.GetFileName(d))))
                foreach (var charDir in SafeDirs(realmDir))
                    characters.Add(new WtfCharacter(account, Path.GetFileName(realmDir), Path.GetFileName(charDir), LastWrite(charDir)));
            // An account folder without config is a leftover (e.g. only Blizzard_SavedSets) – skip it.
            if (characters.Count == 0 && !File.Exists(Path.Combine(accountDir, "config-cache.wtf"))) continue;
            accounts.Add(new WtfAccount(account, characters.OrderByDescending(c => c.LastWrite).ToList()));
        }
        return accounts.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Newest write time of the folder or any file directly inside it (WoW rewrites them on logout).</summary>
    private static DateTime LastWrite(string dir)
    {
        var newest = Directory.GetLastWriteTimeUtc(dir);
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (t > newest) newest = t;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
        return newest;
    }

    private static IEnumerable<string> SafeDirs(string dir)
    {
        try
        {
            return Directory.GetDirectories(dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
