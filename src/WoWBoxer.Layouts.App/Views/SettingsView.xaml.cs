using System.IO;
using System.Windows;
using System.Windows.Controls;
using WoWBoxer.Layouts.App.ViewModels;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

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
