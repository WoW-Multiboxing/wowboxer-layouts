using System.Globalization;

namespace WoWBoxer.Layouts.Core;

/// <summary>Which window edge (horizontally) a bar follows.</summary>
public enum BarAnchorH { Left, Center, Right }

/// <summary>Which window edge (vertically) a bar follows.</summary>
public enum BarAnchorV { Bottom, Middle, Top }

/// <summary>
/// One action bar as a box in UI units, relative to the window point it follows (e.g. bottom centre). A smaller UI Scale
/// makes the window larger in UI units (768 / scale high); the box keeps its size and its offset from that point.
/// <see cref="Margin"/> is the room it wants to the window edges (an edge the bar itself sits closer to, by design, needs
/// only that much).
/// </summary>
public sealed record BarBox(string Key, string Label, BarAnchorH H, BarAnchorV V, double X0, double Y0, double X1, double Y1, double Margin = ActionBars.Margin)
{
    /// <summary>Left, bottom, right, top in a window of <paramref name="w"/> × <paramref name="h"/> UI units (origin bottom-left).</summary>
    public (double L, double B, double R, double T) At(double w, double h)
    {
        var ax = H switch { BarAnchorH.Left => 0, BarAnchorH.Right => w, _ => w / 2 };
        var ay = V switch { BarAnchorV.Bottom => 0, BarAnchorV.Top => h, _ => h / 2 };
        return (ax + X0, ay + Y0, ax + X1, ay + Y1);
    }

    /// <summary>The margins (left, bottom, right, top) this bar needs: <see cref="Margin"/>, or less on an edge it is fixed to.</summary>
    public (double L, double B, double R, double T) Margins()
    {
        double Fixed(double gap) => Math.Clamp(gap, 0, Margin);
        return (H == BarAnchorH.Left ? Fixed(X0) : Margin, V == BarAnchorV.Bottom ? Fixed(Y0) : Margin,
                H == BarAnchorH.Right ? Fixed(-X1) : Margin, V == BarAnchorV.Top ? Fixed(-Y1) : Margin);
    }
}

/// <summary>What happens to the bars in one window at one UI Scale.</summary>
public sealed record BarCheck(IReadOnlyList<BarBox> Outside, IReadOnlyList<BarBox> Covered)
{
    public bool Fits => Outside.Count == 0 && Covered.Count == 0;
}

/// <summary>
/// Auto-fit for every enabled action bar (owner bug 2026-11-06: with bars 2 and 3 on, buttons are missing in the small
/// windows). Works in WoW's UI units: the UI is 768 / scale units high and aspect × that wide. A UI Scale fits a window
/// when every enabled bar is inside it (with a small margin) and no bar lies on another. The bar positions come from the
/// WoWBoxer AddOn's measurement (<c>bridge.bars</c>, wowboxer-addon <c>docs/bridge.md</c>) when there is one, otherwise
/// from each game's default layout below.
/// </summary>
public static class ActionBars
{
    /// <summary>Room (UI units) a bar wants to the window edges.</summary>
    public const double Margin = 4;

    /// <summary>Two boxes whose bottoms are this close (UI units) are in the same row.</summary>
    public const double SameRow = 10;

    /// <summary>The UI is this many units high at UI Scale 1.</summary>
    public const double UiHeight = 768;

    /// <summary>Bit of <c>enableMultiActionBars</c> (character's config-cache.wtf) for each extra bar. Bar 1 is always on.</summary>
    public static readonly IReadOnlyList<(string Key, int Bit, string Label)> Extra =
    [
        ("bottomLeft", 1, "Bottom Left bar (bar 2)"),
        ("bottomRight", 2, "Bottom Right bar (bar 3)"),
        ("right", 4, "Right bar"),
        ("left", 8, "Right bar 2"),
        ("bar5", 16, "Action Bar 5"),
        ("bar6", 32, "Action Bar 6"),
        ("bar7", 64, "Action Bar 7"),
    ];

    /// <summary>A readable name for a bar key.</summary>
    public static string LabelOf(string key) => key switch
    {
        "main" => "Action Bar 1",
        "menu" => "Bag and menu buttons",
        _ => Extra.FirstOrDefault(e => e.Key == key).Label ?? key,
    };

    /// <summary>
    /// Classic Era's classic layout (FrameXML; the 1024-wide main bar is field-verified, docs/wow-display-findings.md):
    /// the main bar with its gryphons, bag and menu buttons at the bottom centre, bars 2 and 3 side by side above it
    /// (inside the same 1024 units), the right bars at the bottom right, 98 up. The main bar keeps the verified rule
    /// (whole bar at 4:3, no margin: the gryphon ends are art).
    /// </summary>
    public static readonly IReadOnlyList<BarBox> EraDefault =
    [
        new("main", "Action Bar 1", BarAnchorH.Center, BarAnchorV.Bottom, -512, 0, 512, 53, Margin: 0),
        new("bottomLeft", "Bottom Left bar (bar 2)", BarAnchorH.Center, BarAnchorV.Bottom, -504, 53, -6, 89),
        new("bottomRight", "Bottom Right bar (bar 3)", BarAnchorH.Center, BarAnchorV.Bottom, 6, 53, 504, 89),
        new("right", "Right bar", BarAnchorH.Right, BarAnchorV.Bottom, -42, 98, -6, 596),
        new("left", "Right bar 2", BarAnchorH.Right, BarAnchorV.Bottom, -83, 98, -47, 596),
    ];

    /// <summary>
    /// Forever's default, measured by the WoWBoxer AddOn on the owner's client (2026-11-06, bars 1–3, 45-unit buttons)
    /// and checked on <c>wbx snap</c> pictures (2026-11-09): the classic main bar art with bar 1 left of centre, the menu
    /// and bag buttons right of it, bars 2 and 3 side by side above. In a too narrow window the game pushes the whole
    /// group against the window edges: in the owner's 870×696 followers (UI Scale 1, live addon log 2026-11-09) bar 1 sat
    /// at the left edge and bar 3 at the right edge, on top of 4 buttons of bar 2, and the bags ran off the right edge.
    /// Modelled centred, which needs a few units more than the group's width: a safe side.
    /// The right bars are not measured yet [AFKLAR: Forever right bars' default place]: modelled at the middle right.
    /// </summary>
    public static readonly IReadOnlyList<BarBox> ForeverDefault =
    [
        new("main", "Action Bar 1", BarAnchorH.Center, BarAnchorV.Bottom, -601, 2, -39, 47),
        new("menu", "Bag and menu buttons", BarAnchorH.Center, BarAnchorV.Bottom, -30, 2, 587, 47),
        new("bottomLeft", "Bottom Left bar (bar 2)", BarAnchorH.Center, BarAnchorV.Bottom, -579, 72, -17, 117),
        new("bottomRight", "Bottom Right bar (bar 3)", BarAnchorH.Center, BarAnchorV.Bottom, 5, 72, 567, 117),
        new("right", "Right bar", BarAnchorH.Right, BarAnchorV.Middle, -50, -358, -5, 204),
        new("left", "Right bar 2", BarAnchorH.Right, BarAnchorV.Middle, -97, -358, -52, 204),
    ];

    /// <summary>The games Auto-fit knows the bars of.</summary>
    public static bool Knows(WowGame game) => game is WowGame.ClassicEra or WowGame.Forever;

    public static IReadOnlyList<BarBox> DefaultLayout(WowGame game) => game == WowGame.Forever ? ForeverDefault : EraDefault;

    /// <summary>Bar keys switched on by <c>enableMultiActionBars</c> (bar 1 and the bag and menu buttons always).</summary>
    public static IReadOnlyList<string> EnabledKeys(int mask) =>
        [.. Always, .. Extra.Where(e => (mask & e.Bit) != 0).Select(e => e.Key)];

    /// <summary>Always on: bar 1, and the bag and menu buttons (on Classic Era part of the 1024-unit main bar).</summary>
    public static readonly IReadOnlyList<string> Always = ["main", "menu"];

    /// <summary><c>enableMultiActionBars</c> from a character's config-cache.wtf text, or null when it is not there.</summary>
    public static int? ReadMask(string configCacheText) =>
        WowConfig.Parse(configCacheText).TryGetValue("enableMultiActionBars", out var v) && int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : null;

    /// <summary>
    /// The boxes of the enabled bars: measured ones first (the addon), else the game's default layout. On Classic Era the
    /// main bar is always the 1024-unit art with the bag and menu buttons on it (field-verified; the addon measures the
    /// 12 action buttons and the menu buttons separately).
    /// Bars the game has no default for and nobody measured are left out.
    /// </summary>
    public static IReadOnlyList<BarBox> Layout(WowGame game, IEnumerable<string> enabled, IReadOnlyList<BarBox>? measured = null)
    {
        var defaults = DefaultLayout(game);
        var list = new List<BarBox>();
        foreach (var key in enabled.Distinct())
        {
            if (game == WowGame.ClassicEra && key == "menu") continue;   // inside the main bar art
            var box = game == WowGame.ClassicEra && key == "main" ? null : measured?.FirstOrDefault(b => b.Key == key);
            box ??= defaults.FirstOrDefault(b => b.Key == key);
            if (box is not null) list.Add(box);
        }
        return list;
    }

    /// <summary>Which bars are cut (a button outside the window) or covered (lying on another bar) at this scale.</summary>
    public static BarCheck Check(IReadOnlyList<BarBox> bars, double gameAspect, double scale)
    {
        var h = UiHeight / scale;
        var w = gameAspect * h;
        var rects = bars.Select(b => b.At(w, h)).ToList();
        var outside = new List<BarBox>();
        var covered = new HashSet<int>();
        for (var i = 0; i < bars.Count; i++)
        {
            var (l, b, r, t) = rects[i];
            var (ml, mb, mr, mt) = bars[i].Margins();
            var e = bars[i].Margin > 0 ? 0.5 : 0;   // rounding; none for the field-verified main bar rule
            if (l < ml - e || b < mb - e || r > w - mr + e || t > h - mt + e) outside.Add(bars[i]);
            for (var j = i + 1; j < bars.Count; j++)
            {
                var o = rects[j];
                // the menu and bag buttons' tall frames touch the bars above by design; they count only against a bar in
                // their own row (bar 1 next to them: the game pushes them onto it in a narrow window, owner 2026-11-10)
                if ((bars[i].Key == "menu" || bars[j].Key == "menu") && Math.Abs(b - o.B) > SameRow) continue;
                const double touch = 1;   // touching edges do not count
                if (l < o.R - touch && o.L < r - touch && b < o.T - touch && o.B < t - touch)
                {
                    covered.Add(i);
                    covered.Add(j);
                }
            }
        }
        return new BarCheck(outside, covered.Order().Select(i => bars[i]).ToList());
    }

    public static bool Fits(IReadOnlyList<BarBox> bars, double gameAspect, double scale) => gameAspect > 0 && Check(bars, gameAspect, scale).Fits;

    /// <summary>The largest UI Scale (2 decimals, WoW's range 0.64–1.00) at which every bar fits this game area; null = not even 0.64.</summary>
    public static double? FitScale(IReadOnlyList<BarBox> bars, double gameAspect)
    {
        if (gameAspect <= 0) return null;
        for (var i = (int)Math.Round(UiScale.Max * 100); i >= (int)Math.Round(UiScale.Min * 100); i--)
            if (Fits(bars, gameAspect, i / 100.0)) return i / 100.0;
        return null;
    }

    /// <summary>
    /// The UI scale a game really draws at. With "Use UI Scale" on it is that value. Off: Classic Era keeps the UI 768
    /// units high (field-verified); Forever draws about 1 unit per pixel, never below 0.64 (the addon measured 0.64 in
    /// 1353- and 1369-pixel-high windows without a uiScale line) [AFKLAR: confirm on a small Forever window].
    /// </summary>
    public static double DrawnScale(WowGame game, UiScaleSetting setting, int gameHeight) =>
        setting.Use && setting.Scale > 0 ? setting.Scale
        : game == WowGame.Forever && gameHeight > 0 ? Math.Clamp(UiHeight / gameHeight, UiScale.Min, UiScale.Max)
        : 1;

    /// <summary>Plain words for the bars missing in one window ("" when everything fits).</summary>
    public static string Describe(BarCheck check)
    {
        // the game may push a bar that does not fit onto another one instead of cutting it: "missing" covers both
        var names = check.Outside.Concat(check.Covered).Select(b => b.Label).Distinct().ToList();
        return names.Count == 0 ? "" : $"buttons missing: {string.Join(", ", names)}";
    }
}
