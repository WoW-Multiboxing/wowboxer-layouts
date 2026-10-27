using System.Windows.Media;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// How much of Classic's main action bar a window shows (docs/wow-display-findings.md): the bar is 1024 UI units wide
/// and the UI 768 high, so it is complete at 4:3; down to about 1.2 only the gryphon ends are clipped.
/// </summary>
public static class ActionBar
{
    public static readonly Color Full = Color.FromRgb(0x8C, 0xCB, 0x7A), Gryphons = Color.FromRgb(0xE6, 0xB4, 0x55), Clipped = Color.FromRgb(0xE3, 0x7A, 0x7E);

    public static Color ColorFor(double gameAspect) => gameAspect >= 4.0 / 3 - 0.001 ? Full : gameAspect >= 1.2 ? Gryphons : Clipped;

    public static Brush Brush(double gameAspect) => new SolidColorBrush(ColorFor(gameAspect));

    public static string Verdict(double gameAspect) => gameAspect >= 4.0 / 3 - 0.001 ? "Full action bar" : gameAspect >= 1.2 ? "Gryphon ends clipped" : "Buttons clipped";

    /// <summary>
    /// Largest UI Scale at which the 1024-unit bar fits: the UI is 768/scale units high, so its width is
    /// 768 x aspect / scale >= 1024, i.e. scale <= 0.75 x aspect (rounded down to 0.05). Derived, not yet verified in game.
    /// </summary>
    public static double MaxUiScale(double gameAspect) => Math.Floor(Math.Min(1.0, 0.75 * gameAspect) * 20) / 20;

    public static string Short(double gameAspect) => gameAspect >= 4.0 / 3 - 0.001 ? "Full bar" : gameAspect >= 1.2 ? "Gryphons clipped" : "Bar clipped";
}
