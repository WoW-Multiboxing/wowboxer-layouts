namespace WoWBoxer.Layouts.Core;

/// <summary>
/// Which release feeds an installed copy reads (docs/release-process.md). Stable = Velopack's default Windows channel
/// "win" (plain tags vX.Y.Z, normal GitHub releases). Beta = channel "beta" (tags vX.Y.Z-beta.N, GitHub pre-releases).
/// A beta user also reads the stable feed, so a beta that was promoted to stable (or a newer stable) still arrives.
/// A stable user never reads the beta feed. Switching back from Beta never installs an older version: the next
/// stable that is newer than the installed beta arrives as usual.
/// </summary>
public static class UpdateChannels
{
    /// <summary>Velopack's channel name for stable builds (its default on Windows).</summary>
    public const string StableFeed = "win";
    public const string BetaFeed = "beta";

    public const string Stable = "Stable";
    public const string Beta = "Beta";

    public static IReadOnlyList<string> Choices { get; } = [Stable, Beta];

    /// <summary>
    /// The channel in effect: the user's choice in Settings if any, else the channel the copy was installed from
    /// (a beta Setup installs on "beta", everything else is stable).
    /// </summary>
    public static string Effective(string? setting, string? installedFeed)
    {
        if (string.Equals(setting, Beta, StringComparison.OrdinalIgnoreCase)) return Beta;
        if (string.Equals(setting, Stable, StringComparison.OrdinalIgnoreCase)) return Stable;
        return string.Equals(installedFeed, BetaFeed, StringComparison.OrdinalIgnoreCase) ? Beta : Stable;
    }

    /// <summary>The Velopack channels to read for a channel, in order: Beta reads beta and stable, Stable only stable.</summary>
    public static IReadOnlyList<string> Feeds(string channel) =>
        string.Equals(channel, Beta, StringComparison.OrdinalIgnoreCase) ? [BetaFeed, StableFeed] : [StableFeed];

    /// <summary>GitHub pre-releases are only read for the beta feed (stable releases are never pre-releases).</summary>
    public static bool ReadsPrereleases(string feed) => string.Equals(feed, BetaFeed, StringComparison.OrdinalIgnoreCase);

    /// <summary>A version string from a tag or a build ("0.7.0-beta.1+sha" → "0.7.0-beta.1"): is it a pre-release?</summary>
    public static bool IsPrerelease(string version) => version.Split('+')[0].Contains('-');
}
