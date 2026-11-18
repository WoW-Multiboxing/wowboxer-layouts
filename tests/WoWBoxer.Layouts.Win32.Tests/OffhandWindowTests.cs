using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.Win32.Tests;

[Collection("Sandbox")]
public class OffhandWindowTests
{
    private static readonly PixelRect Span = new(-1920, 0, 3840, 1080);

    private static List<WindowInfo> StartClients()
    {
        Sandbox.Start([]);
        Sandbox.ClientRule = null;
        Sandbox.SetWindowCount(0);
        Sandbox.SetWindowCount(3);
        return WindowFinder.FindGameWindows();
    }

    [Fact]
    public async Task Reapplying_to_the_selected_client_restores_its_first_position_and_leaves_siblings_alone()
    {
        var clients = StartClients();
        var service = new OffhandWindow();
        var chosen = clients[1];
        await service.ExpandAsync(chosen, Span);
        Assert.Equal(Span, WindowFinder.RectOf(chosen.Handle));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExpandAsync(clients[0], Span));
        Assert.True(service.IsManaging(chosen.Handle));
        Assert.False(service.IsManaging(clients[0].Handle));
        var resized = Span with { Width = 3200 };
        await service.ExpandAsync(chosen, resized);
        Assert.Equal(resized, WindowFinder.RectOf(chosen.Handle));
        Assert.Equal(clients[0].Rect, WindowFinder.RectOf(clients[0].Handle));
        Assert.Equal(clients[2].Rect, WindowFinder.RectOf(clients[2].Handle));

        await service.RestoreAsync();
        Assert.Null(service.ActiveWindow);
        Assert.All(clients, w => Assert.Equal(w.Rect, WindowFinder.RectOf(w.Handle)));
    }

    [Fact]
    public async Task A_reused_handle_and_pid_cannot_inherit_an_old_selection_or_its_restore()
    {
        var old = StartClients()[2];
        var service = new OffhandWindow();
        await service.ExpandAsync(old, Span);
        Sandbox.SetWindowCount(2);
        Sandbox.SetWindowCount(3);
        var replacement = WindowFinder.FindGameWindows()[2];
        Assert.Equal(old.Pid, replacement.Pid);
        Assert.Equal(old.Handle, replacement.Handle);
        Assert.NotEqual(old.ProcessStartedUtc, replacement.ProcessStartedUtc);
        var newPosition = new PixelRect(50, 60, 900, 800);
        Sandbox.Move(replacement.Handle, newPosition);
        Assert.Null(service.ActiveWindow);
        Assert.False(service.IsManaging(replacement.Handle));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExpandAsync(old, Span));
        await service.RestoreAsync();
        Assert.Equal(newPosition, WindowFinder.RectOf(replacement.Handle));
    }

    [Fact]
    public async Task A_client_that_refuses_the_span_is_rolled_back()
    {
        var clients = StartClients();
        Sandbox.ClientRule = r => r == Span ? r with { Width = 1024 } : r;
        try
        {
            var service = new OffhandWindow();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExpandAsync(clients[1], Span));
            Assert.Null(service.ActiveWindow);
            Assert.All(clients, w => Assert.Equal(w.Rect, WindowFinder.RectOf(w.Handle)));
        }
        finally
        {
            Sandbox.ClientRule = null;
        }
    }
}
