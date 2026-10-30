using System.Diagnostics;
using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;
using static WoWBoxer.Layouts.Win32.NativeMethods;

namespace WoWBoxer.Layouts.Win32;

/// <summary>A top-level window and the facts we use to recognise and place it.</summary>
public sealed record WindowInfo(nint Handle, uint Pid, string ProcessName, string ClassName, string Title, PixelRect Rect, int Style)
{
    /// <summary>Full path of the client exe ("" if Windows does not tell us).</summary>
    public string ExePath { get; init; } = "";
    /// <summary>Which WoW game the window belongs to, from its install folder.</summary>
    public WowGame Game => GameDetection.GameOf(ExePath);
}

/// <summary>
/// Finds World of Warcraft windows. Rules from the launcher: window class <c>GxWindowClass*</c> (older clients), or a
/// big window of a <c>Wow*.exe</c> process (Classic Era 2026: <c>WowClassic.exe</c>, class "waApplication Window"),
/// or of any game exe inside a "World of Warcraft" folder (fallback for an unknown exe name, e.g. Forever).
/// </summary>
public static unsafe class WindowFinder
{
    private const int MinWidth = 300, MinHeight = 200;

    public static bool IsWowProcessName(string? name) => GameDetection.IsWowProcessName(name);

    public static bool IsWowPath(string? path) => GameDetection.IsWowPath(path);

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

    /// <summary>
    /// WoW game windows, oldest process first (= the order the user pressed Play). With <paramref name="game"/> only
    /// that game's clients (e.g. a Forever window is never pulled into a Classic Era layout).
    /// </summary>
    public static List<WindowInfo> FindGameWindows(WowGame? game = null)
    {
        if (Sandbox.Active) return Sandbox.GameWindows().Where(w => game is not { } g || w.Game == g).ToList();
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
        var self = (uint)Environment.ProcessId;
        foreach (var h in TopWindows())
        {
            var pid = PidOf(h);
            if (pid == self) continue;   // never our own windows (app, Identify overlays)
            var gx = ClassOf(h).StartsWith("GxWindowClass", StringComparison.Ordinal);
            if (!gx && !wowPids.Contains(pid) && !IsWowPath(ProcessInfo.ImagePath(pid))) continue;
            var rect = RectOf(h);
            if (!gx && (rect.Width < MinWidth || rect.Height < MinHeight)) continue;
            var info = Describe(h);
            if (game is { } g && info.Game != g) continue;
            found.Add((info, ProcessInfo.StartTime(pid) ?? DateTime.MaxValue));
        }
        return found.OrderBy(f => f.Start).ThenBy(f => f.Window.Handle).Select(f => f.Window).ToList();
    }

    public static WindowInfo Describe(nint h)
    {
        var pid = PidOf(h);
        return new WindowInfo(h, pid, ProcessInfo.Name(pid), ClassOf(h), TitleOf(h), RectOf(h), GetWindowLong(h, GWL_STYLE)) { ExePath = ProcessInfo.ImagePath(pid) };
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

    public static PixelRect RectOf(nint h) => Sandbox.Active ? Sandbox.RectOf(h) : GetWindowRect(h, out var r) ? MonitorReader.ToRect(r) : default;

    /// <summary>The size the game actually renders at (inside title bar and borders).</summary>
    public static (int Width, int Height) ClientSizeOf(nint h) => Sandbox.Active ? (Sandbox.RectOf(h).Width, Sandbox.RectOf(h).Height) : GetClientRect(h, out var r) ? (r.Right - r.Left, r.Bottom - r.Top) : default;

    /// <summary>The window as the user sees it (no invisible resize borders). Falls back to <see cref="RectOf"/>.</summary>
    public static unsafe PixelRect VisibleRectOf(nint h) => Sandbox.Active ? Sandbox.RectOf(h) :
        DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, sizeof(RECT)) == 0 ? MonitorReader.ToRect(r) : RectOf(h);

    public static bool Exists(nint h) => Sandbox.Active ? Sandbox.IsFake(h) : IsWindow(h);
}
