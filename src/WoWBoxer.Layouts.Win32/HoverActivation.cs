using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// Reads and writes Windows' "Activate a window by hovering over it with the mouse" (Ease of Access › Make the mouse
/// easier to use) through SystemParametersInfo. Per user, no admin. Saved to the user profile and announced to all
/// windows, so it applies at once. This changes a Windows setting only – it sends nothing to any window.
/// </summary>
public static unsafe class HoverActivation
{
    public static HoverSettings Read()
    {
        int tracking = 0, front = 0;
        uint delay = 0;
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETACTIVEWINDOWTRACKING, 0, &tracking, 0);
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETACTIVEWNDTRKZORDER, 0, &front, 0);
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETACTIVEWNDTRKTIMEOUT, 0, &delay, 0);
        return new HoverSettings(tracking != 0, (int)delay, front != 0);
    }

    /// <summary>Writes the values that differ from now. False if Windows refused one of them.</summary>
    public static bool Write(HoverSettings wanted)
    {
        var now = Read();
        var ok = true;
        // The SET actions take the value itself in pvParam, not a pointer to it.
        if (wanted.DelayMs != now.DelayMs) ok &= Set(NativeMethods.SPI_SETACTIVEWNDTRKTIMEOUT, (nint)(uint)Math.Max(wanted.DelayMs, 0));
        if (wanted.BringToFront != now.BringToFront) ok &= Set(NativeMethods.SPI_SETACTIVEWNDTRKZORDER, wanted.BringToFront ? 1 : 0);
        if (wanted.Enabled != now.Enabled) ok &= Set(NativeMethods.SPI_SETACTIVEWINDOWTRACKING, wanted.Enabled ? 1 : 0);
        return ok;
    }

    private static bool Set(uint action, nint value) =>
        NativeMethods.SystemParametersInfo(action, 0, (void*)value, NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
}
