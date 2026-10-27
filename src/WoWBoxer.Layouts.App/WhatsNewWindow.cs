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
        ("Always on top works both ways",
            "The main window can float on top now, and a window you take off \"on top\" really comes down again – at once, on Arrange, on Start squad and when the app starts."),
        ("Only the selected game is arranged",
            "Each window's game is read from its install folder (_classic_era_, _forever_ …). With Classic Era selected, a Forever window is left alone – and each game keeps its own layouts."),
        ("Action bar: the real state",
            "The action-bar mark on each window is measured on the open game window (its real size and UI Scale), not guessed from the layout. \"Fix action bar\" tells you exactly what to change, or writes the UI Scale for you while WoW is closed."),
        ("You can see the version",
            "The version is shown in the title bar and on About, and this list appears once after each update."),
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
