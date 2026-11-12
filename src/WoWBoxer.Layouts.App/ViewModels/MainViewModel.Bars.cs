using System.IO;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App.ViewModels;

/// <summary>What one window shows of its action bars: a short canvas label and its colour.</summary>
public sealed record BarMark(string Short, System.Windows.Media.Color Color);

/// <summary>
/// Everything needed to judge a slot's bars at any size (the canvas uses it while a window is dragged). <paramref name="Addon"/>:
/// per slot, the WoWBoxer AddOn's own bar fit – its real result wins over the estimate.
/// </summary>
public sealed record BarFitContext(WowGame Game, UiScaleSetting Setting, IReadOnlyDictionary<int, IReadOnlyList<IReadOnlyList<BarBox>>> Slots,
    IReadOnlyDictionary<int, BridgeFit?>? Addon = null)
{
    public const string AddonFitsShort = "AddOn fits bars", NeedsAddonShort = "Needs the AddOn";

    /// <summary>The addon's result for this window: true = it fits every bar, false = it found no way, null = no result (use the estimate).</summary>
    public bool? AddonVerdict(int index, int w, int h) => Addon?.GetValueOrDefault(index)?.FitsIn(w, h);

    /// <summary>True when the window's bars are cut even at <see cref="UiScale.SaneMin"/> and no addon handles them: only the addon can fix it.</summary>
    public bool NeedsAddon(int index, int w, int h)
    {
        if (w <= 0 || h <= 0 || Addon?.GetValueOrDefault(index)?.Addon == true || !Slots.TryGetValue(index, out var setups)) return false;
        return setups.Any(bars => UiScalePolicy.NeedsAddon(ActionBars.FitScale(bars, (double)w / h)));
    }

    public BarCheck Check(int index, int w, int h)
    {
        if (w <= 0 || h <= 0 || !Slots.TryGetValue(index, out var setups)) return new BarCheck([], []);
        var scale = ActionBars.DrawnScale(Game, Setting, h);
        var outside = new List<BarBox>();
        var covered = new List<BarBox>();
        foreach (var bars in setups)
        {
            var c = ActionBars.Check(bars, (double)w / h, scale);
            outside.AddRange(c.Outside.Where(b => outside.All(o => o.Key != b.Key)));
            covered.AddRange(c.Covered.Where(b => covered.All(o => o.Key != b.Key)));
        }
        return new BarCheck(outside, covered);
    }

    public BarMark Mark(int index, int w, int h)
    {
        // the addon's real result when there is one (it lays the bars out in that very window)
        if (AddonVerdict(index, w, h) is { } addon) return addon ? new BarMark(AddonFitsShort, ActionBar.Full) : new BarMark("Bars cut", ActionBar.Clipped);
        var check = Check(index, w, h);
        if (check.Fits) return new BarMark("All bars fit", ActionBar.Full);
        if (NeedsAddon(index, w, h)) return new BarMark(NeedsAddonShort, ActionBar.Clipped);
        // Classic Era: below 4:3 only the gryphon ends go first (field tests)
        if (Game == WowGame.ClassicEra && check.Covered.Count == 0 && check.Outside.All(b => b.Key == "main")
            && h > 0 && (double)w / h / ActionBars.DrawnScale(Game, Setting, h) >= UiScale.GryphonsOnly)
            return new BarMark("Gryphons clipped", ActionBar.Gryphons);
        return new BarMark("Bars cut", ActionBar.Clipped);
    }
}

/// <summary>
/// Auto-fit for every enabled action bar (owner bug 2026-11-06): which bars each slot's account has on, where they are
/// (the WoWBoxer AddOn's measurement or the game's default layout), and the one UI Scale at which all of them fit in
/// every window of the layout. Read only; the scale is written by <see cref="UiScaleFix"/> as before.
/// </summary>
public sealed partial class MainViewModel
{
    private IReadOnlyDictionary<string, AccountBars> _accountBars = new Dictionary<string, AccountBars>();

    /// <summary>Re-reads which bars each account has on (WoW writes these files at logout and /reload).</summary>
    private void ReadBarSetup()
    {
        try
        {
            _accountBars = SquadExe is { } exe && ActionBars.Knows(SquadGame) && GameInstall.FolderOf(exe) is { } folder
                ? BarSetup.Read(folder, SquadGame, WowAccounts)
                : new Dictionary<string, AccountBars>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _accountBars = new Dictionary<string, AccountBars>();
        }
        OnPropertyChanged(nameof(BarFit));
    }

    /// <summary>
    /// The bar setups that can be in slot <paramref name="index"/>: its account's, or – no account chosen – every
    /// account's (Auto-fit then makes room for the largest). Bar 1 alone when nothing is known.
    /// </summary>
    private IReadOnlyList<IReadOnlyList<BarBox>> BarsForSlot(int index)
    {
        var account = index < _settings.Accounts.Count ? _settings.Accounts[index] : "";
        if (account.Length > 0 && _accountBars.TryGetValue(account, out var mine)) return [mine.Bars];
        if (_accountBars.Count > 0) return _accountBars.Values.Select(a => a.Bars).ToList();
        return [ActionBars.Layout(SquadGame, ActionBars.Always)];
    }

    /// <summary>The game area (pixels) a slot of the layout gives (title bar excluded).</summary>
    private (int W, int H) PlannedArea(int index)
    {
        if (Current is null || Monitors.Count == 0 || index < 0 || index >= Current.Slots.Count) return (0, 0);
        var s = Current.Slots[index];
        var monitor = Core.Monitors.Find(Monitors, s.Monitor) ?? Core.Monitors.Primary(Monitors);
        var frame = LayoutEngine.FrameSize(Current.Options, monitor.Dpi);
        return LayoutEngine.GameArea(LayoutEngine.Fit(SlotEditing.Raw(s, monitor, Current.Options), s.Fit ?? Current.Options.Fit, s.Anchor, Current.Options.MinAspect, frame), frame);
    }

    /// <summary>The game area of a slot: measured on the open window, else planned.</summary>
    private (int W, int H) AreaOf(int index) => LiveSlots.TryGetValue(index, out var m) ? (m.Width, m.Height) : PlannedArea(index);

    /// <summary>The largest UI Scale at which every bar of the slot fits its planned game area; null = not even 0.64.</summary>
    private double? SlotFitScale(int index)
    {
        var (w, h) = PlannedArea(index);
        if (w <= 0 || h <= 0) return UiScale.Max;
        double? need = UiScale.Max;
        foreach (var bars in BarsForSlot(index))
            need = ActionBars.FitScale(bars, (double)w / h) is { } fit && need is { } n ? Math.Min(n, fit) : null;
        return need;
    }

    /// <summary>What the slot's window shows with this UI Scale setting (empty check = everything fits).</summary>
    private BarCheck SlotCheck(int index, UiScaleSetting setting, bool live = true)
    {
        var (w, h) = live ? AreaOf(index) : PlannedArea(index);
        return new BarFitContext(SquadGame, setting, new Dictionary<int, IReadOnlyList<IReadOnlyList<BarBox>>> { [index] = BarsForSlot(index) }).Check(index, w, h);
    }

    /// <summary>
    /// The WoWBoxer AddOn's own bar fit for slot <paramref name="index"/>: its account's; with no account chosen, a
    /// stand-in only when every account's addon fits its bars (any of them can be in that window).
    /// </summary>
    private BridgeFit? AddonFor(int index)
    {
        var account = index < _settings.Accounts.Count ? _settings.Accounts[index] : "";
        if (account.Length > 0) return _accountBars.TryGetValue(account, out var mine) ? mine.AddonFit : null;
        return _accountBars.Count > 0 && _accountBars.Values.All(a => a.AddonFits) ? new BridgeFit(true, null, null) : null;
    }

    private bool AddonFits(int index) => AddonFor(index)?.Addon == true;

    /// <summary>
    /// The one UI Scale for the windows whose bars the addon does not fit (the value is shared by every client of an
    /// install; never below <see cref="UiScale.SaneMin"/>), the windows that would need less (only the addon can fit
    /// them) and how many windows the addon handles.
    /// </summary>
    private (double Need, IReadOnlyList<int> NeedAddon, int AddonSlots) SquadFit()
    {
        var need = UiScale.Max;
        var needAddon = new List<int>();
        var addon = 0;
        for (var i = 0; i < (Current?.Slots.Count ?? 0); i++)
        {
            if (AddonFits(i))
            {
                addon++;
                continue;
            }
            var fit = SlotFitScale(i);
            if (UiScalePolicy.NeedsAddon(fit)) needAddon.Add(i + 1);
            need = Math.Min(need, fit ?? UiScale.Min);
        }
        return (Math.Max(need, UiScale.SaneMin), needAddon, addon);
    }

    /// <summary>True when every window of the layout shows every enabled bar with this setting (the addon's windows count as fitting).</summary>
    private bool AllFitAt(UiScaleSetting setting) =>
        Enumerable.Range(0, Current?.Slots.Count ?? 0).All(i => AddonFits(i) || SlotCheck(i, setting, live: false).Fits);

    public const string InstallAddonText = "Install the WoWBoxer AddOn – it fits the bars in each window.";

    /// <summary>Plain words when one shared UI Scale can't fit every bar in some windows ("" otherwise): only the addon can.</summary>
    private string NeedAddonText(IReadOnlyList<int> windows)
    {
        if (windows.Count == 0) return "";
        var which = windows.Count == 1 ? $"window {windows[0]}" : $"windows {string.Join(", ", windows)}";
        // the addon is there but its fit is off (or a bar addon owns the bars)
        var off = windows.All(n => AddonFor(n - 1) is { Addon: false });
        return $"One shared UI Scale can't fit all your action bars in {which}. "
            + (off ? "Turn on Setup › Fit action bars in the WoWBoxer AddOn – it fits the bars in each window." : InstallAddonText);
    }

    /// <summary>The bars of every slot, for the canvas (null: a game Auto-fit does not know; the canvas keeps the 4:3 rule).</summary>
    public BarFitContext? BarFit => Current is null || !ActionBars.Knows(SquadGame)
        ? null
        : new BarFitContext(SquadGame, UiScaleNow, Enumerable.Range(0, Current.Slots.Count).ToDictionary(i => i, BarsForSlot),
            Enumerable.Range(0, Current.Slots.Count).ToDictionary(i => i, AddonFor));

    private BarMark MarkFor(int index)
    {
        var (w, h) = AreaOf(index);
        return (BarFit ?? new BarFitContext(SquadGame, UiScaleNow, new Dictionary<int, IReadOnlyList<IReadOnlyList<BarBox>>>())).Mark(index, w, h);
    }

    /// <summary>The selected slot's bars in words.</summary>
    private string SlotBarsVerdict()
    {
        var check = SlotCheck(SelectedSlotIndex, UiScaleNow);
        var names = BarsForSlot(SelectedSlotIndex).Count == 1 && _settings.Accounts.ElementAtOrDefault(SelectedSlotIndex) is { Length: > 0 } acc && _accountBars.TryGetValue(acc, out var a)
            ? $" (bars {a.Names}; {a.Source})" : "";
        var (w, h) = AreaOf(SelectedSlotIndex);
        if (AddonFor(SelectedSlotIndex) is { Addon: true } addon)
            return addon.FitsIn(w, h) == false
                ? "The WoWBoxer AddOn found no way to fit every bar in this window" + names
                : $"The WoWBoxer AddOn fits the bars in this window{(addon.Mode is "scale" or "stack" ? $" ({(addon.Mode == "scale" ? "scaled" : "stacked")} there last time)" : "")}" + names;
        if (check.Fits) return "All action bars fit" + names;
        var mark = MarkFor(SelectedSlotIndex);
        return (mark.Color == ActionBar.Gryphons ? "Gryphon ends clipped, every button visible" : ActionBars.Describe(check)) + names;
    }

    /// <summary>
    /// The aspect a window needs for every bar at the scale it will draw at. A shorter Forever window without "Use UI
    /// Scale" draws its UI larger, so this settles in a few rounds.
    /// </summary>
    private double NeededAspect(int index, int width, int height)
    {
        var aspect = 4.0 / 3;
        for (var round = 0; round < 6; round++)
        {
            var scale = ActionBars.DrawnScale(SquadGame, UiScaleNow, height);
            var needed = BarsForSlot(index).Select(b => MinAspect(b, scale)).DefaultIfEmpty(4.0 / 3).Max();
            if (Math.Abs(needed - aspect) < 0.001) break;
            aspect = needed;
            height = (int)Math.Floor(width / aspect);
        }
        return aspect;
    }

    /// <summary>The narrowest game-area aspect at which these bars fit at this scale (to 0.001).</summary>
    private static double MinAspect(IReadOnlyList<BarBox> bars, double scale)
    {
        double lo = 0.5, hi = 4;
        if (!ActionBars.Fits(bars, hi, scale)) return hi;
        while (hi - lo > 0.0005)
        {
            var mid = (lo + hi) / 2;
            if (ActionBars.Fits(bars, mid, scale)) hi = mid;
            else lo = mid;
        }
        return Math.Ceiling(hi * 1000) / 1000;
    }
}
