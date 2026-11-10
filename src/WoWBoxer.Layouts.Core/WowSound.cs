namespace WoWBoxer.Layouts.Core;

/// <summary>What an install's <c>WTF\Config.wtf</c> says about sound, for "Sound follows the main window".</summary>
public sealed record WowSoundSetting(bool AllSound, bool InBackground)
{
    /// <summary>The hint to show, or "" when the client will play while another window has the keyboard.</summary>
    public string Hint(SoundFollowMode mode) =>
        !AllSound ? "Sound is off in WoW (Options › Audio › Enable Sound), so there is nothing to follow."
        : InBackground || mode == SoundFollowMode.ActiveWindow ? ""
        : "Turn on Options › Audio › Sound in Background so the main window keeps playing while you hover another window.";
}

/// <summary>Reads (and, only while no client of the install runs, sets) WoW's "Sound in Background".</summary>
public static class WowSound
{
    public const string BackgroundCvar = "Sound_EnableSoundWhenGameIsInBG";
    public const string AllSoundCvar = "Sound_EnableAllSound";

    /// <summary>Missing lines are WoW's defaults: sound on, sound in background off.</summary>
    public static WowSoundSetting Read(string configText)
    {
        var values = WowConfig.Parse(configText);
        return new WowSoundSetting(!IsZero(values.GetValueOrDefault(AllSoundCvar)), IsOne(values.GetValueOrDefault(BackgroundCvar)));
    }

    public static WowSoundSetting ReadFile(string configPath)
    {
        try
        {
            return Read(File.Exists(configPath) ? File.ReadAllText(configPath) : "");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new WowSoundSetting(true, true);   // unknown: no hint rather than a wrong one
        }
    }

    /// <summary>The config text with "Sound in Background" on. Other lines and the line endings stay.</summary>
    public static string WithBackgroundSound(string configText) => WowConfig.WithValue(configText, BackgroundCvar, "1");

    private static bool IsZero(string? v) => v is not null && v.Trim() is "0" or "0.0";
    private static bool IsOne(string? v) => v is not null && v.Trim() == "1";
}
