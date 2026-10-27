namespace WoWBoxer.Layouts.Core;

/// <summary>
/// Which processes are WoW game clients. Rules from the launcher: a <c>Wow*.exe</c> name (Classic Era 2026:
/// <c>WowClassic.exe</c>), or any game exe inside a "World of Warcraft" folder (fallback for an unknown exe name,
/// e.g. Forever). Our own tools (<c>WoWBoxer*</c>) and helper processes never count.
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
}
