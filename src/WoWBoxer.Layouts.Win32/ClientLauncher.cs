using System.Diagnostics;
using Microsoft.Win32;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// Starts WoW clients at their own login screen (no Battle.net auto-login: that would log in the account selected in
/// Battle.net and could disconnect a running client – see docs/launch-research.md). The user logs in to each.
/// Only clients this launcher started can be closed by it.
/// </summary>
public sealed class ClientLauncher
{
    private readonly HashSet<int> _started = [];

    public IReadOnlyCollection<int> Started
    {
        get
        {
            _started.RemoveWhere(id => !IsRunning(id));
            return _started;
        }
    }

    /// <summary>Starts <paramref name="count"/> clients and returns how many started.</summary>
    public int Start(string exe, int count, IStatusSink sink)
    {
        if (Sandbox.Active)
        {
            sink.Info("Demo mode: no WoW client is opened.");
            return 0;
        }
        if (!File.Exists(exe))
        {
            sink.Warn($"Game not found at {exe}. Set the game exe in Settings.");
            return 0;
        }
        var started = 0;
        for (var i = 0; i < count; i++)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
                if (p is null) continue;
                _started.Add(p.Id);
                started++;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                sink.Warn($"Could not start {Path.GetFileName(exe)}: {e.Message}");
                break;
            }
        }
        if (started > 0) sink.Ok($"Opened {started} WoW login screen(s). Log in to each one yourself – the windows are placed as they appear.");
        return started;
    }

    /// <summary>Closes only the clients this launcher started (never the user's own).</summary>
    public int CloseMine(IStatusSink sink)
    {
        var closed = 0;
        foreach (var id in Started.ToList())
        {
            try
            {
                using var p = Process.GetProcessById(id);
                p.Kill();
                closed++;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
            _started.Remove(id);
        }
        sink.Info(closed == 0 ? "No clients started by this app are running." : $"Closed {closed} client(s) this app had started.");
        return closed;
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using var p = Process.GetProcessById(id);
            return !p.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}

/// <summary>Where the WoW client lives: a running client, Blizzard's registry entry, or the default folder.</summary>
public static class GameInstall
{
    /// <summary>The client exe of <paramref name="game"/>. A configured exe of another game is ignored.</summary>
    public static string? FindExe(string? configured, WowGame game = WowGame.ClassicEra)
    {
        if (Sandbox.Active) return Sandbox.GameExe;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured) && GameDetection.GameOf(configured) is var g && (g == game || g == WowGame.Unknown))
            return configured;

        foreach (var w in WindowFinder.FindGameWindows(game))
            if (w.ExePath is { Length: > 0 } running && File.Exists(running)) return running;

        if (GameDetection.InstallOf(game) is not { } install) return null;
        var (folder, exe) = install;

        var roots = new List<string>();
        foreach (var key in new[] { @"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft", @"SOFTWARE\Blizzard Entertainment\World of Warcraft" })
        {
            using var k = Registry.LocalMachine.OpenSubKey(key);
            if (k?.GetValue("InstallPath") is string path && path.Length > 0)
                roots.Add(Path.GetFullPath(Path.Combine(path.TrimEnd('\\'), "..")));   // InstallPath points at _retail_
        }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "World of Warcraft"));
        return roots.Select(r => Path.Combine(r, folder, exe)).FirstOrDefault(File.Exists);
    }

    /// <summary>The client folder (holds WTF) for an exe.</summary>
    public static string? FolderOf(string? exe) => exe is null ? null : Path.GetDirectoryName(exe);

    /// <summary>Accounts and characters from that client's WTF folder (read-only hints).</summary>
    public static IReadOnlyList<WtfAccount> Accounts(string? exe) => Sandbox.Active ? [] : FolderOf(exe) is { } folder ? WtfInfo.Read(folder) : [];
}
