using System.Text.RegularExpressions;

namespace WoWBoxer.Layouts.Core;

/// <summary>
/// Reads WoW's <c>WTF\Config.wtf</c> (lines like <c>SET gxWindowedResolution "870x696"</c>). Read-only:
/// the client rewrites the file on exit, so we never write it while a client runs.
/// </summary>
public static partial class WowConfig
{
    /// <summary>The display cvars that matter for window layouts, with what we know about them.</summary>
    public static readonly IReadOnlyList<(string Name, string Meaning)> DisplayCvars =
    [
        ("gxWindow", "1 = windowed, 0 = fullscreen (missing = client default) [uncertain for 2026 clients]"),
        ("gxMaximize", "1 = Windowed (Fullscreen) / maximised, 0 = plain Windowed"),
        ("gxWindowedResolution", "window size WoW restores in Windowed mode"),
        ("gxFullscreenResolution", "resolution for fullscreen modes"),
        ("gxMonitor", "monitor index the client opens on"),
        ("RenderScale", "3D render scale (1 = 100 %)"),
        ("windowResizeLock", "1 = user cannot resize the window [uncertain, old cvar]"),
        ("useUiScale", "1 = use uiScale instead of automatic (account config)"),
        ("uiScale", "UI scale when useUiScale = 1 (account config)"),
    ];

    public static Dictionary<string, string> Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in SetLine().Matches(text))
            values[m.Groups[1].Value] = m.Groups[2].Value;
        return values;
    }

    /// <summary>"870x696" → (870, 696).</summary>
    public static (int Width, int Height)? ParseSize(string? value)
    {
        var m = value is null ? null : SizeValue().Match(value);
        return m is { Success: true } ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : null;
    }

    /// <summary>Config.wtf for a client exe: <c>&lt;exe folder&gt;\WTF\Config.wtf</c>.</summary>
    public static string ConfigPathFor(string exePath) =>
        Path.Combine(Path.GetDirectoryName(exePath) ?? "", "WTF", "Config.wtf");

    [GeneratedRegex("""^\s*SET\s+(\S+)\s+"([^"]*)"\s*$""", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SetLine();

    [GeneratedRegex(@"^(\d+)\s*x\s*(\d+)$")]
    private static partial Regex SizeValue();
}
