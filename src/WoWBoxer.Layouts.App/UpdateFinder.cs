using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Finds the update for the chosen channel (no WPF, so tools/channel-check can run the very same code against the real
/// GitHub releases with a pretend install). Stable reads only Velopack's "win" feed of normal releases; Beta reads the
/// "beta" feed of pre-releases and the stable feed, and takes the newer. Never offers an older version.
/// </summary>
public static class UpdateFinder
{
    public const string RepoUrl = "https://github.com/WoW-Multiboxing/wowboxer-layouts";

    /// <param name="channel">"Stable" or "Beta" (see <see cref="UpdateChannels.Effective"/>).</param>
    /// <param name="locator">null = this installed copy; a <see cref="TestVelopackLocator"/> for checks without installing.</param>
    public static async Task<(UpdateManager Manager, UpdateInfo Update)?> FindAsync(string channel, IVelopackLocator? locator = null, string repoUrl = RepoUrl)
    {
        (UpdateManager, UpdateInfo)? best = null;
        foreach (var feed in UpdateChannels.Feeds(channel))
        {
            var manager = new UpdateManager(new GithubSource(repoUrl, null, UpdateChannels.ReadsPrereleases(feed)),
                new UpdateOptions { ExplicitChannel = feed, AllowVersionDowngrade = false }, locator);
            if (!manager.IsInstalled) return null;
            var update = await manager.CheckForUpdatesAsync();
            if (update is null) continue;
            if (best is null || update.TargetFullRelease.Version > best.Value.Item2.TargetFullRelease.Version) best = (manager, update);
        }
        return best;
    }

    /// <summary>The channel this copy was installed from ("win" or "beta"), or null when not installed.</summary>
    public static string? InstalledFeed => VelopackLocator.IsCurrentSet ? VelopackLocator.Current.Channel : null;
}
