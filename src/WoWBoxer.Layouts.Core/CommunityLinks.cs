using System.Text.Json;

namespace WoWBoxer.Layouts.Core;

/// <summary>
/// The community links the app shows (Discord, the Discord help channel, YouTube, the website). They live in one small
/// file on the website, <c>https://wowboxer.com/links.json</c>, so they can change without a new app version:
/// <code>
/// { "format": "wowboxer-links/1",
///   "discord": "https://discord.gg/…", "discordHelp": "https://discord.gg/…",
///   "youtube": "https://www.youtube.com/@…", "website": "https://wowboxer.com" }
/// </code>
/// Every field is optional; a missing or unsafe one keeps the compiled-in value (<see cref="Defaults"/>).
/// </summary>
public sealed record CommunityLinks(string Discord, string DiscordHelp, string YouTube, string Website)
{
    public const string Format = "wowboxer-links/1";

    /// <summary>Compiled in: used until the website's file has been read once, and whenever it cannot be read.</summary>
    public static CommunityLinks Defaults { get; } = new(
        "https://discord.gg/Q5xwbqhKzQ",
        "https://discord.gg/3GY3HgF3HC",
        "https://www.youtube.com/@WoWBoxerHQ",
        "https://wowboxer.com");

    /// <summary>Reads links.json. Broken JSON gives the defaults; each field must be https on its own site, or the default stays.</summary>
    public static CommunityLinks Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return Defaults;
            string Pick(string name, string fallback, params string[] hosts) =>
                doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && IsSafe(v.GetString(), hosts) ? v.GetString()!.Trim() : fallback;
            return new CommunityLinks(
                Pick("discord", Defaults.Discord, "discord.gg", "discord.com"),
                Pick("discordHelp", Defaults.DiscordHelp, "discord.gg", "discord.com"),
                Pick("youtube", Defaults.YouTube, "youtube.com", "www.youtube.com", "youtu.be"),
                Pick("website", Defaults.Website, "wowboxer.com", "www.wowboxer.com"));
        }
        catch (JsonException)
        {
            return Defaults;
        }
    }

    /// <summary>
    /// Only https links to the expected sites: a changed or broken file on the website can never send players to
    /// another site.
    /// </summary>
    public static bool IsSafe(string? url, params string[] hosts) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) && uri.UserInfo.Length == 0 && uri.IsDefaultPort;
}
