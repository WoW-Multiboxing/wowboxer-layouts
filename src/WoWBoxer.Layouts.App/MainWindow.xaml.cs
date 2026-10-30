using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using WoWBoxer.Layouts.App.ViewModels;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App;

public partial class MainWindow : Window, IUserDialogs
{
    public MainViewModel ViewModel { get; }

    public MainWindow(AppSettings settings, bool paused = false)
    {
        InitializeComponent();
        ViewModel = new MainViewModel(settings, this, paused);
        ViewModel.FirstRunRequested += () => FirstRunWindow.Show(this);
        ViewModel.ReportRequested += () => ReportWindow.Show(this, ViewModel.BuildReport(), ViewModel.SaveFullReport, Community.Links.DiscordHelp, url => ViewModel.OpenUrlCommand.Execute(url));
        DataContext = ViewModel;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        StateChanged += (_, _) => FitMaximised();

        // Enter in a text box commits it (bindings update on lost focus).
        AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key == Key.Enter && e.OriginalSource is TextBox box)
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }), handledEventsToo: true);

        // Each page fades and slides in when its tab is chosen.
        foreach (FrameworkElement page in Pages.Children)
            page.IsVisibleChanged += (s, e) =>
            {
                if (e.NewValue is true && ViewModel.Animations) AnimateIn((FrameworkElement)s);
            };

        ViewModel.Status.Info("Ready. This app only moves and resizes windows – it never sends keys or clicks to the game.");

        // The activity log shows its newest line. Scroll after the list has processed the change (re-entrant otherwise).
        ((System.Collections.Specialized.INotifyCollectionChanged)ViewModel.Status.Lines).CollectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (StatusList.Items.Count > 0) StatusList.ScrollIntoView(StatusList.Items[^1]);
            });

        // While started: new windows go to their slot, rows, statuses, "on top" and Auto-fit follow what happens while
        // the app runs, also in the tray. Skipped while a squad row is dragged.
        var live = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        live.Tick += (_, _) =>
        {
            if (!Views.SetupView.Dragging) ViewModel.Tick();
        };
        live.Start();
        Closed += (_, _) => live.Stop();
    }

    private static void AnimateIn(FrameworkElement page)
    {
        var shift = new TranslateTransform(0, 14);
        page.RenderTransform = shift;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
    }

    /// <summary>A custom-chrome window overhangs the screen when maximised; pad it back inside.</summary>
    private void FitMaximised()
    {
        var t = SystemParameters.WindowResizeBorderThickness;
        Frame.Margin = WindowState == WindowState.Maximized ? new Thickness(t.Left + 2, t.Top + 2, t.Right + 2, t.Bottom + 2) : new Thickness(0);
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnActivityClosed(object? sender, EventArgs e) => ActivityToggle.IsChecked = false;

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(ViewModel.RefreshMonitors);

    // ---------- IUserDialogs ----------

    public string? Prompt(string title, string message, string initial) => PromptWindow.Show(this, title, message, initial);

    public bool Confirm(string title, string message) => PromptWindow.Confirm(this, title, message);

    public string? OpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    public string? SaveFile(string filter, string fileName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    public void CopyToClipboard(string text) => Clipboard.SetText(text);

    /// <summary>Big click-through numbers on top of each WoW window for a few seconds. They never take focus.</summary>
    public void ShowIdentify(IReadOnlyList<(PixelRect Rect, string Number, string Caption)> labels)
    {
        var overlays = labels.Select(l => IdentifyOverlay.Show(l.Rect, l.Number, l.Caption)).ToList();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var o in overlays) o.Close();
        };
        timer.Start();
    }
}

/// <summary>A transparent, click-through label over a WoW window (Identify). Never activated, sends nothing to the game.</summary>
internal static class IdentifyOverlay
{
    public static Window Show(PixelRect rect, string number, string caption)
    {
        var gold = (Brush)Application.Current.FindResource("GoldHiBrush");
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Content = new Border
            {
                BorderBrush = gold,
                BorderThickness = new Thickness(3),
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x00, 0x00)),
                Child = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Color.FromArgb(0xE8, 0x0B, 0x0E, 0x14)),
                    BorderBrush = (Brush)Application.Current.FindResource("GoldBrush"),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(40, 10, 40, 18),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = number, FontSize = 110, Foreground = gold, HorizontalAlignment = HorizontalAlignment.Center,
                                FontFamily = (FontFamily)Application.Current.FindResource("DisplayFont") },
                            new TextBlock { Text = caption, FontSize = 22, Foreground = (Brush)Application.Current.FindResource("ParchmentBrush"), HorizontalAlignment = HorizontalAlignment.Center,
                                FontFamily = (FontFamily)Application.Current.FindResource("DisplaySemiFont") },
                        },
                    },
                },
            },
        };
        window.Show();
        WindowPlacer.ShowAsOverlay(new WindowInteropHelper(window).Handle, rect);   // physical pixels, click-through, never activated
        return window;
    }
}
