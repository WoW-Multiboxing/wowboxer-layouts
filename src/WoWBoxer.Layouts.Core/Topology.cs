using System.Globalization;

namespace WoWBoxer.Layouts.Core;

/// <summary>
/// The monitor layout the WoWBoxer Team Screen add-on reads when the main window is stretched over two monitors
/// (wowboxer-teamscreen docs/topology.md, schema 1). All numbers are physical pixels. <see cref="Window"/>: the client area
/// of the stretched main window. <see cref="Team"/>: monitor 2's usable area inside it, from the client area's top-left
/// corner (it may reach past the window; the add-on clips it).
/// <see cref="Main"/>: monitor 1's usable area the same way – where an add-on keeps the game world and Blizzard's UI, so
/// nothing is drawn across both monitors.
/// </summary>
public sealed record Topology(int WindowWidth, int WindowHeight, PixelRect Team, PixelRect? Main = null)
{
    public const int Schema = 1;
    /// <summary>The data add-on's folder name under Interface\AddOns.</summary>
    public const string AddOnName = "WoWBoxerTopology";
    /// <summary>The line that marks the add-on as ours: only a folder with it is ever replaced or removed.</summary>
    public const string GeneratedMark = "## X-Generated: WoWBoxer app";

    /// <summary>
    /// The topology for a layout whose main spans two monitors, from the planned window rectangle (visible edges) and the
    /// frame the game loses to (<see cref="LayoutEngine.FrameSize"/>: 1 px side borders, caption + 1 px bottom border).
    /// Null when no slot spans or monitor 2 is not connected.
    /// </summary>
    public static Topology? For(LayoutProfile profile, IReadOnlyList<PlacedSlot> placed, IReadOnlyList<MonitorInfo> monitors)
    {
        if (placed.FirstOrDefault(p => p.Slot.Span && !p.MonitorFallback) is not { } main) return null;
        if (LayoutEngine.SecondScreen(profile, main.Monitor, monitors) is not { } second) return null;
        if (LayoutEngine.SpanArea(profile, main.Slot, main.Monitor, monitors) is not { } span || span != main.Rect) return null;
        var (fw, fh) = LayoutEngine.FrameSize(profile.Options, main.Monitor.Dpi);
        var side = fw / 2;
        int clientX = main.Rect.X + side, clientY = main.Rect.Y + fh - side;
        var (w, h) = LayoutEngine.GameArea(main.Rect, (fw, fh));
        PixelRect Rel(MonitorInfo m)
        {
            var a = m.Area(profile.Options.UseTaskbarArea);
            return new PixelRect(a.X - clientX, a.Y - clientY, a.Width, a.Height);
        }
        return new Topology(w, h, Rel(second), Rel(main.Monitor));
    }

    /// <summary>The add-on's .toc (exactly as the Team Screen's docs give it).</summary>
    public static string TocText() => string.Join("\r\n",
        "## Interface: 16001, 11509, 20505",
        "## Title: WoWBoxer Topology (data)",
        "## Notes: Monitor layout for WoWBoxer add-ons. Written by the WoWBoxer app; data only.",
        GeneratedMark,
        $"{AddOnName}.lua",
        "");

    /// <summary>The add-on's .lua: one assignment, numbers only.</summary>
    public string LuaText() => string.Create(CultureInfo.InvariantCulture,
        $"WoWBoxerTopology = {{ schema = {Schema}, window = {{ w = {WindowWidth}, h = {WindowHeight} }}, team = {{ x = {Team.X}, y = {Team.Y}, w = {Team.Width}, h = {Team.Height} }}{MainText()} }}\r\n");

    private string MainText() => Main is { } m
        ? string.Create(CultureInfo.InvariantCulture, $", main = {{ x = {m.X}, y = {m.Y}, w = {m.Width}, h = {m.Height} }}")
        : "";
}
