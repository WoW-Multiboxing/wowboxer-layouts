using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.Win32.Tests;

/// <summary>#help 2026-11-10 (Altsy): windows flipped non-stop – the guard fought another program over "on top".</summary>
[Collection("Sandbox")]
public class ZOrderGuardTests
{
    [Fact]
    public async Task The_guard_gives_up_on_a_window_another_program_keeps_changing_and_resumes_on_request()
    {
        var rect = new PixelRect(0, 0, 2560, 1440);
        Sandbox.Start([new MonitorInfo(1, @"\.\DISPLAY1", true, rect, rect)]);
        Sandbox.SetWindowCount(2);
        var (main, follower) = (WindowFinder.FindGameWindows()[0].Handle, WindowFinder.FindGameWindows()[1].Handle);
        var gaveUp = new List<nint>();
        using var guard = new ZOrderGuard(() => [(main, ZOrder.Normal), (follower, ZOrder.Top)], gaveUp: gaveUp.Add);

        var corrections = 0;
        for (var round = 0; round < ZOrderGuard.FightLimit + 3; round++)
        {
            WindowPlacer.ApplyZOrder(follower, ZOrder.Normal);   // the other program takes "on top" away again
            corrections += guard.Reassert();
            await Task.Delay(420);                               // past the guard's 400 ms settle time
        }

        Assert.Equal(ZOrderGuard.FightLimit, corrections);       // then it stops fighting
        Assert.Equal([follower], gaveUp);                        // and says so once
        Assert.Contains(follower, guard.GivenUp);
        Assert.False(WindowPlacer.IsTopmost(follower));
        Assert.Equal(0, guard.Reassert());                       // the main (in its place) was never touched

        guard.Resume();                                          // Start / Re-arrange
        Assert.Equal(1, guard.Reassert());
        Assert.True(WindowPlacer.IsTopmost(follower));
    }
}
