using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WoWBoxer.Layouts.App.ViewModels;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    // ---------- window swap: hotkey boxes ("press the keys") ----------

    private void OnHotkeyBoxFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        (DataContext as MainViewModel)?.PauseHotkeysForEditing();   // otherwise Windows would hand a registered combination to the hotkey, not to the box
        box.SetCurrentValue(TextBox.TextProperty, "Press the keys…");   // keeps the binding
    }

    private void OnHotkeyBoxBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box) System.Windows.Data.BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateTarget();
        (DataContext as MainViewModel)?.ResumeHotkeys();
    }

    /// <summary>Reads the combination from the app's own key event (only while this box has focus; no hook).</summary>
    private void OnHotkeyBoxKey(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not HotkeyRow row) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            Keyboard.ClearFocus();
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.ImeProcessed or Key.DeadCharProcessed)
        {
            box.SetCurrentValue(TextBox.TextProperty, ModifierText(Keyboard.Modifiers) + "…");
            return;
        }
        var mods = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Ctrl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;
        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;
        Keyboard.ClearFocus();   // registers again (blur), then the new combination is saved and registered
        row.Capture(new Hotkey(mods, vk).ToString());
    }

    private static string ModifierText(ModifierKeys m) =>
        string.Concat((m.HasFlag(ModifierKeys.Control) ? "Ctrl+" : ""), (m.HasFlag(ModifierKeys.Alt) ? "Alt+" : ""), (m.HasFlag(ModifierKeys.Shift) ? "Shift+" : ""), (m.HasFlag(ModifierKeys.Windows) ? "Win+" : ""));

    // ---------- window swap: custom order chips (drag to move, click to remove) ----------

    private Point _chipDown;
    private OrderChip? _chipDragged;

    private void OnChipDown(object sender, MouseButtonEventArgs e)
    {
        _chipDown = e.GetPosition(this);
        _chipDragged = (sender as FrameworkElement)?.Tag as OrderChip;
    }

    private void OnChipMove(object sender, MouseEventArgs e)
    {
        if (_chipDragged is null || e.LeftButton != MouseButtonState.Pressed) return;
        var d = e.GetPosition(this) - _chipDown;
        if (Math.Abs(d.X) < 6 && Math.Abs(d.Y) < 6) return;
        var chip = _chipDragged;
        _chipDragged = null;
        DragDrop.DoDragDrop((DependencyObject)sender, chip, DragDropEffects.Move);
    }

    private void OnChipUp(object sender, MouseButtonEventArgs e)
    {
        if (_chipDragged is { } chip && DataContext is MainViewModel vm) vm.RemoveFromSwapOrderCommand.Execute(chip);   // a click, not a drag
        _chipDragged = null;
    }

    private void OnChipDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(OrderChip)) is OrderChip from && (sender as FrameworkElement)?.Tag is OrderChip to && DataContext is MainViewModel vm)
            vm.MoveSwapOrderChip(from.Position, to.Position);
    }

    /// <summary>A desktop shortcut that opens the app in the tray and starts the current layout's squad.</summary>
    private void OnCreateShortcut(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { SelectedProfile: { } profile } vm || Environment.ProcessPath is not { } exe) return;
        var name = $"{profile.Name} - WoWBoxer";
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Path.ChangeExtension(ProfileStore.FileNameFor(name), ".lnk"));
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is not available.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = exe;
            link.Arguments = $"--tray --start \"{profile.Name}\"";
            link.WorkingDirectory = Path.GetDirectoryName(exe);
            link.IconLocation = exe + ",0";
            link.Description = $"Open the WoW clients and place their windows ({profile.Name})";
            link.Save();
            vm.Status.Report(Win32.StatusLevel.Ok, $"Desktop shortcut created: {Path.GetFileName(path)}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
        {
            vm.Status.Report(Win32.StatusLevel.Warn, $"Could not create the shortcut: {ex.Message}");
        }
    }
}
