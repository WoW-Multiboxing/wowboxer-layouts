using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WoWBoxer.Layouts.App.ViewModels;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App;

public partial class MainWindow : Window, IUserDialogs
{
    public MainViewModel ViewModel { get; }

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        ViewModel = new MainViewModel(settings, this);
        DataContext = ViewModel;
        Canvas.SlotEdited += (_, e) => ViewModel.OnSlotEdited(e.Index, e.Slot);
        // Scroll after the list has processed the change (scrolling inside the event is re-entrant and throws).
        ((INotifyCollectionChanged)ViewModel.Status.Lines).CollectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (StatusList.Items.Count > 0) StatusList.ScrollIntoView(StatusList.Items[^1]);
            });
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        // Enter in a text box commits it (bindings update on lost focus).
        AddHandler(KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key == Key.Enter && e.OriginalSource is TextBox box)
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }), handledEventsToo: true);

        ViewModel.Status.Info("Ready. This app only moves and resizes windows – it never sends keys or clicks to the game.");
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(ViewModel.RefreshMonitors);

    // ---------- IUserDialogs ----------

    public string? Prompt(string title, string message, string initial) => PromptWindow.Show(this, title, message, initial);

    public bool Confirm(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

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

    // ---------- desktop shortcut ----------

    private void OnCreateShortcut(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedProfile is not { } profile || Environment.ProcessPath is not { } exe) return;
        var name = $"{profile.Name} - WoWBoxer";
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProfileStore.FileNameFor(name).Replace(".json", ".lnk"));
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is not available.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = exe;
            link.Arguments = $"--tray --start \"{profile.Name}\"";
            link.WorkingDirectory = Path.GetDirectoryName(exe);
            link.IconLocation = exe + ",0";
            link.Description = $"Start Battle.net and arrange the WoW windows ({profile.Name})";
            link.Save();
            ViewModel.Status.Ok($"Desktop shortcut created: {Path.GetFileName(path)}. It starts \"{profile.Name}\" from the tray.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
        {
            ViewModel.Status.Warn($"Could not create the shortcut: {ex.Message}");
        }
    }
}

/// <summary>A transparent, click-through label over a WoW window (Identify). Never activated, sends nothing to the game.</summary>
internal static class IdentifyOverlay
{
    public static Window Show(PixelRect rect, string number, string caption)
    {
        var accent = new SolidColorBrush(Color.FromRgb(0x4D, 0xA3, 0xFF));
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
                BorderBrush = accent,
                BorderThickness = new Thickness(4),
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x00, 0x00)),
                Child = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x10, 0x14, 0x1C)),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(36, 12, 36, 18),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = number, FontSize = 110, FontWeight = FontWeights.Bold, Foreground = accent, HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock { Text = caption, FontSize = 22, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
                        },
                    },
                },
            },
        };
        // Physical-pixel placement after the handle exists (WPF itself works in DIPs).
        window.SourceInitialized += (_, _) => WindowPlacer.ShowAsOverlay(new WindowInteropHelper(window).Handle, rect);
        window.Show();
        WindowPlacer.ShowAsOverlay(new WindowInteropHelper(window).Handle, rect);
        return window;
    }
}
