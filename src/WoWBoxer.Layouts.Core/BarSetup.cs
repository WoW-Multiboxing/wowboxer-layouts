namespace WoWBoxer.Layouts.Core;

/// <summary>
/// The WoWBoxer AddOn's action bar measurement (<c>WoWBoxerDB.bridge.bars</c> in the account's
/// <c>SavedVariables\WoWBoxer.lua</c>; contract in wowboxer-addon <c>docs/bridge.md</c>). Read only. WoW writes the file
/// at logout and /reload, so this is the state from then.
/// </summary>
/// <param name="Squeezed">
/// The window was too small when the addon measured: the game had pushed the bars against the window edges (live
/// Forever log 2026-11-09, 870x696: bar 1 at the left edge, bar 3 at the right edge on top of bar 2). Those places
/// are not where the bars belong, so only the list of bars is used and the places come from the game's default
/// layout. (Since 1.2.0-test.4 the addon also writes <c>squeezed</c> and, per bar, how far the game pushed it,
/// <c>dx</c>/<c>dy</c>; those are not used yet [AFKLAR: check on Forever that adding them back gives the main window's places].)
/// </param>
public sealed record BridgeBars(string? Character, string? Game, double UiWidth, double UiHeight, double Scale, IReadOnlyList<BarBox> Bars, DateTime? MeasuredAt, bool Squeezed = false)
{
    /// <summary>Reads <c>bridge.bars</c> from a SavedVariables file text; null when there is none (old addon, never measured).</summary>
    public static BridgeBars? Parse(string savedVariablesText)
    {
        var db = LuaData.ParseFile(savedVariablesText).GetValueOrDefault("WoWBoxerDB");
        var bars = LuaData.Get(db, "bridge", "bars");
        // version 1 only; diag.bars (macro placements) has no "ui" and is not this table
        if (LuaData.Number(bars, "version") is not 1.0 || LuaData.Number(bars, "ui", "w") is not { } w || LuaData.Number(bars, "ui", "h") is not { } h || w <= 0 || h <= 0)
            return null;
        var list = new List<BarBox>();
        foreach (var item in LuaData.Items(LuaData.Get(bars, "bars")))
        {
            if (LuaData.Text(item, "key") is not { Length: > 0 } key) continue;
            if (LuaData.Number(item, "left") is not { } l || LuaData.Number(item, "bottom") is not { } b
                || LuaData.Number(item, "right") is not { } r || LuaData.Number(item, "top") is not { } t || r <= l || t <= b) continue;
            var anchor = (LuaData.Text(item, "anchor") ?? "center/bottom").Split('/');
            var ah = anchor[0] switch { "left" => BarAnchorH.Left, "right" => BarAnchorH.Right, _ => BarAnchorH.Center };
            var av = (anchor.Length > 1 ? anchor[1] : "bottom") switch { "top" => BarAnchorV.Top, "middle" => BarAnchorV.Middle, _ => BarAnchorV.Bottom };
            var ax = ah switch { BarAnchorH.Left => 0, BarAnchorH.Right => w, _ => w / 2 };
            var ay = av switch { BarAnchorV.Bottom => 0, BarAnchorV.Top => h, _ => h / 2 };
            var label = LuaData.Text(item, "label") ?? ActionBars.LabelOf(key);
            list.Add(new BarBox(key, label, ah, av, l - ax, b - ay, r - ax, t - ay));
        }
        if (list.Count == 0) return null;
        DateTime? at = LuaData.Number(bars, "measuredAt") is { } secs and > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)secs).UtcDateTime : null;
        var squeezed = (LuaData.Number(bars, "outside") ?? 0) > 0 || (LuaData.Number(bars, "covered") ?? 0) > 0 || LuaData.Get(bars, "squeezed") is true
            || list.Any(x => x.Key != "menu" && ((x.H != BarAnchorH.Left && x.At(w, h).L <= 0.5) || (x.H != BarAnchorH.Right && x.At(w, h).R >= w - 0.5)));
        return new BridgeBars(LuaData.Text(db, "bridge", "character"), LuaData.Text(bars, "game"), w, h, LuaData.Number(bars, "scale") ?? 1, list, at, squeezed);
    }

    public static BridgeBars? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>
/// The WoWBoxer AddOn's own bar fit (since 1.3.0-test.9, wowboxer-addon <c>docs/bridge.md</c> "The addon fits the bars
/// itself"): <c>bridge.barFit</c> is on while the addon lays out the bars of each window by itself, and then the app
/// leaves UI Scale alone for that client. <c>bridge.barMode</c> says what it did the last time ("none" = fits as it is,
/// "scale", "stack", "nofit" = nothing fits), in the window <c>bridge.bars.window</c> it measured.
/// </summary>
public sealed record BridgeFit(bool Addon, string? Mode, double? Scale, int WindowWidth = 0, int WindowHeight = 0)
{
    /// <summary>Window sizes this close (pixels) count as the window the addon measured.</summary>
    public const int SameWindow = 8;

    /// <summary>Reads <c>bridge.barFit</c>; null when the addon never wrote it (an older addon). Accepts <c>true</c> and <c>"addon"</c>.</summary>
    public static BridgeFit? Parse(string savedVariablesText)
    {
        var bridge = LuaData.Get(LuaData.ParseFile(savedVariablesText).GetValueOrDefault("WoWBoxerDB"), "bridge");
        bool on;
        switch (LuaData.Get(bridge, "barFit"))
        {
            case bool b:
                on = b;
                break;
            case string text:
                on = string.Equals(text, "addon", StringComparison.OrdinalIgnoreCase);
                break;
            default:
                return null;
        }
        var w = LuaData.Number(bridge, "bars", "window", "w") ?? 0;
        var h = LuaData.Number(bridge, "bars", "window", "h") ?? 0;
        return new BridgeFit(on, LuaData.Text(bridge, "barMode"), LuaData.Number(bridge, "barScale"), (int)w, (int)h);
    }

    /// <summary>
    /// What the addon's result says about a window with this game area: true = every bar fits (as it is, scaled or
    /// stacked), false = the addon found no way, null = it does not handle the bars, or "nofit" was for another window
    /// size (the addon lays the bars out again whenever the window changes).
    /// </summary>
    public bool? FitsIn(int width, int height)
    {
        if (!Addon) return null;
        if (Mode != "nofit") return true;
        var same = WindowWidth > 0 && Math.Abs(WindowWidth - width) <= SameWindow && Math.Abs(WindowHeight - height) <= SameWindow;
        return same ? false : null;
    }

    public static BridgeFit? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>The action bars one account's character has on: which ones, where, and where we know it from.</summary>
/// <param name="AddonFit">The WoWBoxer AddOn's own bar fit for this account (null: no addon, or an older one).</param>
public sealed record AccountBars(string Account, string? Character, IReadOnlyList<BarBox> Bars, string Source, BridgeFit? AddonFit = null)
{
    /// <summary>The addon lays out this account's bars in each window itself: the app never lowers UI Scale for it.</summary>
    public bool AddonFits => AddonFit?.Addon == true;

    public string Names => string.Join(", ", Bars.Select(b => b.Key switch
    {
        "main" => "1",
        "menu" => "bags",
        "bottomLeft" => "2",
        "bottomRight" => "3",
        "right" => "right",
        "left" => "right 2",
        _ => b.Key,
    }));
}

/// <summary>
/// Which action bars each account of an install has on, for Auto-fit. Read only, from files WoW writes at logout:
/// the character's <c>config-cache.wtf</c> (<c>enableMultiActionBars</c>, which bars are on, both games) and the
/// WoWBoxer AddOn's measurement (where they really are – Edit Mode on Forever).
/// </summary>
public static class BarSetup
{
    public static IReadOnlyDictionary<string, AccountBars> Read(string installFolder, WowGame game, IEnumerable<WtfAccount> accounts)
    {
        var result = new Dictionary<string, AccountBars>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in accounts)
        {
            var accountDir = Path.Combine(installFolder, "WTF", "Account", account.Name);
            var file = Path.Combine(accountDir, "SavedVariables", "WoWBoxer.lua");
            var bridge = BridgeBars.ReadFile(file);
            // the character the addon saw last, else the one WoW wrote last
            var character = (bridge?.Character is { Length: > 0 } name
                ? account.Characters.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
                    || c.Name.StartsWith(name + "-", StringComparison.OrdinalIgnoreCase))
                : null) ?? account.LastPlayed;
            int? mask = null;
            if (character is not null)
            {
                var cache = Path.Combine(accountDir, character.Realm, character.Name, "config-cache.wtf");
                try
                {
                    // WoW leaves a setting at its default (0: only bar 1) out of the file
                    if (File.Exists(cache)) mask = ActionBars.ReadMask(File.ReadAllText(cache)) ?? 0;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
            result[account.Name] = For(account.Name, character?.Name, game, mask, bridge) with { AddonFit = BridgeFit.ReadFile(file) };
        }
        return result;
    }

    /// <summary>Combines the two sources: the character's switches say which bars are on, the addon says where they are.</summary>
    public static AccountBars For(string account, string? character, WowGame game, int? mask, BridgeBars? bridge)
    {
        var bridgeUsable = bridge is not null && (bridge.Game is null || GameOf(bridge.Game) == game);
        IEnumerable<string> keys = mask is { } m ? ActionBars.EnabledKeys(m)
            : bridgeUsable ? [.. ActionBars.Always, .. bridge!.Bars.Select(b => b.Key)]
            : ActionBars.Always;
        var placesUsable = bridgeUsable && !bridge!.Squeezed;
        var bars = ActionBars.Layout(game, keys, placesUsable ? bridge!.Bars : null);
        var source = placesUsable ? "measured by the WoWBoxer AddOn"
            : bridgeUsable ? "default positions; the AddOn measured them squeezed into a small window"
            : mask is not null ? "the character's settings, default positions" : "bar 1 only (no settings found)";
        return new AccountBars(account, character, bars, source);
    }

    private static WowGame GameOf(string addonGame) => addonGame switch
    {
        "forever" => WowGame.Forever,
        "era" => WowGame.ClassicEra,
        "tbc" => WowGame.Anniversary,
        _ => WowGame.Unknown,
    };
}
