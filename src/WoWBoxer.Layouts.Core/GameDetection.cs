namespace WoWBoxer.Layouts.Core;

/// <summary>
/// Which WoW game a client belongs to, from the folder Blizzard installs it in (one folder per game under
/// "World of Warcraft"). Exe names are shared (Classic Era and Anniversary both use <c>WowClassic.exe</c>; Forever and
/// Retail both use <c>Wow.exe</c>), so the folder decides.
/// </summary>
public enum WowGame { Unknown, ClassicEra, Forever, Anniversary, Classic, Retail, Other }

/// <summary>
/// Which processes are WoW game clients. Rules from the launcher: a <c>Wow*.exe</c> name (Classic Era 2026:
/// <c>WowClassic.exe</c>, Forever 2026: <c>Wow.exe</c>), or any game exe inside a "World of Warcraft" folder.
/// Our own tools (<c>WoWBoxer*</c>) and helper processes never count.
/// </summary>
public static class GameDetection
{
    public static bool IsWowProcessName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.ToLowerInvariant();
        return name.StartsWith("wow", StringComparison.Ordinal) && !name.StartsWith("wowboxer", StringComparison.Ordinal)   // our own tools
            && !name.Contains("voice") && !name.Contains("error") && !name.Contains("crash");
    }

    public static bool IsWowPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        path = path.ToLowerInvariant();
        if (!path.Contains(@"\world of warcraft\")) return false;
        var file = path[(path.LastIndexOf('\\') + 1)..];
        string[] notGame = ["launcher", "battle.net", "agent", "error", "crash", "voice"];
        return !notGame.Any(file.Contains);
    }

    /// <summary>The game of a client exe, from its install folder (<c>_classic_era_</c>, <c>_forever_</c> …).</summary>
    public static WowGame GameOf(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath)) return WowGame.Unknown;
        var folder = Path.GetFileName(Path.GetDirectoryName(exePath.TrimEnd('\\', '/')) ?? "").ToLowerInvariant();
        return folder switch
        {
            "_classic_era_" => WowGame.ClassicEra,
            "_forever_" => WowGame.Forever,
            "_anniversary_" => WowGame.Anniversary,
            "_classic_" => WowGame.Classic,
            "_retail_" => WowGame.Retail,
            "" => WowGame.Unknown,
            _ when folder.StartsWith('_') && folder.EndsWith('_') => WowGame.Other,   // _ptr_, _beta_, _classic_beta_ …
            _ => WowGame.Unknown,
        };
    }

    /// <summary>The install folder and exe of a game, relative to the "World of Warcraft" folder.</summary>
    public static (string Folder, string Exe)? InstallOf(WowGame game) => game switch
    {
        WowGame.ClassicEra => ("_classic_era_", "WowClassic.exe"),
        WowGame.Forever => ("_forever_", "Wow.exe"),
        WowGame.Anniversary => ("_anniversary_", "WowClassic.exe"),
        WowGame.Classic => ("_classic_", "WowClassic.exe"),
        WowGame.Retail => ("_retail_", "Wow.exe"),
        _ => null,
    };

    public static string DisplayName(WowGame game) => game switch
    {
        WowGame.ClassicEra => "Classic Era",
        WowGame.Forever => "Forever",
        WowGame.Anniversary => "Classic Anniversary",
        WowGame.Classic => "Classic",
        WowGame.Retail => "Retail",
        WowGame.Other => "another WoW version",
        _ => "unknown WoW version",
    };
}
