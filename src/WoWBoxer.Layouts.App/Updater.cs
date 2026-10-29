using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Auto-update from GitHub Releases (Velopack). Only when installed with the installer, never during a run from
/// source. The only network call the app makes; it can be switched off in settings. A new version found at start is
/// installed at once and the app opens again, while it is still stopped (no windows placed); once started, it waits
/// until the app quits.
/// </summary>
internal static class Updater
{
    public const string RepoUrl = "https://github.com/WoW-Multiboxing/wowboxer-layouts";

    /// <summary>True while the app quits only to install an update and open again (so quitting changes nothing else).</summary>
    public static bool RestartingForUpdate { get; private set; }

    /// <param name="canRestartNow">Still safe to restart (the app is stopped)? Asked once the update is downloaded.</param>
    /// <param name="args">The app's own start arguments, passed on to the new version.</param>
    public static async Task CheckAsync(IStatusSink status, Func<bool> canRestartNow, string[] args)
    {
        try
        {
            // Channels: stable builds use Velopack's default channel ("win") and only see full releases; a build
            // packed with --channel beta is published as a GitHub prerelease, so it must look at prereleases too.
            // Each install keeps following the channel it was installed from.
            var channel = VelopackLocator.IsCurrentSet ? VelopackLocator.Current.Channel : null;
            var beta = !string.IsNullOrEmpty(channel) && channel != "win";
            var manager = new UpdateManager(new GithubSource(RepoUrl, null, beta));
            if (!manager.IsInstalled) return;
            var update = await manager.CheckForUpdatesAsync();
            if (update is null) return;
            await manager.DownloadUpdatesAsync(update);
            var version = update.TargetFullRelease.Version;
            if (canRestartNow())
            {
                status.Ok($"Installing update {version} – the app opens again in a moment.");
                manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: true, args);
                RestartingForUpdate = true;
                System.Windows.Application.Current.Shutdown();
                return;
            }
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
            status.Ok($"Update {version} downloaded. It is installed when you quit the app (it is placing windows now).");
        }
        catch (Exception e)
        {
            // Offline, private repo, rate limit … never bother the user with it.
            status.Detail($"Update check skipped: {e.Message}");
        }
    }
}
