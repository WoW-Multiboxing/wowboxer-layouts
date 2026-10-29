using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Auto-update from GitHub Releases (Velopack). Only when installed with the installer, never during a run from
/// source. The only network call the app makes; it can be switched off in settings. When the app opens and finds a new
/// version, it asks: "Update now" installs it and opens the app again; "Later" installs it when the app quits.
/// </summary>
internal static class Updater
{
    public const string RepoUrl = "https://github.com/WoW-Multiboxing/wowboxer-layouts";

    /// <summary>True while the app quits only to install an update and open again (so quitting changes nothing else).</summary>
    public static bool RestartingForUpdate { get; private set; }

    /// <param name="ask">Asks the user "update now?" for the given version; null (a tray start, no window) = install on quit.</param>
    /// <param name="args">The app's own start arguments, passed on to the new version.</param>
    public static async Task CheckAsync(IStatusSink status, Func<string, bool>? ask, string[] args)
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
            var version = update.TargetFullRelease.Version;
            if (ask?.Invoke(version.ToString()) == true)
            {
                status.Ok($"Downloading update {version} …");
                await manager.DownloadUpdatesAsync(update);
                status.Ok($"Installing update {version} – the app opens again in a moment.");
                manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: true, args);
                RestartingForUpdate = true;
                System.Windows.Application.Current.Shutdown();
                return;
            }
            await manager.DownloadUpdatesAsync(update);
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
            status.Ok($"Update {version} downloaded. It is installed when you quit the app.");
        }
        catch (Exception e)
        {
            // Offline, private repo, rate limit … never bother the user with it.
            status.Detail($"Update check skipped: {e.Message}");
        }
    }
}
