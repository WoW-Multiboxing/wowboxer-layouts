namespace WoWBoxer.Layouts.Core;

/// <summary>A saved profile and the file it lives in.</summary>
public sealed record StoredProfile(string Path, LayoutProfile Profile);

/// <summary>Profiles as <c>*.json</c> files in one folder (the app uses %APPDATA%\WoWBoxer\Layouts\profiles).</summary>
public sealed class ProfileStore(string folder)
{
    public string Folder { get; } = folder;

    /// <summary>All valid profiles, sorted by name. Broken files are reported in <paramref name="errors"/>, not thrown.</summary>
    public List<StoredProfile> LoadAll(List<string>? errors = null)
    {
        if (!Directory.Exists(Folder)) return [];
        var list = new List<StoredProfile>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                list.Add(new StoredProfile(path, ProfileJson.Deserialize(File.ReadAllText(path))));
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
            {
                errors?.Add($"{System.IO.Path.GetFileName(path)}: {e.Message}");
            }
        }
        return list.OrderBy(p => p.Profile.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Saves the profile under a file named after it. If it was stored under another file before
    /// (it was renamed), that file is removed. Returns the new path.
    /// </summary>
    public string Save(LayoutProfile profile, string? previousPath = null)
    {
        Directory.CreateDirectory(Folder);
        var path = PathFor(profile.Name);
        var temp = path + ".tmp";
        File.WriteAllText(temp, ProfileJson.Serialize(profile));
        File.Move(temp, path, overwrite: true);
        if (previousPath is not null && !string.Equals(System.IO.Path.GetFullPath(previousPath), System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            File.Delete(previousPath);
        return path;
    }

    public void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>"Name", or "Name (2)", "Name (3)" ... so a new profile never overwrites another.</summary>
    public string UniqueName(string name, string? ignorePath = null)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Layout" : name.Trim();
        var candidate = name;
        for (var i = 2; File.Exists(PathFor(candidate)) && !SamePath(PathFor(candidate), ignorePath); i++)
            candidate = $"{name} ({i})";
        return candidate;
    }

    public string PathFor(string name) => System.IO.Path.Combine(Folder, FileNameFor(name));

    public static string FileNameFor(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var safe = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray()).TrimEnd('.', ' ');
        return (safe.Length == 0 ? "Layout" : safe) + ".json";
    }

    private static bool SamePath(string a, string? b) =>
        b is not null && string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
