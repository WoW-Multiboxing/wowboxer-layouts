using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Core.Tests;

public class UpdateChannelTests
{
    [Theory]
    [InlineData(null, "win", "Stable")]
    [InlineData(null, null, "Stable")]
    [InlineData(null, "beta", "Beta")]       // installed from WoWBoxer-Beta-Setup.exe
    [InlineData("Beta", "win", "Beta")]      // a stable install switched to Beta in Settings
    [InlineData("Stable", "beta", "Stable")] // a beta install switched back
    [InlineData("beta", "win", "Beta")]
    [InlineData("nonsense", "beta", "Beta")]
    public void Effective_channel(string? setting, string? installed, string expected) =>
        Assert.Equal(expected, UpdateChannels.Effective(setting, installed));

    [Fact]
    public void Stable_never_reads_the_beta_feed()
    {
        Assert.Equal(["win"], UpdateChannels.Feeds("Stable"));
        Assert.False(UpdateChannels.ReadsPrereleases("win"));
    }

    [Fact]
    public void Beta_reads_beta_and_stable()
    {
        Assert.Equal(["beta", "win"], UpdateChannels.Feeds("Beta"));
        Assert.True(UpdateChannels.ReadsPrereleases("beta"));
    }

    [Theory]
    [InlineData("0.6.1", false)]
    [InlineData("0.6.1-beta.1", true)]
    [InlineData("0.6.1-beta.1+abc123", true)]
    [InlineData("0.6.1+abc-123", false)]
    public void Prerelease_versions(string version, bool expected) => Assert.Equal(expected, UpdateChannels.IsPrerelease(version));
}

public class ExperimentalFeatureTests
{
    private static readonly ExperimentalFeature[] Known = [new("swap", "Swap", "test")];

    [Fact]
    public void Off_by_default() => Assert.False(ExperimentalFeatures.IsOn([], "swap", Known));

    [Fact]
    public void On_when_switched_on() => Assert.True(ExperimentalFeatures.IsOn(ExperimentalFeatures.Set(null, "swap", true), "swap", Known));

    [Fact]
    public void Off_again_without_a_release()
    {
        var on = ExperimentalFeatures.Set(null, "swap", true);
        Assert.False(ExperimentalFeatures.IsOn(ExperimentalFeatures.Set(on, "swap", false), "swap", Known));
    }

    [Fact]
    public void A_feature_this_build_does_not_have_is_off() => Assert.False(ExperimentalFeatures.IsOn(["gone"], "gone", Known));

    [Fact]
    public void Set_keeps_other_ids_and_no_duplicates() =>
        Assert.Equal(["other", "swap"], ExperimentalFeatures.Set(["other", "swap", "swap"], "swap", true));

    [Fact]
    public void Ids_are_unique_in_this_build() =>
        Assert.Equal(ExperimentalFeatures.All.Count, ExperimentalFeatures.All.Select(f => f.Id).Distinct().Count());
}
