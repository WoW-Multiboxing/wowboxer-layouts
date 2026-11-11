using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>What one <see cref="TopologyAddon.Sync"/> did: the installs written to, removed from, and any that failed.</summary>
public sealed record TopologyResult(IReadOnlyList<string> Written, IReadOnlyList<string> Removed, IReadOnlyList<string> Failed)
{
    public static TopologyResult None { get; } = new([], [], []);
    public bool Changed => Written.Count + Removed.Count + Failed.Count > 0;
}

/// <summary>
/// Writes the data-only add-on <c>Interface\AddOns\WoWBoxerTopology</c> (two text files, numbers only) into every
/// installed WoW client, so the Team Screen add-on knows where monitor 2 is inside a main window stretched over both
/// monitors. The game reads it at login or /reload. A file write only – nothing is sent to the game. When no layout spans,
/// our own folder is removed again (only a folder whose .toc carries <see cref="Topology.GeneratedMark"/>; anything else
/// with that name is never touched).
/// </summary>
public static class TopologyAddon
{
    private static readonly object Gate = new();
    private static string? _lastKey;
    private static (string Key, DateTime At, IReadOnlyList<string> Installs)? _installs;

    /// <summary>
    /// Brings every install in line with <paramref name="topology"/> (null = no spanning main: remove ours). Does nothing
    /// when the same state was already synced to the same installs (it runs on every placement tick).
    /// </summary>
    /// <param name="installs">Client folders (holding the exe) to include besides those found under the WoW folders.</param>
    public static TopologyResult Sync(Topology? topology, IEnumerable<string>? installs = null, IReadOnlyList<string>? roots = null)
    {
        if (roots is null && Sandbox.Active) return TopologyResult.None;   // demo mode never writes into the real game folders
        var all = CachedInstalls(roots, installs);
        var lua = topology?.LuaText();
        var key = (lua ?? "none") + "|" + string.Join("|", all);
        lock (Gate)
        {
            if (key == _lastKey) return TopologyResult.None;
            var result = Apply(lua, all);
            // a failed install is tried again next time
            _lastKey = result.Failed.Count == 0 ? key : null;
            return result;
        }
    }

    // the folders are listed at most every 30 s (Sync runs every second while the app places windows)
    private static IReadOnlyList<string> CachedInstalls(IReadOnlyList<string>? roots, IEnumerable<string>? extra)
    {
        var extraList = (extra ?? []).ToList();
        var key = string.Join("|", roots ?? []) + "#" + string.Join("|", extraList);
        lock (Gate)
        {
            if (_installs is { } c && c.Key == key && DateTime.UtcNow - c.At < TimeSpan.FromSeconds(30)) return c.Installs;
        }
        var found = Installs(roots ?? GameInstall.WowRoots(), extraList);
        lock (Gate) _installs = (key, DateTime.UtcNow, found);
        return found;
    }

    /// <summary>Forgets what was synced (tests; after the user deleted the folder by hand).</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _lastKey = null;
            _installs = null;
        }
    }

    /// <summary>The game client folders: <c>_classic_era_</c>, <c>_forever_</c> … with a game exe in them, under each WoW folder.</summary>
    public static IReadOnlyList<string> Installs(IEnumerable<string> roots, IEnumerable<string>? extra = null)
    {
        var found = new List<string>();
        foreach (var root in roots)
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.EnumerateDirectories(root, "_*_"))
                    if (Directory.EnumerateFiles(dir, "*.exe").Any(f => GameDetection.IsWowProcessName(Path.GetFileName(f))))
                        found.Add(Path.GetFullPath(dir));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        foreach (var dir in extra ?? [])
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) found.Add(Path.GetFullPath(dir));
        return found.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string FolderIn(string install) => Path.Combine(install, "Interface", "AddOns", Topology.AddOnName);

    private static TopologyResult Apply(string? lua, IReadOnlyList<string> installs)
    {
        List<string> written = [], removed = [], failed = [];
        foreach (var install in installs)
        {
            var folder = FolderIn(install);
            var toc = Path.Combine(folder, Topology.AddOnName + ".toc");
            var data = Path.Combine(folder, Topology.AddOnName + ".lua");
            try
            {
                var exists = Directory.Exists(folder);
                var ours = !exists || IsOurs(toc);
                if (!ours) continue;   // someone else's folder of that name: never touched
                if (lua is null)
                {
                    if (!exists) continue;
                    Directory.Delete(folder, recursive: true);
                    removed.Add(install);
                    continue;
                }
                var tocText = Topology.TocText();
                if (exists && ReadOrNull(toc) == tocText && ReadOrNull(data) == lua) continue;
                Directory.CreateDirectory(folder);
                WriteAtomic(toc, tocText);
                WriteAtomic(data, lua);
                written.Add(install);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed.Add(install);
            }
        }
        return new TopologyResult(written, removed, failed);
    }

    private static bool IsOurs(string toc) => ReadOrNull(toc) is { } text && text.Contains(Topology.GeneratedMark, StringComparison.Ordinal);

    private static string? ReadOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteAtomic(string path, string text)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, text, new System.Text.UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>One line for the status bar / log.</summary>
    public static string Describe(TopologyResult r, Topology? t)
    {
        static string Names(IEnumerable<string> installs) => string.Join(", ", installs.Select(Path.GetFileName));
        var parts = new List<string>();
        if (r.Written.Count > 0 && t is not null)
            parts.Add($"Monitor layout for the Team Screen add-on written to {Names(r.Written)} (window {t.WindowWidth}×{t.WindowHeight}, " +
                      $"monitor 2 at {t.Team.X},{t.Team.Y} {t.Team.Width}×{t.Team.Height}) – the game reads it at login or /reload.");
        if (r.Removed.Count > 0) parts.Add($"Monitor layout for the Team Screen removed from {Names(r.Removed)} (the main window no longer spans both monitors).");
        if (r.Failed.Count > 0) parts.Add($"Could not write the monitor layout add-on in {Names(r.Failed)} (no write access to its Interface\\AddOns folder).");
        return string.Join(" ", parts);
    }
}
