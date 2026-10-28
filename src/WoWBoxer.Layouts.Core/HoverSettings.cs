namespace WoWBoxer.Layouts.Core;

/// <summary>
/// Windows' own "Activate a window by hovering over it with the mouse" (per user): on/off, the delay before the window
/// under the pointer is activated, and whether it also comes to the front. A Windows setting – the app never sends input.
/// </summary>
public sealed record HoverSettings(bool Enabled, int DelayMs, bool BringToFront)
{
    public const int MaxDelayMs = 500;
    public const int RecommendedDelayMs = 30;

    /// <summary>The delay slider's range. The user's own value can be larger (set outside the app); only new values are clamped.</summary>
    public static int ClampDelay(int ms) => Math.Clamp(ms, 0, MaxDelayMs);

    public string Describe() => $"{(Enabled ? "on" : "off")}, delay {DelayMs} ms, bring to front {(BringToFront ? "on" : "off")}";

    /// <summary>Restore needed: the app has saved an original and the setting differs from it now.</summary>
    public static bool NeedsRestore(HoverSettings? original, HoverSettings now) => original is not null && original != now;
}
