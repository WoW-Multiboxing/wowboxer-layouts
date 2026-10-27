using Velopack;
using Velopack.Sources;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Auto-update from GitHub Releases (Velopack). Only when installed with the installer, never during a run from
/// source. The only network call the app makes; it can be switched off in settings. Updates apply when the app quits.
/// </summary>
internal static class Updater
{
    public const string RepoUrl = "https://github.com/WoW-Multiboxing/wowboxer-layouts";

    public static async Task CheckAsync(IStatusSink status)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
            if (!manager.IsInstalled) return;
            var update = await manager.CheckForUpdatesAsync();
            if (update is null) return;
            await manager.DownloadUpdatesAsync(update);
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
            status.Ok($"Update {update.TargetFullRelease.Version} downloaded. It is installed when you quit the app.");
        }
        catch (Exception e)
        {
            // Offline, private repo, rate limit … never bother the user with it.
            status.Detail($"Update check skipped: {e.Message}");
        }
    }
}
