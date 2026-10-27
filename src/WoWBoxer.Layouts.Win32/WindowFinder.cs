using System.Diagnostics;
using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;
using static WoWBoxer.Layouts.Win32.NativeMethods;

namespace WoWBoxer.Layouts.Win32;

/// <summary>A top-level window and the facts we use to recognise and place it.</summary>
public sealed record WindowInfo(nint Handle, uint Pid, string ProcessName, string ClassName, string Title, PixelRect Rect, int Style);

/// <summary>
/// Finds World of Warcraft windows. Rules from the launcher: window class <c>GxWindowClass*</c> (older clients), or a
/// big window of a <c>Wow*.exe</c> process (Classic Era 2026: <c>WowClassic.exe</c>, class "waApplication Window"),
/// or of any game exe inside a "World of Warcraft" folder (fallback for an unknown exe name, e.g. Forever).
/// </summary>
public static unsafe class WindowFinder
{
    private const int MinWidth = 300, MinHeight = 200;

    public static bool IsWowProcessName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        name = name.ToLowerInvariant();
        return name.StartsWith("wow", StringComparison.Ordinal) && !name.Contains("voice") && !name.Contains("error") && !name.Contains("crash");
    }

    public static bool IsWowPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        path = path.ToLowerInvariant();
        if (!path.Contains(@"\world of warcraft\")) return false;
        var file = path[(path.LastIndexOf('\\') + 1)..];
        string[] notGame = ["launcher", "battle.net", "agent", "error", "crash", "voice"];
        return !notGame.Any(file.Contains);
    }

    /// <summary>Visible top-level windows without an owner (real app windows, not tooltips or dialogs).</summary>
    public static List<nint> TopWindows()
    {
        var list = new List<nint>();
        var gc = GCHandle.Alloc(list);
        try
        {
            EnumWindows(&OnWindow, GCHandle.ToIntPtr(gc));
        }
        finally
        {
            gc.Free();
        }
        return list;
    }

    [UnmanagedCallersOnly]
    private static int OnWindow(nint hWnd, nint lParam)
    {
        if (IsWindowVisible(hWnd) && GetWindow(hWnd, GW_OWNER) == 0)
            ((List<nint>)GCHandle.FromIntPtr(lParam).Target!).Add(hWnd);
        return 1;
    }

    /// <summary>WoW game windows, oldest process first (= the order the user pressed Play).</summary>
    public static List<WindowInfo> FindGameWindows()
    {
        var wowPids = new HashSet<uint>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (IsWowProcessName(p.ProcessName)) wowPids.Add((uint)p.Id);
                }
                catch (InvalidOperationException)
                {
                    // exited meanwhile
                }
            }
        }

        var found = new List<(WindowInfo Window, DateTime Start)>();
        foreach (var h in TopWindows())
        {
            var pid = PidOf(h);
            var gx = ClassOf(h).StartsWith("GxWindowClass", StringComparison.Ordinal);
            if (!gx && !wowPids.Contains(pid) && !IsWowPath(ProcessInfo.ImagePath(pid))) continue;
            var rect = RectOf(h);
            if (!gx && (rect.Width < MinWidth || rect.Height < MinHeight)) continue;
            found.Add((Describe(h), ProcessInfo.StartTime(pid) ?? DateTime.MaxValue));
        }
        return found.OrderBy(f => f.Start).ThenBy(f => f.Window.Handle).Select(f => f.Window).ToList();
    }

    public static WindowInfo Describe(nint h)
    {
        var pid = PidOf(h);
        return new WindowInfo(h, pid, ProcessInfo.Name(pid), ClassOf(h), TitleOf(h), RectOf(h), GetWindowLong(h, GWL_STYLE));
    }

    public static uint PidOf(nint h)
    {
        GetWindowThreadProcessId(h, out var pid);
        return pid;
    }

    public static string ClassOf(nint h)
    {
        var buffer = stackalloc char[256];
        var n = GetClassName(h, buffer, 256);
        return new string(buffer, 0, Math.Max(n, 0));
    }

    /// <summary>Reads the title Windows keeps for the window; does not wait for a busy game.</summary>
    public static string TitleOf(nint h)
    {
        var buffer = stackalloc char[512];
        var n = GetWindowText(h, buffer, 512);
        return new string(buffer, 0, Math.Max(n, 0));
    }

    public static PixelRect RectOf(nint h) => GetWindowRect(h, out var r) ? MonitorReader.ToRect(r) : default;

    public static bool Exists(nint h) => IsWindow(h);
}
