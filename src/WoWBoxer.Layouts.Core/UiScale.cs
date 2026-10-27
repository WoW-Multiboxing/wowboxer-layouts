using System.Globalization;
using System.Text.RegularExpressions;

namespace WoWBoxer.Layouts.Core;

/// <summary>WoW's "Use UI Scale" and "UI Scale" options as stored in <c>WTF\Config.wtf</c>.</summary>
public sealed record UiScaleSetting(bool Use, double Scale)
{
    public static readonly UiScaleSetting Automatic = new(false, 1);

    /// <summary>The scale the UI is drawn at: <see cref="Scale"/> when "Use UI Scale" is on, otherwise 1 (768 units high).</summary>
    public double Effective => Use && Scale > 0 ? Scale : 1;

    public string Display => Use ? Scale.ToString("0.00", CultureInfo.InvariantCulture) : "automatic";
}

/// <summary>
/// UI Scale and the Classic main action bar (docs/wow-display-findings.md). The UI is 768 units high at scale 1 and
/// 768 / scale units high otherwise; the main bar with its gryphons is 1024 units wide. So the whole bar shows when
/// game aspect / scale ≥ 4:3. Field test 2026-10-27 (Forever 1.60, same engine as Classic Era 2026): WoW keeps
/// <c>useUiScale</c> and <c>uiScale</c> in the shared Config.wtf and rewrites them on exit, so one value applies to
/// every client of an install, and the file may only be written while no client of that install runs.
/// </summary>
public static partial class UiScale
{
    /// <summary>WoW's slider range.</summary>
    public const double Min = 0.64, Max = 1.0;

    /// <summary>Below this effective aspect buttons are cut too, not only the gryphon ends.</summary>
    public const double GryphonsOnly = 1.3;

    public static UiScaleSetting Read(string configText)
    {
        var values = WowConfig.Parse(configText);
        var use = values.TryGetValue("useUiScale", out var u) && u.Trim() == "1";
        var scale = values.TryGetValue("uiScale", out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : 1;
        return new UiScaleSetting(use, scale);
    }

    public static UiScaleSetting ReadFile(string configPath)
    {
        try
        {
            return File.Exists(configPath) ? Read(File.ReadAllText(configPath)) : UiScaleSetting.Automatic;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return UiScaleSetting.Automatic;
        }
    }

    /// <summary>What the action bar "sees": the game aspect divided by the UI scale. ≥ 4:3 = whole bar.</summary>
    public static double EffectiveAspect(double gameAspect, UiScaleSetting setting) => gameAspect / setting.Effective;

    /// <summary>The largest UI Scale (2 decimals, within WoW's range) at which the whole bar fits a game area of this aspect.</summary>
    public static double ForFullBar(double gameAspect) =>
        Math.Clamp(Math.Floor(Math.Min(Max, 0.75 * gameAspect) * 100 + 1e-9) / 100, Min, Max);

    /// <summary>The one UI Scale for a whole squad (it is shared): the smallest any window needs, or null if all show the bar at 1.</summary>
    public static double? ForSquad(IEnumerable<double> gameAspects)
    {
        var need = gameAspects.Where(a => a > 0).Select(ForFullBar).DefaultIfEmpty(Max).Min();
        return need >= Max ? null : need;
    }

    /// <summary>What a window shows of the bar at this effective aspect (field tests: 1.31 loses only the gryphons; 1.25 already cuts the bag buttons).</summary>
    public static string BarVerdict(double effectiveAspect) =>
        effectiveAspect >= 4.0 / 3 - 0.001 ? "Full action bar" : effectiveAspect >= GryphonsOnly ? "Gryphon ends clipped" : "Buttons clipped";

    /// <summary>The config text with "Use UI Scale" on (or off) and the given scale. Other lines and the line endings stay.</summary>
    public static string WithScale(string configText, bool use, double scale)
    {
        var newline = configText.Contains("\r\n") ? "\r\n" : "\n";
        var text = SetValue(configText, "useUiScale", use ? "1" : "0", newline);
        return SetValue(text, "uiScale", scale.ToString("0.00", CultureInfo.InvariantCulture), newline);
    }

    private static string SetValue(string text, string name, string value, string newline)
    {
        var line = $"SET {name} \"{value}\"";
        var regex = new Regex($"""^[ \t]*SET[ \t]+{name}[ \t]+"[^"]*"(?=[ \t]*\r?$)""", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (regex.IsMatch(text)) return regex.Replace(text, line, 1);
        if (text.Length > 0 && !text.EndsWith('\n')) text += newline;
        return text + line + newline;
    }
}
