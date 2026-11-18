namespace WoWBoxer.Layouts.Core;

/// <summary>Extends a full-size Mainhand display into the adjacent part of one workspace display.</summary>
public static class OffhandSpan
{
    public static bool TryCreate(MonitorInfo main, MonitorInfo companion, IReadOnlyList<MonitorInfo> monitors,
        int workspacePercent, out PixelRect bounds, out string error)
    {
        bounds = default;
        if (main.Index == companion.Index)
        {
            error = "Choose two different displays.";
            return false;
        }
        if (!monitors.Contains(main) || !monitors.Contains(companion))
        {
            error = "The displays have changed. Refresh and choose both displays again.";
            return false;
        }
        if (workspacePercent is < 1 or > 100)
        {
            error = "Choose a workspace share between 1% and 100%.";
            return false;
        }

        var game = main.Bounds;
        var desk = companion.Bounds;
        // Match physical edges in Windows display settings. The game area keeps its native aspect
        // even with mixed DPI, a portrait workspace, or a vertical offset between the displays.
        bool horizontal = (game.Right == desk.X || desk.Right == game.X) && game.Y < desk.Bottom && desk.Y < game.Bottom;
        bool vertical = (game.Bottom == desk.Y || desk.Bottom == game.Y) && game.X < desk.Right && desk.X < game.Right;
        if (!horizontal && !vertical)
        {
            error = "Choose displays that share an edge in Windows Display settings.";
            return false;
        }

        int extra = Geometry.Round((horizontal ? desk.Width : desk.Height) * (workspacePercent / 100.0));
        int gameLength = horizontal ? game.Width : game.Height;
        double fraction = (double)extra / (gameLength + extra);
        if (fraction is < 0.1 or > 0.9)
        {
            error = "Choose another workspace share. The Offhand manual seam accepts 10% to 90% of the combined window.";
            return false;
        }

        var span = horizontal
            ? new PixelRect(desk.X < game.X ? game.X - extra : game.X, game.Y, game.Width + extra, game.Height)
            : new PixelRect(game.X, desk.Y < game.Y ? game.Y - extra : game.Y, game.Width, game.Height + extra);
        if (monitors.Any(m => m.Index != main.Index && m.Index != companion.Index && span.Overlaps(m.Bounds)))
        {
            error = "The window would cover a third display. Choose another pair or a smaller workspace share.";
            return false;
        }

        bounds = span;
        error = "";
        return true;
    }

    /// <summary>Offhand's manual seam is the workspace fraction of the combined window in either orientation.</summary>
    public static string Calibration(MonitorInfo main, MonitorInfo companion, PixelRect bounds)
    {
        var game = main.Bounds;
        bool horizontal = bounds.Width != game.Width;
        string side = horizontal
            ? (game.X < companion.Bounds.X ? "left" : "right")
            : (game.Y < companion.Bounds.Y ? "top" : "bottom");
        int total = horizontal ? bounds.Width : bounds.Height;
        double seam = (total - (horizontal ? game.Width : game.Height)) * 100.0 / total;
        int divisor = (int)System.Numerics.BigInteger.GreatestCommonDivisor(game.Width, game.Height);
        var calibration = FormattableString.Invariant($"Mainhand {side} · workspace seam {seam:0.#}% · aspect {game.Width / divisor}:{game.Height / divisor}");
        var desk = companion.Bounds;
        bool partlyOffscreen = horizontal
            ? bounds.Y < desk.Y || bounds.Bottom > desk.Bottom
            : bounds.X < desk.X || bounds.Right > desk.Right;
        return partlyOffscreen ? calibration + ". Part of the workspace falls outside the selected display." : calibration;
    }
}
