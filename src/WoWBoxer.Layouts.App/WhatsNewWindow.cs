using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace WoWBoxer.Layouts.App;

/// <summary>"What's new": shown once on the first start after an update, so the owner can see what changed.</summary>
internal static class WhatsNewWindow
{
    /// <summary>The newest changes, shortest first. Keep it to what a player notices.</summary>
    public static readonly (string Title, string Text)[] Changes =
    [
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

    public static bool ShouldShow(AppSettings settings, string version) =>
        settings.FirstRunDone && !string.Equals(settings.LastSeenVersion, version, StringComparison.Ordinal);

    public static void Show(Window? owner, string version)
    {
        var res = Application.Current;
        Brush R(string key) => (Brush)res.FindResource(key);

        var list = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        foreach (var (title, text) in Changes)
        {
            list.Children.Add(new TextBlock { Text = title, Style = (Style)res.FindResource("H2"), FontSize = 15, Margin = new Thickness(0, 10, 0, 2) });
            list.Children.Add(new TextBlock { Text = text, Foreground = R("TextBrush"), FontSize = 13.5, TextWrapping = TextWrapping.Wrap });
        }

        var ok = new Button
        {
            Style = (Style)res.FindResource("EnterWorldButton"),
            Content = new TextBlock { Text = "CONTINUE" },
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0),
        };
        var content = new StackPanel
        {
            Children =
            {
                new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/app.png")), Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "What's new", Style = (Style)res.FindResource("H1"), FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) },
                new TextBlock { Text = $"WoWBoxer Window Layouts {version}", Foreground = R("GoldBrush"), FontSize = 13.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) },
                new ContentControl { Style = (Style)res.FindResource("Divider"), Margin = new Thickness(100, 16, 100, 0) },
                list,
                ok,
            },
        };
        var window = new Window
        {
            Title = "What's new in WoWBoxer Window Layouts",
            Owner = owner,
            Width = 680,
            SizeToContent = SizeToContent.Height,
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
                Child = new ContentControl { Style = (Style)res.FindResource("OrnatePanel"), Padding = new Thickness(36, 28, 36, 28), Content = content },
            },
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(window, "WhatsNew");
        WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 60, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0) });
        ok.Click += (_, _) => window.Close();
        window.ShowDialog();
    }
}
