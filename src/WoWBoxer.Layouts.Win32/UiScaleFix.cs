using System.Diagnostics;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// Writes WoW's UI Scale into the install's shared <c>WTF\Config.wtf</c> – only while no client of that install runs,
/// because every client rewrites the file when it exits. Changes nothing else in the file and keeps a one-time backup
/// (<c>Config.wtf.wowboxer-backup</c>). Never touches a running game.
/// </summary>
public static class UiScaleFix
{
    public const string BackupSuffix = ".wowboxer-backup";

    /// <summary>Running game clients whose exe lives in <paramref name="installFolder"/>.</summary>
    public static int RunningClients(string installFolder)
    {
        var folder = Path.GetFullPath(installFolder).TrimEnd('\\');
        var count = 0;
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                string name;
                try
                {
                    name = p.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                if (!GameDetection.IsWowProcessName(name)) continue;
                var path = ProcessInfo.ImagePath((uint)p.Id);
                if (path.Length > 0 && string.Equals(Path.GetDirectoryName(path)?.TrimEnd('\\'), folder, StringComparison.OrdinalIgnoreCase)) count++;
            }
        }
        return count;
    }

    public static UiScaleSetting Read(string exe) => UiScale.ReadFile(WowConfig.ConfigPathFor(exe));

    /// <summary>Sets "Use UI Scale" and the scale. Returns null on success, otherwise why it did not write.</summary>
    public static string? Write(string exe, bool use, double scale)
    {
        var folder = Path.GetDirectoryName(exe) ?? "";
        var running = RunningClients(folder);
        if (running > 0) return $"{running} client(s) of this WoW install are running. WoW rewrites Config.wtf when it closes, so close them first.";
        var path = WowConfig.ConfigPathFor(exe);
        try
        {
            var text = File.Exists(path) ? File.ReadAllText(path) : "";
            var backup = path + BackupSuffix;
            if (File.Exists(path) && !File.Exists(backup)) File.Copy(path, backup);
            var updated = UiScale.WithScale(text, use, Math.Clamp(scale, UiScale.Min, UiScale.Max));
            if (updated != text) File.WriteAllText(path, updated);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"Could not write {path}: {e.Message}";
        }
    }
}
