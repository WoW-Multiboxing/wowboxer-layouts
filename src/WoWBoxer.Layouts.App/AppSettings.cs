using System.IO;
using System.Text.Json;

namespace WoWBoxer.Layouts.App;

/// <summary>Small per-user settings in %APPDATA%\WoWBoxer\Layouts\settings.json. Nothing leaves the PC.</summary>
public sealed class AppSettings
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WoWBoxer", "Layouts");
    public static string ProfilesFolder => Path.Combine(Folder, "profiles");
    private static string FilePath => Path.Combine(Folder, "settings.json");

    public bool FirstRunDone { get; set; }
    public string? LastProfile { get; set; }
    /// <summary>The version that last ran; a newer one shows "What's new" once.</summary>
    public string? LastSeenVersion { get; set; }
    public bool RenameWindows { get; set; } = true;
    /// <summary>Look for a new version on GitHub at start (installed copies only).</summary>
    public bool CheckForUpdates { get; set; } = true;
    public bool Animations { get; set; } = true;
    public string LastTab { get; set; } = "Setup";
    public string? BattleNetExe { get; set; }
    /// <summary>The last used WoW client exe (any install); updated from the open clients, found automatically when empty.</summary>
    public string? GameExe { get; set; }
    /// <summary>Set WoW's UI Scale automatically (while the clients are closed) so the whole action bar fits.</summary>
    public bool AutoFitActionBar { get; set; } = true;
    /// <summary>WoW account folder per slot (index = slot - 1). Local only, never part of a shared layout.</summary>
    public List<string> Accounts { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new() : new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // settings are a convenience; never crash over them
        }
    }
}
