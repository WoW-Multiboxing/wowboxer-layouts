namespace WoWBoxer.Layouts.Core.Tests;

public class CommunityTests
{
    [Fact]
    public void Links_json_overrides_the_defaults()
    {
        var links = CommunityLinks.Parse("""
            { "format": "wowboxer-links/1", "discord": "https://discord.gg/NEW", "discordHelp": "https://discord.com/invite/HELP",
              "youtube": "https://www.youtube.com/@Other", "website": "https://wowboxer.com/" }
            """);

        Assert.Equal(new CommunityLinks("https://discord.gg/NEW", "https://discord.com/invite/HELP", "https://www.youtube.com/@Other", "https://wowboxer.com/"), links);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void A_broken_or_empty_file_keeps_the_defaults(string json) => Assert.Equal(CommunityLinks.Defaults, CommunityLinks.Parse(json));

    [Theory]
    [InlineData("http://discord.gg/abc")]            // not https
    [InlineData("https://evil.example/discord")]    // another site
    [InlineData("https://discord.gg.evil.example/x")]
    [InlineData("https://user@discord.gg/x")]
    [InlineData("https://discord.gg:8443/x")]
    [InlineData("javascript:alert(1)")]
    public void An_unsafe_link_keeps_the_default(string url)
    {
        var links = CommunityLinks.Parse($$"""{ "discord": "{{url}}", "youtube": "https://www.youtube.com/@Ok" }""");

        Assert.Equal(CommunityLinks.Defaults.Discord, links.Discord);
        Assert.Equal("https://www.youtube.com/@Ok", links.YouTube);   // the good field is still used
    }

    [Fact]
    public void Defaults_are_the_real_links()
    {
        Assert.Equal("https://discord.gg/Q5xwbqhKzQ", CommunityLinks.Defaults.Discord);
        Assert.Equal("https://discord.gg/3GY3HgF3HC", CommunityLinks.Defaults.DiscordHelp);
        Assert.Equal("https://www.youtube.com/@WoWBoxerHQ", CommunityLinks.Defaults.YouTube);
    }

    private static readonly ProblemReport.Secrets Fake = new(
        [@"C:\Users\Fakeuser Name"], ["Fakeuser Name", "fakeuser"], ["WOWACCT1", "MainAccount"], ["Arthas", "Jaina", "Al"]);

    [Fact]
    public void Redaction_hides_user_folder_user_account_and_character_names()
    {
        var text = """
            Path: C:\Users\Fakeuser Name\AppData\Local\WoWBoxer.Layouts\current\WoWBoxer.Layouts.exe
            Machine user fakeuser · window title "Arthas" · slot 2 Jaina (jaina) · account WOWACCT1 and mainaccount
            Arthasa is another word · Already · Al
            """;

        var redacted = ProblemReport.Redact(text, Fake);

        Assert.DoesNotContain("Fakeuser", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WOWACCT1", redacted);
        Assert.DoesNotContain("Jaina", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mainaccount", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"%USERPROFILE%\AppData\Local\WoWBoxer.Layouts", redacted);
        Assert.Contains("window title \"<character>\"", redacted);
        Assert.Contains("Arthasa is another word · Already · <character>", redacted);   // whole words only
    }

    [Fact]
    public void A_character_named_like_the_app_does_not_hide_the_app_name()
    {
        var redacted = ProblemReport.Redact("App: WoWBoxer 0.5.0 · slot 1 Main · Jaina", Fake with { CharacterNames = ["Wowboxer", "Main", "Jaina"] });

        Assert.Equal("App: WoWBoxer 0.5.0 · slot 1 Main · <character>", redacted);
    }

    [Fact]
    public void A_short_report_is_one_code_block_after_the_users_words()
    {
        var message = ProblemReport.ForDiscord("Windows do not move", "Version: 0.4.3\nLast log lines:\nline 1");

        Assert.Equal("**What happened:** Windows do not move\n```\nVersion: 0.4.3\nLast log lines:\nline 1\n```", message);
    }

    [Fact]
    public void A_long_report_drops_the_oldest_log_lines_and_fits_one_message()
    {
        var report = "Version: 0.4.3\nLayout: Trio\nLast log lines:\n" + string.Join('\n', Enumerable.Range(1, 200).Select(i => $"2026-10-30 12:00:{i % 60:00} Info   log line number {i}"));

        var message = ProblemReport.ForDiscord("It broke", report);

        Assert.True(message.Length <= ProblemReport.DiscordLimit, $"{message.Length} characters");
        Assert.Contains("Layout: Trio", message);
        Assert.Contains("log line number 200", message);      // the newest stay
        Assert.DoesNotContain("log line number 1\n", message); // the oldest go
        Assert.Contains("shortened", message);
        Assert.EndsWith("```", message);
    }

    [Fact]
    public void Backticks_in_the_report_cannot_break_the_code_block()
    {
        var message = ProblemReport.ForDiscord("a ``` b", "x ``` y");

        Assert.Equal(2, message.Split("```").Length - 1);
    }
}
