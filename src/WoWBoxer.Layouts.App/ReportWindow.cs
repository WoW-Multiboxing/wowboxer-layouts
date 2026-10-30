using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shell;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// "Report a problem": the report the app built (personal names hidden), a "What happened?" box, and Post in Discord –
/// which copies the message to the clipboard, saves the full report as a file and opens the help channel's invite. The
/// user sees and may edit everything first; nothing is ever posted or uploaded by the app.
/// </summary>
internal static class ReportWindow
{
    /// <param name="report">The short report (already redacted); editable in the window.</param>
    /// <param name="saveFull">Saves the full report (also redacted) and returns its path.</param>
    /// <param name="openUrl">Opens the help invite in the browser.</param>
    public static void Show(Window owner, string report, Func<string, string> saveFull, string helpUrl, Action<string> openUrl)
    {
        var res = Application.Current;
        T Res<T>(string key) => (T)res.FindResource(key);

        var what = new TextBox { MinHeight = 54, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 12), MaxLength = 600 };
        System.Windows.Automation.AutomationProperties.SetAutomationId(what, "ReportWhat");
        System.Windows.Automation.AutomationProperties.SetName(what, "What happened?");
        var text = new TextBox
        {
            Text = report,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 11.5,
            Height = 300,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 4, 0, 0),
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(text, "ReportText");
        System.Windows.Automation.AutomationProperties.SetName(text, "Report");

        var done = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), FontSize = 13, Foreground = Res<Brush>("OkBrush"), Visibility = Visibility.Collapsed };
        System.Windows.Automation.AutomationProperties.SetAutomationId(done, "ReportDone");
        var post = new Button { Style = Res<Style>("EnterWorldButton"), Content = new TextBlock { Text = "POST IN DISCORD" }, IsDefault = true, Padding = new Thickness(20, 7, 20, 7), Margin = new Thickness(0, 0, 10, 0) };
        System.Windows.Automation.AutomationProperties.SetAutomationId(post, "ReportPost");
        System.Windows.Automation.AutomationProperties.SetName(post, "Post in Discord");
        var close = new Button { Content = "Close", IsCancel = true, MinWidth = 90, VerticalAlignment = VerticalAlignment.Center };

        var body = new DockPanel { Width = 640 };
        var top = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Report a problem", Style = Res<Style>("H2") },
                new TextBlock
                {
                    Style = Res<Style>("Body"), Margin = new Thickness(0, 6, 0, 12),
                    Text = "Ask in the WoWBoxer Discord (#setup-help). Below is what the app sees, with your Windows user name, WoW account and character names hidden. Read it and change anything you like – nothing leaves your PC until you paste it yourself.",
                },
                new TextBlock { Text = "What happened?", Foreground = Res<Brush>("ParchmentBrush"), FontSize = 13 },
                what,
                new TextBlock { Text = "The report", Foreground = Res<Brush>("ParchmentBrush"), FontSize = 13 },
            },
        };
        var bottom = new StackPanel
        {
            Children =
            {
                done,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0), Children = { post, close } },
            },
        };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        body.Children.Add(top);
        body.Children.Add(bottom);
        body.Children.Add(text);

        var window = new Window
        {
            Title = "Report a problem",
            Owner = owner.IsVisible ? owner : null,
            SizeToContent = SizeToContent.WidthAndHeight,
            MaxHeight = SystemParameters.WorkArea.Height - 20,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner.IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Foreground = Res<Brush>("TextBrush"),
            FontFamily = Res<FontFamily>("BodyFont"),
            Content = new Border
            {
                Background = Res<Brush>("NightBrush"), CornerRadius = new CornerRadius(10), Margin = new Thickness(12),
                Child = new ContentControl { Style = Res<Style>("OrnatePanel"), Padding = new Thickness(26, 22, 26, 22), Content = body },
            },
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(window, "ReportWindow");
        WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 40, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0) });

        post.Click += (_, _) =>
        {
            var message = ProblemReport.ForDiscord(what.Text, text.Text);
            string? file = null;
            try
            {
                file = saveFull(text.Text);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // the clipboard message still works without the file
            }
            try
            {
                Clipboard.SetText(message);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                done.Text = "The clipboard is busy – press Post in Discord again.";
                done.Visibility = Visibility.Visible;
                return;
            }
            openUrl(helpUrl);
            done.Inlines.Clear();
            done.Inlines.Add(new Run("Copied. Paste with Ctrl+V in #setup-help and press Enter."));
            if (file is not null)
            {
                done.Inlines.Add(new LineBreak());
                var link = new Hyperlink(new Run("Show the full report file")) { Foreground = Res<Brush>("GoldHiBrush") };
                link.Click += (_, _) => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"")?.Dispose();
                done.Inlines.Add(new Run("If someone asks for more: drag the full report into Discord. ") { Foreground = Res<Brush>("MutedBrush"), FontSize = 12 });
                done.Inlines.Add(link);
            }
            done.Visibility = Visibility.Visible;
        };
        window.Loaded += (_, _) => what.Focus();
        window.ShowDialog();
    }
}
