using System.Diagnostics;
using Microsoft.Win32;

namespace WoWBoxer.Layouts.Win32;

/// <summary>Finds and starts the Battle.net app. The user still picks the account and presses Play.</summary>
public static class BattleNet
{
    private const string ProcessName = "Battle.net";

    public static bool IsRunning()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    public static string? FindExe(string? configured = null)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Battle.net", "Battle.net.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Battle.net", "Battle.net.exe"),
        };
        foreach (var key in new[]
                 {
                     @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net",
                     @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net",
                 })
        {
            using var k = Registry.LocalMachine.OpenSubKey(key);
            if (k?.GetValue("InstallLocation") is string location && location.Length > 0)
                candidates.Add(Path.Combine(location, "Battle.net.exe"));
        }
        return candidates.FirstOrDefault(File.Exists);
    }

    public static void Start(string exe) =>
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) })?.Dispose();
}
