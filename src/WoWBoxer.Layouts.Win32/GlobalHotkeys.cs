using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// Window swap's global hotkeys (Settings › Advanced, off by default). RegisterHotKey asks Windows to send WM_HOTKEY to
/// the app's own window when the combination is pressed; Windows then does not pass that key press to the active window,
/// so the game never sees it. This is not an input hook: the app does not watch, record or send any other key, and it
/// never sends input to any window.
/// </summary>
public static partial class GlobalHotkeys
{
    public const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    public const int ErrorAlreadyRegistered = 1409;

    /// <summary>Registers one hotkey for <paramref name="hwnd"/>. Returns 0 on success, else the Windows error (1409 = taken by another program).</summary>
    public static int Register(nint hwnd, int id, Hotkey key)
    {
        if (Sandbox.Active) return 0;
        return RegisterHotKey(hwnd, id, (uint)key.Modifiers | MOD_NOREPEAT, (uint)key.Key) ? 0 : Marshal.GetLastPInvokeError();
    }

    public static void Unregister(nint hwnd, int id)
    {
        if (!Sandbox.Active) UnregisterHotKey(hwnd, id);
    }

    /// <summary>The window that has the keyboard now (read only; used to pick "the active window's party").</summary>
    public static nint ForegroundWindow() => GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();
}
