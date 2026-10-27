using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WoWBoxer.Layouts.App;

/// <summary>First start: what the app does and never does (docs/plan.md §2).</summary>
internal static class FirstRunWindow
{
    public const string RulesUrl = "https://github.com/WoW-Multiboxing/wowboxer-layouts#readme";

    public static void Show(Window? owner)
    {
        var ok = new Button { Content = "Got it", IsDefault = true, MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(16, 6, 16, 6) };
        var link = new Hyperlink(new Run("Read the multiboxing rules")) { NavigateUri = new Uri(RulesUrl) };
        link.RequestNavigate += (_, e) => Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();

        TextBlock Line(string text, bool good) => new()
        {
            Text = (good ? "✓  " : "✗  ") + text,
            Foreground = good ? new SolidColorBrush(Color.FromRgb(0x8E, 0xE6, 0xA8)) : new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x9A)),
            Margin = new Thickness(0, 3, 0, 3),
            TextWrapping = TextWrapping.Wrap,
        };

        var window = new Window
        {
            Title = "Welcome to WoWBoxer Window Layouts",
            Owner = owner,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ThemeMode = ThemeMode.Dark,
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            Content = new StackPanel
            {
                Margin = new Thickness(28, 24, 28, 24),
                Children =
                {
                    new TextBlock { Text = "Your WoW windows, where you want them", FontSize = 22, FontWeight = FontWeights.SemiBold },
                    new TextBlock { Text = "For manual multiboxing: one key press = one action on one character.", Opacity = 0.75, Margin = new Thickness(0, 4, 0, 16), TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = "What it does", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) },
                    Line("Starts Battle.net and places each WoW window in your layout as it opens.", true),
                    Line("Moves, resizes and restyles windows – nothing else.", true),
                    Line("Keeps logs on your PC only. No telemetry.", true),
                    new TextBlock { Text = "What it never does", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) },
                    Line("Never sends keys or mouse clicks to the game, never broadcasts input.", false),
                    Line("Never reads game memory, injects code or logs in for you.", false),
                    Line("Never steals focus: you press Play and play yourself.", false),
                    new TextBlock { Margin = new Thickness(0, 16, 0, 0), Inlines = { link } },
                    ok,
                },
            },
        };
        ok.Click += (_, _) => window.Close();
        window.ShowDialog();
    }
}
