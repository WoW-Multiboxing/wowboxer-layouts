using System.Windows;
using System.Windows.Controls;

namespace WoWBoxer.Layouts.App;

/// <summary>A one-line text prompt (rename, paste a WBX1: code).</summary>
internal static class PromptWindow
{
    public static string? Show(Window owner, string title, string message, string initial)
    {
        var box = new TextBox { Text = initial, MinWidth = 380, Margin = new Thickness(0, 8, 0, 16) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90 };
        var window = new Window
        {
            Title = title,
            Owner = owner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            ThemeMode = ThemeMode.Dark,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = message },
                    box,
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } },
                },
            },
        };
        ok.Click += (_, _) => window.DialogResult = true;
        window.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        return window.ShowDialog() == true ? box.Text : null;
    }
}
