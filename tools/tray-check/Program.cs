using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WoWBoxer.Layouts.App;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;
using Forms = System.Windows.Forms;

namespace WoWBoxer.Layouts.Tools;

// Windows desktop: dotnet run --project tools/tray-check -c Release -- --demo
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is not ["--demo"])
        {
            Console.Error.WriteLine("Pass --demo to use the isolated demo settings and fake game clients.");
            return 2;
        }

        try
        {
            // WPF resolves unqualified pack URIs (including the tray icon) against the entry assembly.
            // Set the host identity before constructing the Application or resolving any WPF resources.
            Assembly.SetEntryAssembly(typeof(App.Program).Assembly);
            return RunDemo();
        }
        catch (Exception e)
        {
            SmokeFailure.Report("Tray startup check", e);
            return 1;
        }
    }

    private static int RunDemo()
    {
        var app = new App.App();
        app.InitializeComponent();
        bool passed = false;
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            try
            {
                await CheckTrayAsync(app);
                passed = true;
            }
            catch (Exception e)
            {
                SmokeFailure.Report("Tray behavior check", e);
            }
            finally
            {
                app.Shutdown();
            }
        }));
        app.Run();
        return passed ? 0 : 1;
    }

    private static async Task CheckTrayAsync(App.App app)
    {
        if (!Sandbox.Active || app.MainWindow is not MainWindow window)
            throw new InvalidOperationException("Expected a running demo window.");
        var vm = window.ViewModel;
        Check(vm.UseFewerResourcesInTray && vm.Placing && vm.Hover.Enabled && Sandbox.Hover.Enabled,
            "demo starts with background optimization, placement and simulated hover enabled");

        Sandbox.SetWindowCount(2);
        await UntilAsync(() => vm.OffhandClients.Count == 2, "visible window refreshes the client list");
        var caption = FindMinimizeButton(window) ?? throw new InvalidOperationException("No minimize-to-tray button.");
        caption.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await UntilAsync(() => !window.IsVisible, "minimize hides the window");

        Sandbox.SetWindowCount(3);
        var fresh = WindowFinder.FindGameWindows().Last();
        var thirdSlot = LayoutEngine.Resolve(vm.Current!, vm.Monitors)[2].Rect;
        Check(WindowFinder.RectOf(fresh.Handle) != thirdSlot, "a newly opened client initially sits outside its slot");
        await UntilAsync(() => WindowFinder.RectOf(fresh.Handle) == thirdSlot, "the hidden app places a new client");
        Check(!window.IsVisible && vm.OffhandClients.Count == 2, "the hidden app defers updating the client picker");
        Check(vm.Placing && vm.Hover.Enabled && Sandbox.Hover.Enabled, "placement and simulated hover stay enabled");

        // Invoke the registered menu item. Looking up the shell icon depends on Explorer version and tray overflow.
        var trayField = typeof(App.App).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic);
        var tray = trayField?.GetValue(app) as Forms.NotifyIcon;
        if (tray?.Visible != true || tray.ContextMenuStrip is null)
            throw new InvalidOperationException("The running app has no visible tray icon with a menu.");
        var open = tray.ContextMenuStrip.Items.OfType<Forms.ToolStripMenuItem>()
            .Single(i => i.Text?.StartsWith("Open WoWBoxer", StringComparison.Ordinal) == true);
        open.PerformClick();
        await UntilAsync(() => window.IsVisible && window.WindowState != WindowState.Minimized &&
            vm.OffhandClients.Count == 3, "Open from the tray restores the same window and refreshes its client list");
        Check(ReferenceEquals(app.MainWindow, window) && vm.Placing && Sandbox.Hover.Enabled,
            "the same app and its enabled settings survive the round trip");
    }

    private static Button? FindMinimizeButton(DependencyObject parent)
    {
        if (parent is Button button && AutomationProperties.GetAutomationId(button) == "MinimizeToTray") return button;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindMinimizeButton(VisualTreeHelper.GetChild(parent, i)) is { } found) return found;
        return null;
    }

    private static async Task UntilAsync(Func<bool> condition, string description)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(15)) await Task.Delay(100);
        Check(condition(), description);
    }

    private static void Check(bool success, string description)
    {
        if (!success) throw new InvalidOperationException(description);
        Console.WriteLine($"PASS  {description}");
    }
}
