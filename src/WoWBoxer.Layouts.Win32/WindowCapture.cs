using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// Read-only picture of a window's client area (what the game draws), for checking layouts without asking the user.
/// Tries PrintWindow (works even if the window is covered); falls back to copying the screen if that comes back black.
/// </summary>
public static unsafe partial class WindowCapture
{
    public sealed record Snapshot(int Width, int Height, byte[] Bgra, string Method);

    public static Snapshot? Capture(nint h)
    {
        if (!NativeMethods.GetClientRect(h, out var client)) return null;
        int w = client.Right - client.Left, ht = client.Bottom - client.Top;
        if (w <= 0 || ht <= 0) return null;

        var printed = Grab(w, ht, dc => PrintWindow(h, dc, PW_CLIENTONLY | PW_RENDERFULLCONTENT));
        if (printed is not null && !IsBlack(printed)) return new Snapshot(w, ht, printed, "PrintWindow");

        var origin = new POINT();
        ClientToScreen(h, ref origin);
        var screen = Grab(w, ht, dc =>
        {
            var screenDc = GetDC(0);
            try
            {
                return BitBlt(dc, 0, 0, w, ht, screenDc, origin.X, origin.Y, SRCCOPY);
            }
            finally
            {
                ReleaseDC(0, screenDc);
            }
        });
        return screen is null ? null : new Snapshot(w, ht, screen, "screen copy");
    }

    public static void SavePng(Snapshot shot, string path)
    {
        using var file = File.Create(path);
        PngWriter.Write(file, shot.Width, shot.Height, shot.Bgra);
    }

    private static byte[]? Grab(int w, int h, Func<nint, bool> draw)
    {
        var screenDc = GetDC(0);
        var memDc = CreateCompatibleDC(screenDc);
        var info = new BITMAPINFOHEADER { biSize = sizeof(BITMAPINFOHEADER), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        var bitmap = CreateDIBSection(screenDc, &info, 0, out var bits, 0, 0);
        try
        {
            if (bitmap == 0) return null;
            var old = SelectObject(memDc, bitmap);
            var ok = draw(memDc);
            GdiFlush();
            SelectObject(memDc, old);
            if (!ok) return null;
            var pixels = new byte[w * h * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            if (bitmap != 0) DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(0, screenDc);
        }
    }

    private static bool IsBlack(byte[] bgra)
    {
        for (var i = 0; i < bgra.Length; i += 4 * 97)   // sample; a real game frame is never all black
            if (bgra[i] > 8 || bgra[i + 1] > 8 || bgra[i + 2] > 8) return false;
        return true;
    }

    private const uint PW_CLIENTONLY = 1, PW_RENDERFULLCONTENT = 2, SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PrintWindow(nint hWnd, nint hdc, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint hWnd, ref POINT point);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hWnd, nint hdc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* info, uint usage, out nint bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GdiFlush();

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int srcX, int srcY, uint rop);
}
