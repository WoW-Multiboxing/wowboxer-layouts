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

    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(false, 200, false)]
    [InlineData(false, 1200, true)]    // a value set outside the app, above the slider's range
    [InlineData(true, 30, false)]
    public void Turning_on_always_sets_the_default_delay_and_keeps_bring_to_front(bool enabled, int delay, bool front)
    {
        var on = new HoverSettings(enabled, delay, front).TurnedOn();

        Assert.Equal(new HoverSettings(true, HoverSettings.DefaultDelayMs, front), on);
        Assert.Equal(30, HoverSettings.DefaultDelayMs);
    }

    [Fact]
    public void Describe_reads_like_the_windows_setting() =>
        Assert.Equal("on, delay 30 ms, bring to front off", new HoverSettings(true, 30, false).Describe());
}
