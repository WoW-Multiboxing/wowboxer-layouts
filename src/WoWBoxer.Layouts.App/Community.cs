using System.IO;
using System.Net.Http;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// The community links (Discord, Discord help, YouTube, website). Compiled in, and replaced by the website's
/// <see cref="LinksUrl"/> – read at most once a day and kept on this PC. A plain web request that sends nothing; it
/// follows the "Check for updates" switch, so switching that off means no network at all.
/// </summary>
internal static class Community
{
    public const string LinksUrl = "https://wowboxer.com/links.json";

    private static string Folder => Demo.On ? Path.Combine(Demo.Root, "local") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoWBoxer", "Layouts");
    private static string CacheFile => Path.Combine(Folder, "links.json");
    /// <summary>When the website was last asked (also after a failed try, so it is never asked more than once a day).</summary>
    private static string CheckedFile => Path.Combine(Folder, "links.checked");

    public static CommunityLinks Links { get; private set; } = Load();

    private static CommunityLinks Load()
    {
        try
        {
            return File.Exists(CacheFile) ? CommunityLinks.Parse(File.ReadAllText(CacheFile)) : CommunityLinks.Defaults;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return CommunityLinks.Defaults;
        }
    }

    /// <summary>Reads links.json from the website if it was not read in the last 24 hours. Quiet when offline.</summary>
    public static async Task RefreshAsync(bool allowed)
    {
        if (!allowed) return;
        try
        {
            if (File.Exists(CheckedFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(CheckedFile) < TimeSpan.FromDays(1)) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(CheckedFile, DateTime.UtcNow.ToString("O"));
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync(LinksUrl);
            if (json.Length > 16_000) return;   // not our small file
            Links = CommunityLinks.Parse(json);
            File.WriteAllText(CacheFile, json);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            // offline, or the file is not on the website yet: keep the links we have
        }
    }
}
