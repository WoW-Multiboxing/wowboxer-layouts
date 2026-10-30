using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace WoWBoxer.Layouts.App;

/// <summary>"What's new": shown once on the first start after an update, so the owner can see what changed.</summary>
internal static class WhatsNewWindow
{
    /// <summary>The newest changes first. Keep it to what a player notices; only the first <see cref="Shown"/> are listed.</summary>
    public static readonly (string Title, string Text)[] Changes =
    [
        ("New Help tab",
            "Everything for when something goes wrong, in one place: common problems with their fixes, Report a problem (a short report you see first, pasted by you in #setup-help), Diagnose, the logs and the Discord, YouTube and website links. F1 opens it."),
        ("Up to 10 clients – two parties",
            "Pick 1–10 clients. Clients 1–5 are party 1, 6–10 party 2 (client 6 is its main); Your squad groups the rows by party."),
        ("Layout and monitors: two clear choices",
            "Under the layout cards, Monitors picks 1, 2 or 3 monitors (and which ones). Every layout works across them: Main + side puts the main on monitor 1 and the followers on the others; Free for all lets you drag windows across monitors."),
        ("Hover to activate starts at 30 ms",
            "Turning hover to activate on now sets the delay to 30 ms, the default. Move the slider for faster or slower; Reset to default next to it puts 30 ms back. Closing WoWBoxer turns it off again (Settings can keep it on)."),
        ("Asks to update when it opens",
            "When a new version is out, the app now asks right away: Update now (it opens again in a few seconds) or Later (installs when you quit)."),
        ("Hover to activate: the WoWBoxer way to switch",
            "Point at a window and it becomes active – Windows' own setting, in Your squad, Settings or the tray. It sends nothing to the game; WoWBoxer never sends keys or clicks. Off until you turn it on, and your own Windows values come back with one click, or when you uninstall."),
        ("New website address",
            "The Guide and rules links now open wowboxer.com."),
        ("Start and Stop",
            "The app now opens stopped and leaves every window alone until you press Start placing windows. Stop (next to the main button, or the tray) leaves them alone again. A desktop layout shortcut starts at once; Settings can start it whenever the app opens."),
        ("Plug and play",
            "The app always watches: every WoW window that opens goes straight into the next free slot. Each slot shows Awaiting… until its window is there, then Connected. Nothing to start, no live switch – Pause is in the tray menu."),
        ("One button",
            "\"Open N WoW clients\" opens a login screen for each empty slot (you log in yourself). When all are connected it becomes Re-arrange. \"Open Battle.net instead\" sits next to it."),
        ("Any WoW install",
            "No game picker: Classic Era, Forever, Retail … are found by their install folder. If two games are open, the squad follows the one with the most windows; the other's windows are left alone (Include adds them)."),
        ("Less on screen",
            "Squad rows are one line – ▾ opens class, account, always on top and Identify. Window rules, UI Scale and tools moved to Settings; the activity log opens from the status bar."),
    ];

    /// <summary>How many changes the window lists (the rest is in the CHANGELOG).</summary>
    private const int Shown = 6;

    public static bool ShouldShow(AppSettings settings, string version) =>
        settings.FirstRunDone && !string.Equals(settings.LastSeenVersion, version, StringComparison.Ordinal);

    public static void Show(Window? owner, string version)
    {
        var res = Application.Current;
        Brush R(string key) => (Brush)res.FindResource(key);

        var list = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        foreach (var (title, text) in Changes.Take(Shown))
        {
            list.Children.Add(new TextBlock { Text = title, Style = (Style)res.FindResource("H2"), FontSize = 14, Margin = new Thickness(0, 8, 0, 2) });
            list.Children.Add(new TextBlock { Text = text, Foreground = R("TextBrush"), FontSize = 13, TextWrapping = TextWrapping.Wrap });
        }
        // the list scrolls; the header and Continue always stay in view
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0), Focusable = false };

        var ok = new Button
        {
            Style = (Style)res.FindResource("EnterWorldButton"),
            Content = new TextBlock { Text = "CONTINUE" },
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
        };
        var header = new StackPanel
        {
            Children =
            {
                new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/app.png")), Width = 44, Height = 44, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "What's new", Style = (Style)res.FindResource("H1"), FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) },
                new TextBlock { Text = $"WoWBoxer Window Layouts {version}", Foreground = R("GoldBrush"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) },
                new ContentControl { Style = (Style)res.FindResource("Divider"), Margin = new Thickness(100, 12, 100, 0) },
            },
        };
        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        // one quiet line at the bottom: where to talk about it
        var bottom = new StackPanel { Children = { new TextBlock { Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13, Inlines = { FirstRunWindow.DiscordLink() } }, ok } };
        DockPanel.SetDock(bottom, Dock.Bottom);
        content.Children.Add(header);
        content.Children.Add(bottom);
        content.Children.Add(scroll);
        var window = new Window
        {
            Title = "What's new in WoWBoxer Window Layouts",
            Owner = owner,
            Width = 620,
            SizeToContent = SizeToContent.Height,
            // never taller than the screen (a long list used to push Continue off it)
            MaxHeight = Math.Min(640, SystemParameters.WorkArea.Height - 40),
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Foreground = R("TextBrush"),
            FontFamily = (FontFamily)res.FindResource("BodyFont"),
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            Content = new Border
            {
                Margin = new Thickness(14),
                CornerRadius = new CornerRadius(12),
                Background = R("WarTableBrush"),
                Child = new ContentControl { Style = (Style)res.FindResource("OrnatePanel"), Padding = new Thickness(32, 22, 32, 22), Content = content },
            },
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(window, "WhatsNew");
        WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 60, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0) });
        ok.Click += (_, _) => window.Close();
        window.ShowDialog();
    }
}
