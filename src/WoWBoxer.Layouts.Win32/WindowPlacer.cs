using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;
using static WoWBoxer.Layouts.Win32.NativeMethods;

namespace WoWBoxer.Layouts.Win32;

public enum PlaceResult { Gone, AlreadyThere, Requested }

/// <summary>
/// Moves, resizes and restyles windows. Never activates them (SWP_NOACTIVATE) and never waits on a busy
/// game (SWP_ASYNCWINDOWPOS). Sends no input of any kind.
/// </summary>
public static class WindowPlacer
{
    private const int BorderStyles = WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
    private const uint NoActivateAsync = SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS;

    /// <summary>Asks the window to move into <paramref name="rect"/>. Does nothing if it is already there.</summary>
    public static PlaceResult Place(nint h, PixelRect rect, bool borderless, ZOrder z = ZOrder.Normal)
    {
        if (!IsWindow(h)) return PlaceResult.Gone;
        if (IsZoomed(h) || IsIconic(h)) ShowWindowAsync(h, SW_SHOWNOACTIVATE);   // un-maximise / un-minimise without activating
        var styleChanged = borderless ? MakeBorderless(h) : AddFrame(h);
        if (!styleChanged && IsAt(h, rect)) return PlaceResult.AlreadyThere;

        var (after, zFlag) = ZOrderArgs(z);
        var r = WithInvisibleFrame(h, rect);
        SetWindowPos(h, after, r.X, r.Y, r.Width, r.Height, NoActivateAsync | SWP_FRAMECHANGED | SWP_SHOWWINDOW | zFlag);
        return PlaceResult.Requested;
    }

    /// <summary>
    /// Shrinks the window 2 px and back, so WoW gets a fresh resize and redraws at the real size
    /// (fixes UI drawn at the old size, e.g. cut-off action bars). Candidate fix from the launcher.
    /// </summary>
    public static async Task NudgeAsync(nint h, PixelRect rect, CancellationToken cancel = default)
    {
        if (!IsWindow(h)) return;
        const uint flags = NoActivateAsync | SWP_NOZORDER;
        var r = WithInvisibleFrame(h, rect);
        SetWindowPos(h, 0, r.X, r.Y, r.Width - 2, r.Height - 2, flags);
        await Task.Delay(250, cancel).ConfigureAwait(false);
        SetWindowPos(h, 0, r.X, r.Y, r.Width, r.Height, flags);
    }

    /// <summary>Synchronous move for Diagnose. 0 = ok, otherwise the Windows error (5 = access denied, usually admin).</summary>
    public static int TryMove(nint h, PixelRect rect)
    {
        var r = WithInvisibleFrame(h, rect);
        if (SetWindowPos(h, 0, r.X, r.Y, r.Width, r.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW)) return 0;
        var error = Marshal.GetLastPInvokeError();
        return error == 0 ? -1 : error;
    }

    /// <summary>Removes title bar and borders. Returns true if the style changed.</summary>
    public static bool MakeBorderless(nint h)
    {
        var style = GetWindowLong(h, GWL_STYLE);
        var borderless = style & ~BorderStyles;
        if (borderless == style) return false;
        SetWindowLong(h, GWL_STYLE, borderless);
        return true;
    }

    /// <summary>Puts title bar and borders back (style only; the caller applies it with SWP_FRAMECHANGED). True if changed.</summary>
    public static bool AddFrame(nint h)
    {
        var style = GetWindowLong(h, GWL_STYLE);
        if ((style & BorderStyles) == BorderStyles) return false;
        SetWindowLong(h, GWL_STYLE, style | BorderStyles);
        return true;
    }

    /// <summary>Puts title bar and borders back and clears "always on top". Position and size stay as they are.</summary>
    public static bool RestoreFrame(nint h)
    {
        if (!IsWindow(h)) return false;
        var style = GetWindowLong(h, GWL_STYLE);
        SetWindowLong(h, GWL_STYLE, style | BorderStyles);
        return SetWindowPos(h, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_NOOWNERZORDER | NoActivateAsync);
    }

    /// <summary>True if the window's visible edges are at <paramref name="rect"/> (the invisible resize border does not count).</summary>
    public static bool IsAt(nint h, PixelRect rect) => WindowFinder.VisibleRectOf(h) == rect;

    /// <summary>
    /// Slot rectangles describe what the user sees. A window with a resize frame has an invisible border
    /// (about 7 px left, right and bottom on Windows 10/11), so its window rectangle must be that much bigger,
    /// or framed windows end up with gaps between them. Borderless windows have no such border.
    /// </summary>
    internal static PixelRect WithInvisibleFrame(nint h, PixelRect rect)
    {
        if ((GetWindowLong(h, GWL_STYLE) & WS_THICKFRAME) == 0) return rect;
        var window = WindowFinder.RectOf(h);
        var visible = WindowFinder.VisibleRectOf(h);
        if (window == visible || window.Width == 0) return rect;
        int left = visible.X - window.X, top = visible.Y - window.Y;
        int right = window.Right - visible.Right, bottom = window.Bottom - visible.Bottom;
        return new PixelRect(rect.X - left, rect.Y - top, rect.Width + left + right, rect.Height + top + bottom);
    }

    /// <summary>Sets the window title (e.g. "WoW - Paladin"). Gives up after 1 s if the game is busy.</summary>
    public static unsafe bool SetTitle(nint h, string title)
    {
        fixed (char* text = title)
            return SendMessageTimeout(h, WM_SETTEXT, 0, text, SMTO_ABORTIFHUNG, 1000, out _) != 0;
    }

    private static (nint After, uint Flag) ZOrderArgs(ZOrder z) => z switch
    {
        ZOrder.Top => (HWND_TOPMOST, SWP_NOOWNERZORDER),
        ZOrder.Bottom => (HWND_NOTOPMOST, SWP_NOOWNERZORDER),
        _ => (0, SWP_NOZORDER),
    };
}
