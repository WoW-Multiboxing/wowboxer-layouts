using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Auto-update from GitHub Releases (Velopack). Only when installed with the installer, never during a run from
/// source. The only network call the app makes; it can be switched off in settings. When the app opens and finds a new
/// version, it asks: "Update now" installs it and opens the app again; "Later" installs it when the app quits.
/// </summary>
internal static class Updater
{
    public const string RepoUrl = UpdateFinder.RepoUrl;

    /// <summary>True while the app quits only to install an update and open again (so quitting changes nothing else).</summary>
    public static bool RestartingForUpdate { get; private set; }

    /// <param name="channelSetting">Settings › Updates ("Stable", "Beta" or null = the channel the copy was installed from).</param>
    /// <param name="ask">Asks the user "update now?" for the given version; null (a tray start, no window) = install on quit.</param>
    /// <param name="args">The app's own start arguments, passed on to the new version.</param>
    public static async Task CheckAsync(IStatusSink status, string? channelSetting, Func<string, bool>? ask, string[] args)
    {
        if (AppFlavor.IsDev)
        {
            status.Detail("WoWBoxer Dev does not update itself: run tools/dev-build.ps1 again for a newer dev build.");
            return;
        }
        try
        {
            // Channels (docs/release-process.md): Stable reads only normal releases; Beta reads pre-releases and normal
            // releases and takes the newer. The setting is read at every check, so a switch counts from the next start.
            var channel = Core.UpdateChannels.Effective(channelSetting, UpdateFinder.InstalledFeed);
            var found = await UpdateFinder.FindAsync(channel);
            if (found is null)
            {
                if (UpdateFinder.InstalledFeed is not null)
                    status.Info($"You have the newest version ({ViewModels.MainViewModel.AppVersion}, {channel.ToLowerInvariant()} channel).");   // so a quiet start is not a mystery
                return;
            }
            var (manager, update) = found.Value;
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
