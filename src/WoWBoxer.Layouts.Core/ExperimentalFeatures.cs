namespace WoWBoxer.Layouts.Core;

/// <summary>A risky feature that ships switched off (Settings › Experimental) until it has proven itself.</summary>
/// <param name="Id">Stable id kept in settings.json; never reuse an id for a different feature.</param>
public sealed record ExperimentalFeature(string Id, string Title, string Description);

/// <summary>
/// Feature flags (docs/release-process.md). A new risky feature is registered here, checks <see cref="IsOn(IEnumerable{string}?, string)"/>
/// before it does anything, and is off for everyone until the user switches it on – so it can be switched off again
/// without a release. When it has proven itself, the check and the entry are removed (it becomes a normal feature).
/// </summary>
public static class ExperimentalFeatures
{
    /// <summary>Every experimental feature in this build, in the order Settings lists them. Empty = nothing to try.</summary>
    public static IReadOnlyList<ExperimentalFeature> All { get; } =
    [
        new(MainOverMonitors, "Main window over both monitors",
            "For the WoWBoxer Team Screen add-on: the main window can cover monitor 1 and monitor 2. On its own the game then draws its world and UI across both monitors – it needs an add-on that keeps the game on monitor 1."),
    ];

    /// <summary>"Main window over both monitors" (layout switch + the WoWBoxerTopology data add-on).</summary>
    public const string MainOverMonitors = "main-over-monitors";

    public static bool IsOn(IEnumerable<string>? enabled, string id) => IsOn(enabled, id, All);

    /// <summary>On only if the user switched it on and this build still has it (a removed feature is simply off).</summary>
    public static bool IsOn(IEnumerable<string>? enabled, string id, IReadOnlyList<ExperimentalFeature> known) =>
        enabled is not null
        && known.Any(f => string.Equals(f.Id, id, StringComparison.Ordinal))
        && enabled.Contains(id, StringComparer.Ordinal);

    /// <summary>The enabled list after switching <paramref name="id"/> on or off (no duplicates, unknown ids kept so a downgrade/upgrade does not lose them).</summary>
    public static List<string> Set(IEnumerable<string>? enabled, string id, bool on)
    {
        var list = (enabled ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
        list.RemoveAll(x => string.Equals(x, id, StringComparison.Ordinal));
        if (on) list.Add(id);
        return list;
    }
}
