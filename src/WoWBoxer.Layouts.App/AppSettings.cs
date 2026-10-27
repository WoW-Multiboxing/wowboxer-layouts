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
    public bool Forever { get; set; }
    public bool RenameWindows { get; set; } = true;
    /// <summary>Look for a new version on GitHub at start (installed copies only).</summary>
    public bool CheckForUpdates { get; set; } = true;
    /// <summary>Move running WoW windows right away when the layout changes.</summary>
    public bool LiveApply { get; set; } = true;
    public bool Animations { get; set; } = true;
    public string LastTab { get; set; } = "Setup";
    public string? BattleNetExe { get; set; }
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
