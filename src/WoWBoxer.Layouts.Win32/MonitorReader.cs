using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;
using static WoWBoxer.Layouts.Win32.NativeMethods;

namespace WoWBoxer.Layouts.Win32;

public static unsafe class MonitorReader
{
    /// <summary>All monitors in the order Windows reports them (index 1, 2 ...), in physical pixels.</summary>
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        if (Sandbox.Active) return Sandbox.Monitors;
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
            var device = new string(info.szDevice);
            monitors.Add(new MonitorInfo(
                monitors.Count + 1,
                device,
                (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                ToRect(info.rcMonitor),
                ToRect(info.rcWork),
                dpi) { DeviceId = DeviceIdOf(device) });
        }
        return monitors;
    }

    /// <summary>The screen's device interface name (\\?\DISPLAY#…), "" if Windows does not say.</summary>
    private static string DeviceIdOf(string device)
    {
        var dd = new DISPLAY_DEVICEW { cb = sizeof(DISPLAY_DEVICEW) };
        return EnumDisplayDevices(device, 0, &dd, EDD_GET_DEVICE_INTERFACE_NAME) ? new string(dd.DeviceID) : "";
    }

    [UnmanagedCallersOnly]
    private static int OnMonitor(nint hMonitor, nint hdc, RECT* rect, nint lParam)
    {
        ((List<nint>)GCHandle.FromIntPtr(lParam).Target!).Add(hMonitor);
        return 1;
    }

    internal static PixelRect ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
