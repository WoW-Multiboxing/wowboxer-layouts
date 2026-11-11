namespace WoWBoxer.Layouts.Core.Tests;

/// <summary>Bug 2026-11-10: swap hotkeys faster than the Forever clients settle made the windows go back and forth.</summary>
public class SwapQueueTests
{
    private DateTime _now = new(2026, 11, 10, 15, 20, 0, DateTimeKind.Utc);

    [Fact]
    public void A_swap_waits_until_the_previous_one_has_landed()
    {
        var q = new SwapQueue(TimeSpan.FromSeconds(1.5), clock: () => _now);

        Assert.Equal(SwapQueue.Offer.RunNow, q.Ask("2"));
        q.Started();
        Assert.Equal(SwapQueue.Offer.Waiting, q.Ask("3"));   // pressed 300 ms later
        Assert.Null(q.Next());                               // still landing
        q.Landed();                                          // both windows in place
        Assert.Equal("3", q.Next());
        Assert.Null(q.Next());
    }

    [Fact]
    public void A_flood_of_presses_keeps_at_most_two_waiting_and_the_order()
    {
        var q = new SwapQueue(TimeSpan.FromSeconds(1.5), clock: () => _now);
        Assert.Equal(SwapQueue.Offer.RunNow, q.Ask("next"));
        q.Started();

        var offers = new[] { "2", "3", "2", "3", "2" }.Select(q.Ask).ToList();

        Assert.Equal([SwapQueue.Offer.Waiting, SwapQueue.Offer.Waiting, SwapQueue.Offer.Dropped, SwapQueue.Offer.Dropped, SwapQueue.Offer.Dropped], offers);
        _now += q.MaxBusy;                                   // never confirmed: the time limit lets the next one go
        Assert.Equal("2", q.Next());
        q.Started();
        Assert.Null(q.Next());
        q.Landed();
        Assert.Equal("3", q.Next());
    }

    [Fact]
    public void Clear_forgets_waiting_swaps()
    {
        var q = new SwapQueue(TimeSpan.FromSeconds(1.5), clock: () => _now);
        q.Ask("1");
        q.Started();
        q.Ask("2");
        q.Clear();
        Assert.False(q.Busy);
        Assert.Null(q.Next());
        Assert.Equal(SwapQueue.Offer.RunNow, q.Ask("3"));
    }
}
