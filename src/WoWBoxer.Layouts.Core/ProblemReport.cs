using System.Text;
using System.Text.RegularExpressions;

namespace WoWBoxer.Layouts.Core;

/// <summary>
/// "Report a problem": hides personal names in a report and fits it into one Discord message. Nothing here sends
/// anything – the user sees the report, may edit it, and pastes it themselves.
/// </summary>
public static class ProblemReport
{
    /// <summary>Discord's limit for one message (without Nitro).</summary>
    public const int DiscordLimit = 2000;

    /// <summary>
    /// Words of the app and the game that are never treated as a name, so a character called "Wowboxer" or "Main" does
    /// not turn the report into nonsense (such a name says nothing personal anyway).
    /// </summary>
    private static readonly HashSet<string> AppWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "WoWBoxer", "Window", "Windows", "Layouts", "Layout", "Main", "Follower", "Slot", "Party", "Classic", "Era", "Forever",
        "Retail", "Discord", "Monitor", "Monitors", "Hover", "Game", "App", "Ok", "Info", "Warn", "Error", "Battle", "Microsoft",
    };

    /// <summary>What a report must not contain, and what each is replaced with.</summary>
    public sealed record Secrets(
        IReadOnlyCollection<string> UserFolders,
        IReadOnlyCollection<string> UserNames,
        IReadOnlyCollection<string> AccountNames,
        IReadOnlyCollection<string> CharacterNames);

    /// <summary>
    /// Replaces the Windows user folder with %USERPROFILE%, user names with &lt;user&gt;, WoW account names with
    /// &lt;account&gt; and character names with &lt;character&gt; – whole words, any case. Longest names first, so
    /// "Arthas" never leaves "Arthasa" half-hidden; names shorter than 2 letters are left (they would hide everything).
    /// </summary>
    public static string Redact(string text, Secrets secrets)
    {
        foreach (var folder in secrets.UserFolders.Where(f => f.Length > 3).OrderByDescending(f => f.Length))
            text = Regex.Replace(text, Regex.Escape(folder.TrimEnd('\\', '/')), "%USERPROFILE%", RegexOptions.IgnoreCase);
        foreach (var (names, mask) in new[] { (secrets.UserNames, "<user>"), (secrets.AccountNames, "<account>"), (secrets.CharacterNames, "<character>") })
            foreach (var name in names.Select(n => n.Trim()).Where(n => n.Length >= 2 && !AppWords.Contains(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(n => n.Length))
                text = Regex.Replace(text, $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(name)}(?![\p{{L}}\p{{N}}_])", mask, RegexOptions.IgnoreCase);
        return text;
    }

    /// <summary>
    /// One Discord message: the user's words, then the report as a code block. If it is too long, the oldest log lines
    /// go first (the report's "Last log lines" part), then the end of the report, marked so nobody wonders.
    /// </summary>
    public static string ForDiscord(string whatHappened, string report, int limit = DiscordLimit)
    {
        whatHappened = whatHappened.Trim().Replace("```", "'''");
        report = report.Replace("\r\n", "\n").Replace("```", "'''").TrimEnd();
        var head = whatHappened.Length == 0 ? "**Problem report**\n" : $"**What happened:** {whatHappened}\n";
        if (head.Length > limit / 2) head = head[..(limit / 2 - 2)] + "…\n";
        const string open = "```\n", close = "\n```", cut = "\n… (shortened – the full report is in the attached file)";
        string Message(string body) => head + open + body + close;
        if (Message(report).Length <= limit) return Message(report);

        var lines = report.Split('\n').ToList();
        var logStart = lines.FindIndex(l => l.StartsWith("Last log lines", StringComparison.Ordinal)) + 1;
        while (logStart > 0 && logStart < lines.Count && Message(string.Join('\n', lines) + cut).Length > limit)
            lines.RemoveAt(logStart);   // the oldest log line
        var body = string.Join('\n', lines);
        var room = limit - Message(cut).Length;
        if (body.Length > room) body = body[..Math.Max(0, room)].TrimEnd();
        return Message(body + cut);
    }

    /// <summary>"key: value" lines, aligned, for the report.</summary>
    public static string Lines(IEnumerable<(string Key, string Value)> items)
    {
        var list = items.ToList();
        var width = list.Count == 0 ? 0 : list.Max(i => i.Key.Length) + 1;
        var sb = new StringBuilder();
        foreach (var (key, value) in list) sb.Append((key + ":").PadRight(width + 1)).Append(value).Append('\n');
        return sb.ToString();
    }
}
