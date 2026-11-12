namespace WoWBoxer.Layouts.Core;

/// <summary>What Auto-fit (and Fix action bar) does with an install's shared UI Scale.</summary>
public enum UiScaleStep
{
    /// <summary>Write nothing.</summary>
    Leave,
    /// <summary>Set "Use UI Scale" and <see cref="UiScalePlan.Scale"/>.</summary>
    Lower,
    /// <summary>Put the player's own UI Scale back (the lines from the backup of before WoWBoxer's first change).</summary>
    Restore,
}

public sealed record UiScalePlan(UiScaleStep Step, double Scale, string Why);

/// <summary>
/// The rules for the one UI Scale an install shares (owner, 2026-11-12, bug-2026-11-12-actionbars: the shared scale
/// cannot fit every layout, never goes below 0.64, and fought the WoWBoxer AddOn, which now fits the bars in each window):
/// <list type="number">
/// <item>A client whose addon reports that it fits the bars itself (<see cref="BridgeFit"/>) is never given a lower UI
/// Scale. When every window of the layout is such a client, the player's own UI Scale comes back (once, while the
/// install's clients are closed).</item>
/// <item>Without the addon the fit stays, but never below <see cref="UiScale.SaneMin"/>; windows that would need less are
/// for the addon.</item>
/// </list>
/// Pure: the caller reads the files, checks that no client runs and writes.
/// </summary>
public static class UiScalePolicy
{
    /// <param name="now">The install's UI Scale now.</param>
    /// <param name="need">The smallest UI Scale the windows without the addon need (<see cref="UiScale.Max"/> = none).</param>
    /// <param name="alreadyFits">Every bar already fits every window at <paramref name="now"/>.</param>
    /// <param name="addonSlots">Windows of the layout whose addon fits their bars itself.</param>
    /// <param name="slots">Windows in the layout.</param>
    /// <param name="restorable">WoWBoxer changed the UI Scale and the player's own one is known (and differs from now).</param>
    public static UiScalePlan Decide(UiScaleSetting now, double need, bool alreadyFits, int addonSlots, int slots, bool restorable)
    {
        if (slots > 0 && addonSlots >= slots)
            return restorable
                ? new UiScalePlan(UiScaleStep.Restore, now.Scale, "the WoWBoxer AddOn fits the bars in every window")
                : new UiScalePlan(UiScaleStep.Leave, now.Scale, "the WoWBoxer AddOn fits the bars in every window");
        if (addonSlots > 0)
            return new UiScalePlan(UiScaleStep.Leave, now.Scale, "the WoWBoxer AddOn fits the bars in some windows, and a lower UI Scale would work against it");
        if (alreadyFits) return new UiScalePlan(UiScaleStep.Leave, now.Scale, "every bar already fits");
        var target = Math.Max(need, UiScale.SaneMin);
        if (now.Use && now.Scale <= target + 0.001) return new UiScalePlan(UiScaleStep.Leave, now.Scale, "the UI Scale is already that small");
        return new UiScalePlan(UiScaleStep.Lower, target, need < UiScale.SaneMin ? "as small as WoWBoxer goes" : "every bar fits");
    }

    /// <summary>True when a window needs less than WoWBoxer sets (only the addon can fit its bars); null need = not even 0.64.</summary>
    public static bool NeedsAddon(double? need) => need is not { } n || n < UiScale.SaneMin - 0.001;
}
