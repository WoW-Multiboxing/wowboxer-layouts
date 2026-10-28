using System.Text.Json;

namespace WoWBoxer.Layouts.Core.Tests;

public class HoverSettingsTests
{
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(30, 30)]
    [InlineData(900, 500)]
    public void New_delays_stay_in_the_slider_range(int ms, int expected) => Assert.Equal(expected, HoverSettings.ClampDelay(ms));

    [Fact]
    public void Nothing_to_restore_before_the_app_changed_anything() =>
        Assert.False(HoverSettings.NeedsRestore(null, new HoverSettings(true, 30, false)));

    [Fact]
    public void Restore_only_when_the_setting_differs_from_the_original()
    {
        var original = new HoverSettings(false, 100, true);

        Assert.False(HoverSettings.NeedsRestore(original, original with { }));
        Assert.True(HoverSettings.NeedsRestore(original, original with { Enabled = true }));
        Assert.True(HoverSettings.NeedsRestore(original, original with { DelayMs = 30 }));
        Assert.True(HoverSettings.NeedsRestore(original, original with { BringToFront = false }));
    }

    [Fact]
    public void The_saved_original_survives_the_settings_file()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);   // as AppSettings saves it
        var original = new HoverSettings(false, 1200, true);   // a value set outside the app stays exact

        Assert.Equal(original, JsonSerializer.Deserialize<HoverSettings>(JsonSerializer.Serialize(original, options), options));
    }

    [Fact]
    public void Describe_reads_like_the_windows_setting() =>
        Assert.Equal("on, delay 30 ms, bring to front off", new HoverSettings(true, 30, false).Describe());
}
