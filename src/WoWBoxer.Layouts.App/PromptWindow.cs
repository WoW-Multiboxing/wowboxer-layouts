using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace WoWBoxer.Layouts.App;

/// <summary>Small dialogs in the War Table style: a one-line prompt and a yes/no question.</summary>
internal static class PromptWindow
{
    public static string? Show(Window owner, string title, string message, string initial)
    {
        var box = new TextBox { Text = initial, MinWidth = 420, Margin = new Thickness(0, 10, 0, 18) };
        System.Windows.Automation.AutomationProperties.SetAutomationId(box, "PromptText");
        var result = Dialog(owner, title, message, box, "OK", "Cancel");
        return result ? box.Text : null;
    }

    public static bool Confirm(Window owner, string title, string message) => Dialog(owner, title, message, null, "Yes", "No");

    private static bool Dialog(Window owner, string title, string message, UIElement? input, string yes, string no)
    {
        var ok = new Button { Content = yes, IsDefault = true, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = no, IsCancel = true, MinWidth = 100 };
        var body = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = title, Style = (Style)owner.FindResource("H2"), Margin = new Thickness(0, 0, 0, 10) },
                new TextBlock { Text = message, Style = (Style)owner.FindResource("Body") },
            },
        };
        if (input is not null) body.Children.Add(input);
        else ((FrameworkElement)body.Children[1]).Margin = new Thickness(0, 0, 0, 20);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } });

        var panel = new ContentControl { Style = (Style)owner.FindResource("OrnatePanel"), Padding = new Thickness(26, 22, 26, 22), Content = body };
        var window = new Window
        {
            Title = title,
            Owner = owner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Foreground = (Brush)owner.FindResource("TextBrush"),
            FontFamily = (FontFamily)owner.FindResource("BodyFont"),
            Content = new Border { Background = (Brush)owner.FindResource("NightBrush"), CornerRadius = new CornerRadius(10), Margin = new Thickness(12), Child = panel },
        };
        WindowChrome.SetWindowChrome(window, new WindowChrome { CaptionHeight = 40, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0) });
        ok.Click += (_, _) => window.DialogResult = true;
        window.Loaded += (_, _) =>
        {
            if (input is TextBox t)
            {
                t.Focus();
                t.SelectAll();
            }
            else ok.Focus();
        };
        return window.ShowDialog() == true;
    }
}
