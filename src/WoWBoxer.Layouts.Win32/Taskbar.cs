using System.Runtime.InteropServices;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// The Windows taskbar. A visible taskbar stays on top of game windows, so covering its area only makes sense
/// when Windows hides it automatically.
/// </summary>
public static partial class Taskbar
{
    private const uint ABM_GETSTATE = 4;
    private const int ABS_AUTOHIDE = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public nint hWnd;
        public uint uCallbackMessage, uEdge;
        public int left, top, right, bottom;
        public nint lParam;
    }

    [LibraryImport("shell32.dll")]
    private static partial nuint SHAppBarMessage(uint message, ref APPBARDATA data);

    public static bool AutoHides()
    {
        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        return ((int)SHAppBarMessage(ABM_GETSTATE, ref data) & ABS_AUTOHIDE) != 0;
    }
}
