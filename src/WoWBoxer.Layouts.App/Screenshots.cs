using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WoWBoxer.Layouts.App.ViewModels;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;
using Forms = System.Windows.Forms;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// "--screenshots FOLDER" (always in demo mode): renders each screen of the app at 1440×900 as PNG, plus
/// <c>shots.json</c> (file, alt text, caption, app version). tools/make-screenshots.ps1 turns them into WebP and writes
/// the website's manifest.json. The window stays off-screen and never takes focus; the pictures come from WPF's own
/// renderer, so they do not depend on the screen, its scale or what is on top of it.
/// </summary>
internal static class Screenshots
{
    public const int Width = 1440, Height = 900;

    public sealed record Shot(string File, string Title, string Alt, string Caption);

    public static async Task RunAsync(MainWindow main, string folder, Func<Forms.ContextMenuStrip> trayMenu)
    {
        Directory.CreateDirectory(folder);
        var vm = main.ViewModel;
        // No resize frame or caption style: Windows then never limits the window to the (possibly small) screen.
        main.WindowStyle = WindowStyle.None;
        main.ResizeMode = ResizeMode.NoResize;
        main.ShowActivated = false;
        main.ShowInTaskbar = false;
        main.WindowStartupLocation = WindowStartupLocation.Manual;
        main.Left = main.Top = -20000;
        main.Width = Width;
        main.Height = Height;
        main.Show();

        var shots = new List<Shot>();
        async Task Snap(string name, string title, string alt, string caption, Func<BitmapSource>? render = null)
        {
            if (vm.SelectedTab == AppTab.Setup) await InPlace(vm);
            await Settle();
            var bitmap = render?.Invoke() ?? Render((FrameworkElement)main.Content);
            Save(bitmap, Path.Combine(folder, name + ".png"));
            shots.Add(new Shot(name, title, alt, caption));
        }

        // 1) Setup: 3 clients, Main + side, 1 monitor
        Squad(vm, 3);
        Use(vm, PresetKind.Side, monitors: 1);
        vm.SelectedTab = AppTab.Setup;
        vm.SelectedSlotIndex = -1;
        await Snap("setup-main-side", "Setup: clients, layout and monitors", "WoWBoxer Window Layouts, Setup: three WoW windows in the Main + side layout on one monitor – the main window big on the left, two followers stacked on the right – and the squad list with every window connected.",
            "Pick how many clients and a layout – every window goes to its place as it opens.");

        // 2) Setup: 2 monitors, Free for all
        Squad(vm, 4);
        Use(vm, PresetKind.Free, monitors: 2);
        // a hand-made arrangement; window 2 straddles the monitor border (Free for all allows it)
        Place(vm, 0, 0, 0, 1840, 1150);
        Place(vm, 1, 1960, 560, 1100, 740);
        Place(vm, 2, 3160, 40, 1260, 440);
        Place(vm, 3, 3160, 520, 1260, 440);
        vm.SelectedSlotIndex = -1;
        await Snap("setup-two-monitors-free", "Two monitors, Free for all", "WoWBoxer Setup with two monitors (2560×1440 and 1920×1080) and the Free for all layout: four WoW windows arranged by hand over both monitors on the map – window 2 straddles the border between them.",
            "Two monitors, Free for all: drag any window anywhere – across monitors too.");

        // 3) Setup: 10 clients, two parties
        Squad(vm, 10);
        Use(vm, PresetKind.Side, monitors: 2);
        vm.SelectedSlotIndex = -1;
        await Snap("setup-ten-clients", "Ten clients in two parties", "WoWBoxer Setup with ten WoW clients in two parties of five over two monitors; the squad list is grouped into Party 1 and Party 2.",
            "Up to 10 clients: two parties of five, each with its own main.");

        // 4) Window details (the inspector of one squad row)
        Squad(vm, 3);
        Use(vm, PresetKind.Side, monitors: 1);
        vm.SelectedSlotIndex = 0;
        vm.ExpandedIndex = 0;
        await Snap("window-details", "Window details", "WoWBoxer Setup with the details of window 1 open: name, class, position and size in pixels, always on top, and whether the whole action bar fits.",
            "Fine-tune any window: exact position and size, on top, and an action-bar check.");
        vm.ExpandedIndex = -1;

        // 5) Settings: hover to activate and the action-bar fit
        vm.SelectedTab = AppTab.Settings;
        await Snap("settings", "Settings: hover to activate and action bar", "WoWBoxer Settings: hover to activate windows (a Windows setting) with its delay, the action-bar fit (UI Scale) and the window rules.",
            "Hover to activate is a Windows setting – it sends nothing to the game. Auto-fit keeps the whole action bar in view.");

        // 6) Help: Report a problem
        vm.SelectedTab = AppTab.Help;
        await Snap("help", "Help and Report a problem", "WoWBoxer Help tab: the Report a problem button, links to the WoWBoxer Discord, YouTube and website, tools to see what the app sees, and common problems with their fixes.",
            "Stuck? Common problems with their fixes, and Report a problem for #setup-help on Discord.");

        // 7) About and the community links
        vm.SelectedTab = AppTab.About;
        await Snap("about", "About and community links", "WoWBoxer About tab: version, MIT licence, privacy (no telemetry) and the Discord, YouTube and website links.",
            "Free and open source (MIT). No telemetry – your layouts and logs stay on your PC.");

        // 8) The tray menu, over the app's corner as it pops up from the taskbar
        vm.SelectedTab = AppTab.Setup;
        vm.SelectedSlotIndex = -1;
        await Snap("tray-menu", "Tray menu", "The WoWBoxer tray menu: Open WoWBoxer, Open missing WoW clients, Re-arrange, Identify windows, Stop placing windows, Hover to activate windows, Report a problem and Quit.",
            "Everything you need mid-raid is one right-click away in the tray.", () => WithTrayMenu(Render((FrameworkElement)main.Content), trayMenu()));

        // 9) First start: the welcome screen over the app
        vm.SelectedTab = AppTab.Setup;
        await Snap("first-run", "Welcome screen", "WoWBoxer welcome screen on first start: what the app does (places WoW windows) and what it never does (no keys or clicks sent to the game, no memory reading, no injection).",
            "What it does – and what it never does.", () =>
            {
                var app = Render((FrameworkElement)main.Content);
                var welcome = FirstRunWindow.Create(null);
                welcome.ShowActivated = false;
                welcome.ShowInTaskbar = false;
                welcome.WindowStartupLocation = WindowStartupLocation.Manual;
                welcome.Left = welcome.Top = -20000;
                welcome.Show();
                welcome.UpdateLayout();
                var dialog = Render(welcome);
                welcome.Close();
                return Compose(app, dialog);
            });

        var json = JsonSerializer.Serialize(new { version = MainViewModel.AppVersion, shots },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(Path.Combine(folder, "shots.json"), json);
    }

    /// <summary><paramref name="count"/> open demo windows and the made-up names and classes on every slot.</summary>
    private static void Squad(MainViewModel vm, int count)
    {
        Sandbox.SetWindowCount(count);
        vm.ClientCount = count;
    }

    private static void Use(MainViewModel vm, PresetKind kind, int monitors)
    {
        vm.SelectedLayout = vm.Layouts.First(c => c.Kind == kind);
        vm.SelectedMonitorCount = vm.MonitorCounts[monitors - 1];
        for (var i = 0; i < vm.ClientCount; i++)
        {
            vm.SelectedSlotIndex = i;
            if (vm.SlotName != Demo.Squad[i].Name) vm.SlotName = Demo.Squad[i].Name;
            if (vm.SlotClass.Id != Demo.Squad[i].Class) vm.SlotClass = WowClasses.Find(Demo.Squad[i].Class)!;
        }
    }

    /// <summary>
    /// Re-arranges, then waits until every demo window is in its slot ("Connected"), so the squad shows no
    /// "Placing…" and the activity line is the same on every run.
    /// </summary>
    private static async Task InPlace(MainViewModel vm)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (vm.ArrangeCommand.CanExecute(null)) vm.ArrangeCommand.Execute(null);
            for (var i = 0; i < 40 && (vm.Busy || vm.ConnectedCount < vm.ClientCount); i++) await Task.Delay(250);
            if (!vm.Busy && vm.ConnectedCount == vm.ClientCount) return;
        }
        throw new InvalidOperationException($"Only {vm.ConnectedCount} of {vm.ClientCount} demo windows are in place.");
    }

    private static void Place(MainViewModel vm, int slot, int x, int y, int width, int height)
    {
        vm.SelectedSlotIndex = slot;
        vm.SlotX = x;
        vm.SlotY = y;
        vm.SlotW = width;
        vm.SlotH = height;
    }

    /// <summary>Lets the app place the demo windows (its 1 s tick) and finish layout and data binding.</summary>
    private static async Task Settle()
    {
        await Task.Delay(1600);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static RenderTargetBitmap Render(FrameworkElement element)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Round(element.ActualWidth), (int)Math.Round(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>The welcome screen centred over the dimmed app, as it looks on first start.</summary>
    private static BitmapSource Compose(BitmapSource app, BitmapSource dialog)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(app, new Rect(0, 0, Width, Height));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xB0, 0x05, 0x07, 0x0B)), null, new Rect(0, 0, Width, Height));
            dc.DrawImage(dialog, new Rect(Math.Round((Width - dialog.Width) / 2), Math.Round((Height - dialog.Height) / 2), dialog.Width, dialog.Height));
        }
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>The tray menu (WinForms, drawn by itself) at the bottom right of the app, where Windows opens it.</summary>
    private static BitmapSource WithTrayMenu(BitmapSource app, Forms.ContextMenuStrip menu)
    {
        foreach (var item in menu.Items.OfType<Forms.ToolStripMenuItem>())
        {
            if (item.Text == "Start placing windows") item.Text = "Stop placing windows";   // as it reads while started
            if (item.Text == "Hover to activate windows") item.Checked = true;
        }
        menu.PerformLayout();
        var size = menu.GetPreferredSize(System.Drawing.Size.Empty);
        menu.Size = size;
        using var drawn = new System.Drawing.Bitmap(size.Width, size.Height);
        menu.DrawToBitmap(drawn, new System.Drawing.Rectangle(0, 0, size.Width, size.Height));
        menu.Dispose();
        using var stream = new MemoryStream();
        drawn.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        var picture = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(app, new Rect(0, 0, Width, Height));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x70, 0x05, 0x07, 0x0B)), null, new Rect(0, 0, Width, Height));
            var at = new Rect(Width - picture.PixelWidth - 24, Height - picture.PixelHeight - 40, picture.PixelWidth, picture.PixelHeight);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0)), null, new Rect(at.X + 4, at.Y + 6, at.Width, at.Height));   // a soft drop shadow
            dc.DrawImage(picture, at);
        }
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void Save(BitmapSource bitmap, string path)
    {
        if (bitmap.PixelWidth != Width || bitmap.PixelHeight != Height)
            throw new InvalidOperationException($"{Path.GetFileName(path)} is {bitmap.PixelWidth}×{bitmap.PixelHeight}, not {Width}×{Height}.");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
