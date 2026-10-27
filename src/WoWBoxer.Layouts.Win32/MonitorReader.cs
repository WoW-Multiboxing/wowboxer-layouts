using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;
using static WoWBoxer.Layouts.Win32.NativeMethods;

namespace WoWBoxer.Layouts.Win32;

public static unsafe class MonitorReader
{
    /// <summary>All monitors in the order Windows reports them (index 1, 2 ...), in physical pixels.</summary>
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var handles = new List<nint>();
        var gc = GCHandle.Alloc(handles);
        try
        {
            EnumDisplayMonitors(0, 0, &OnMonitor, GCHandle.ToIntPtr(gc));
        }
        finally
        {
            gc.Free();
        }

        var monitors = new List<MonitorInfo>();
        foreach (var h in handles)
        {
            var info = new MONITORINFOEXW { cbSize = sizeof(MONITORINFOEXW) };
            if (!GetMonitorInfo(h, &info)) continue;
            var dpi = GetDpiForMonitor(h, 0, out var dpiX, out _) == 0 ? (int)dpiX : 96;   // MDT_EFFECTIVE_DPI
            monitors.Add(new MonitorInfo(
                monitors.Count + 1,
                new string(info.szDevice),
                (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                ToRect(info.rcMonitor),
                ToRect(info.rcWork),
                dpi));
        }
        return monitors;
    }

    [UnmanagedCallersOnly]
    private static int OnMonitor(nint hMonitor, nint hdc, RECT* rect, nint lParam)
    {
        ((List<nint>)GCHandle.FromIntPtr(lParam).Target!).Add(hMonitor);
        return 1;
    }

    internal static PixelRect ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
