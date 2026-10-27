using static WoWBoxer.Layouts.Win32.NativeMethods;

namespace WoWBoxer.Layouts.Win32;

public static class Dpi
{
    /// <summary>
    /// Makes sure this process works in physical pixels on every monitor. The manifest already asks for
    /// Per-Monitor V2; this is the fallback for hosts without our manifest. Returns the awareness in effect.
    /// </summary>
    public static string EnsurePerMonitorV2()
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);   // fails harmlessly if already set
        return GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) switch
        {
            0 => "unaware",
            1 => "system aware",
            2 => "per-monitor aware",
            var other => $"unknown ({other})",
        };
    }
}
