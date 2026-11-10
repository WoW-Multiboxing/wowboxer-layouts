using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.Win32.Tests;

/// <summary>
/// Bug 2026-11-10 (owner, 3 Forever clients): the client changes the size it is given, and WoWBoxer resized it again
/// every second for 30 s – after each swap the windows went back and forth. A simulated client that keeps 4:3.
/// </summary>
[Collection("Sandbox")]
public class SelfResizingClientTests
{
    private static readonly PixelRect Screen = new(0, 0, 2560, 1392);

    /// <summary>A client with a minimum width (widens itself, keeps its left edge) – placeholder until the probe's rule.</summary>
    private const int MinWidth = 928;
    private static PixelRect KeepsFourThree(PixelRect r) => KeepsWidth(r, MinWidth);
    private static PixelRect KeepsWidth(PixelRect r, int min) => r.Width >= min ? r : r with { Width = min };

    private static (SquadPlan Plan, SlotKeeper Keeper, List<nint> Windows) Squad(double minAspect)
    {
        Sandbox.Start([new MonitorInfo(1, @"\\.\DISPLAY1", true, Screen, Screen)]);
        Sandbox.ClientRule = null;
        Sandbox.SetWindowCount(0);
        Sandbox.SetWindowCount(3);
        var profile = Presets.Create(PresetKind.Side, 3, Sandbox.Monitors, options: new LayoutOptions { MinAspect = minAspect });
        var plan = new SquadPlan(profile, Sandbox.Monitors, rename: false);
        var keeper = new SlotKeeper();
        var windows = WindowFinder.FindGameWindows();
        keeper.Sync(windows, plan.Slots.Count);
        return (plan, keeper, windows.Select(w => w.Handle).ToList());
    }

    [Fact]
    public async Task A_client_that_keeps_its_own_size_is_resized_a_few_times_then_left_alone_and_explained_once()
    {
        var (plan, keeper, _) = Squad(minAspect: 1.25);   // 5:4 followers – narrower than this client allows
        var moves = 0;
        Sandbox.ClientRule = r => { moves++; return KeepsFourThree(r); };
        var refused = new List<(nint, int, PixelRect)>();
        keeper.Refused = (h, slot, r) => refused.Add((h, slot, r));

        keeper.Settle(TimeSpan.FromSeconds(5));   // what a swap does
        for (var step = 0; step < 10; step++) await keeper.StepAsync(plan, new QuietSink());

        var narrow = plan.Slots.Count(s => s.Rect.Width < MinWidth);
        Assert.True(narrow >= 2);
        Assert.Equal(narrow, refused.Count);                              // each window it could not size: said once
        Assert.True(moves <= narrow * SlotKeeper.HoldLimit + (plan.Slots.Count - narrow), $"{moves} moves");   // not every second for 30 s
        Assert.All(refused, r => Assert.Equal(MinWidth, r.Item3.Width));   // the size the game keeps
        Assert.True(keeper.AllLanded(plan));                              // a waiting swap may go now
        var before = moves;
        await keeper.StepAsync(plan, new QuietSink());
        Assert.Equal(before, moves);                                      // left alone
    }

    [Fact]
    public async Task Sizes_the_client_accepts_land_at_once_and_swap_round_trips_end_where_they_started()
    {
        var (plan, keeper, windows) = Squad(minAspect: 4.0 / 3);   // a layout made for this client
        var smallest = plan.Slots.Min(s => s.Rect.Width);
        Sandbox.ClientRule = r => KeepsWidth(r, smallest);
        var refused = 0;
        keeper.Refused = (_, _, _) => refused++;
        var home = plan.Slots.Select(s => s.Rect).ToList();
        var mains = WindowSwap.MainSlots(plan.Profile);
        var pos = WindowSwap.Home(3);

        for (var round = 0; round < 20; round++)
        {
            pos = WindowSwap.ToMain(pos, round % 2 == 0 ? 1 + round / 2 % 2 : 0, mains[0]);
            var placed = new SquadPlan(WindowSwap.Apply(plan.Profile, pos), Sandbox.Monitors, rename: false);
            for (var c = 0; c < 3; c++) placed.Place(windows[c], c);
            keeper.Settle(TimeSpan.FromSeconds(5));
            await keeper.StepAsync(placed, new QuietSink());
            Assert.True(keeper.AllLanded(placed), $"round {round}");
        }
        pos = WindowSwap.Home(3);
        for (var c = 0; c < 3; c++) plan.Place(windows[c], c);

        Assert.Equal(0, refused);
        Assert.Equal(home, windows.Select(WindowFinder.VisibleRectOf));
    }

    private sealed class QuietSink : IStatusSink
    {
        public void Report(StatusLevel level, string message) { }
    }
}
