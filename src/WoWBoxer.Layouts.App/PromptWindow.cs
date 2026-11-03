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

    /// <summary>One question, several answers (the first is the default). Returns the button's index, or -1 for Esc / closed.</summary>
    public static int Choose(Window owner, string title, string message, IReadOnlyList<string> buttons)
    {
        var result = -1;
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Window? window = null;
        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var b = new Button { Content = buttons[i], IsDefault = i == 0, MinWidth = 110, Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0) };
            System.Windows.Automation.AutomationProperties.SetAutomationId(b, $"Choice{i}");
            b.Click += (_, _) =>
            {
                result = index;
                window!.Close();
            };
            row.Children.Add(b);
        }
        var body = new StackPanel
        {
            Width = 460,
            Children =
            {
                new TextBlock { Text = title, Style = (Style)owner.FindResource("H2"), Margin = new Thickness(0, 0, 0, 10) },
                new TextBlock { Text = message, Style = (Style)owner.FindResource("Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) },
                row,
            },
        };
        window = Frame(owner, title, body);
        window.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) window.Close();
        };
        window.Loaded += (_, _) => ((Button)row.Children[0]).Focus();
        window.ShowDialog();
        return result;
    }

    /// <summary>
    /// "Paste a layout code": fixed width, the code wraps inside a box that scrolls instead of growing, OK and Cancel always
    /// in view, a Paste button, a live check under the box, Enter = OK when the code is valid.
    /// </summary>
    public static string? AskShareCode(Window owner, Func<string, (bool Ok, string Message)> check)
    {
        var box = new TextBox
        {
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Height = 120,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Segoe UI"),
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 6),
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(box, "ShareCodeText");
        System.Windows.Automation.AutomationProperties.SetName(box, "Layout code");
        var status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, MinHeight = 18, Margin = new Thickness(0, 0, 0, 16) };
        System.Windows.Automation.AutomationProperties.SetAutomationId(status, "ShareCodeCheck");
        var ok = new Button { Content = "OK", MinWidth = 100, Margin = new Thickness(0, 0, 8, 0), IsEnabled = false };
        var cancel = new Button { Content = "Cancel", MinWidth = 100 };
        var paste = new Button { Content = "Paste", MinWidth = 90, ToolTip = "Paste the code from the clipboard" };
        System.Windows.Automation.AutomationProperties.SetAutomationId(ok, "ShareCodeOk");
        System.Windows.Automation.AutomationProperties.SetAutomationId(paste, "ShareCodePaste");
        var valid = false;
        void Check()
        {
            var (good, message) = check(box.Text);
            var empty = box.Text.Trim().Length == 0;
            valid = good;
            ok.IsEnabled = good;
            status.Text = empty ? "Paste a code that starts with WBX1:" : message;
            status.Foreground = (Brush)owner.FindResource(empty ? "MutedBrush" : good ? "OkBrush" : "NoBrush");
        }
        box.TextChanged += (_, _) => Check();
        var buttons = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(paste, Dock.Left);
        buttons.Children.Add(paste);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Children = { ok, cancel } };
        DockPanel.SetDock(right, Dock.Right);
        buttons.Children.Add(right);
        var body = new StackPanel
        {
            Width = 520,
            Children =
            {
                new TextBlock { Text = "Paste a layout code", Style = (Style)owner.FindResource("H2"), Margin = new Thickness(0, 0, 0, 10) },
                new TextBlock { Text = "Paste a WBX1: code from a friend or the website:", Style = (Style)owner.FindResource("Body"), TextWrapping = TextWrapping.Wrap },
                box,
                status,
                buttons,
            },
        };
        var window = Frame(owner, "Paste a layout code", body);
        System.Windows.Automation.AutomationProperties.SetAutomationId(window, "ShareCodeDialog");
        string? result = null;
        void Accept()
        {
            if (!valid) return;
            result = box.Text;
            window.Close();
        }
        ok.Click += (_, _) => Accept();
        cancel.Click += (_, _) => window.Close();
        paste.Click += (_, _) =>
        {
            try
            {
                if (Clipboard.ContainsText()) box.Text = Clipboard.GetText().Trim();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // the clipboard is busy with another program: Ctrl+V still works
            }
            box.Focus();
            box.CaretIndex = box.Text.Length;
        };
        // Enter = OK when the code is valid (never a new line inside the code)
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            e.Handled = true;
            Accept();
        };
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) window.Close();
        };
        window.Loaded += (_, _) =>
        {
            Check();
            box.Focus();
        };
        window.ShowDialog();
        return result;
    }

    /// <summary>The War Table dialog frame around <paramref name="body"/>.</summary>
    private static Window Frame(Window owner, string title, UIElement body)
    {
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
        return window;
    }

    public static bool Confirm(Window owner, string title, string message, string yes, string no) => Dialog(owner, title, message, null, yes, no);

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
