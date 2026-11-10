namespace WoWBoxer.Layouts.Core.Tests;

public class RateGuardTests
{
    private DateTime _now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Blocks_after_the_limit_within_the_window_and_says_so_once()
    {
        var guard = new RateGuard<string>(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), () => _now);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(guard.Allow("swap", out var just));
            Assert.False(just);
            _now += TimeSpan.FromMilliseconds(100);
        }
        Assert.False(guard.Allow("swap", out var blocked));
        Assert.True(blocked);
        Assert.False(guard.Allow("swap", out blocked));
        Assert.False(blocked);   // logged once
        _now += TimeSpan.FromSeconds(5);
        Assert.True(guard.Allow("swap", out _));   // the pause is over
    }

    [Fact]
    public void Slow_events_never_block_and_keys_are_separate()
    {
        var guard = new RateGuard<int>(2, TimeSpan.FromSeconds(1), clock: () => _now);
        for (var i = 0; i < 20; i++)
        {
            Assert.True(guard.Allow(1, out _));
            _now += TimeSpan.FromMilliseconds(600);
        }
        Assert.True(guard.Allow(2, out _));
        Assert.True(guard.Allow(2, out _));
        Assert.False(guard.Allow(2, out _));
        Assert.True(guard.Allow(1, out _));
    }

    [Fact]
    public void Without_a_pause_only_reset_unblocks()
    {
        var guard = new RateGuard<int>(1, TimeSpan.FromSeconds(10), clock: () => _now);
        guard.Allow(7, out _);
        Assert.False(guard.Allow(7, out _));
        _now += TimeSpan.FromDays(1);
        Assert.True(guard.IsBlocked(7));
        Assert.Equal([7], guard.Blocked);
        guard.Reset();
        Assert.True(guard.Allow(7, out _));
    }
}
