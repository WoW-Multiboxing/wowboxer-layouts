using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using WoWBoxer.Layouts.App.ViewModels;

namespace WoWBoxer.Layouts.App;

/// <summary>First start: what the app does and never does (docs/plan.md §2), in the War Table style.</summary>
internal static class FirstRunWindow
{
    /// <summary>"Join the WoWBoxer Discord – tips, layouts and help" as a link (first-run screen, What's new). Never a pop-up.</summary>
    public static Hyperlink DiscordLink()
    {
        var link = new Hyperlink(new Run("Join the WoWBoxer Discord – tips, layouts and help")) { NavigateUri = new Uri(Community.Links.Discord) };
        link.SetResourceReference(TextElement.ForegroundProperty, "GoldHiBrush");
        link.RequestNavigate += (_, e) => Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        return link;
    }

    public static void Show(Window? owner)
    {
        var res = Application.Current;
        Brush R(string key) => (Brush)res.FindResource(key);

        var go = new Button
        {
            Style = (Style)res.FindResource("EnterWorldButton"),
            Content = new TextBlock { Text = "GOT IT – LET'S SET UP" },
            IsDefault = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0),
        };
        var link = new Hyperlink(new Run("Read the multiboxing rules and the full guide")) { NavigateUri = new Uri(MainViewModel.WebsiteGuideUrl) };
        link.RequestNavigate += (_, e) => Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();

        TextBlock Line(string text, bool good) => new()
        {
            Text = (good ? "✓   " : "✗   ") + text,
            Foreground = good ? R("OkBrush") : R("NoBrush"),
            Margin = new Thickness(0, 4, 0, 4),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
        };
        TextBlock Head(string text) => new() { Text = text, Style = (Style)res.FindResource("H2"), Margin = new Thickness(0, 0, 0, 6) };

        var columns = new Grid { Margin = new Thickness(0, 22, 0, 0) };
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        var does = new StackPanel { Children = { Head("What it does"),
            Line("Starts Battle.net and places each WoW window in your layout as it opens.", true),
            Line("Moves, resizes and restyles windows – nothing else.", true),
            Line("Keeps your layouts and logs on your PC. No telemetry.", true) } };
        var never = new StackPanel { Children = { Head("What it never does"),
            Line("Never sends keys or mouse clicks to the game or broadcasts input.", false),
            Line("Never reads game memory, injects code or logs in for you.", false),
            Line("Never takes focus: you press Play and play every character yourself.", false) } };
        Grid.SetColumn(never, 2);
        columns.Children.Add(does);
        columns.Children.Add(never);

        var content = new StackPanel
        {
            Children =
            {
                new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/app.png")), Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Welcome, multiboxer", Style = (Style)res.FindResource("H1"), FontSize = 30, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) },
                new TextBlock { Text = "Your WoW windows, where you want them – one key press, one action, one character.", Foreground = R("MutedBrush"), FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) },
                new ContentControl { Style = (Style)res.FindResource("Divider"), Margin = new Thickness(120, 20, 120, 0) },
                columns,
                new TextBlock { Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13.5, Inlines = { link } },
                new TextBlock { Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13.5, Inlines = { DiscordLink() } },
                go,
            },
        };
        var panel = new ContentControl { Style = (Style)res.FindResource("OrnatePanel"), Padding = new Thickness(40, 32, 40, 32), Content = content };
        var window = new Window
        {
            Title = "Welcome to WoWBoxer Window Layouts",
            Owner = owner,
            Width = 820,
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
                Child = panel,
            },
        };
        WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 60, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0) });
        go.Click += (_, _) => window.Close();
        window.ShowDialog();
    }
}
