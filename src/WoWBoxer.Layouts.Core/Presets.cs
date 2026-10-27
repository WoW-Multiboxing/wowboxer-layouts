namespace WoWBoxer.Layouts.Core;

public enum PresetKind
{
    /// <summary>Main window left at full height, followers in a column (two columns for 5+ followers) on the right.</summary>
    Side,
    /// <summary>Main window fills the monitor; followers float on top in a row along the bottom.</summary>
    Overlay,
    /// <summary>All windows in an even grid.</summary>
    Grid,
    /// <summary>Main window fills one monitor, followers in a grid on the other monitor.</summary>
    Dual,
    /// <summary>A loose cascade to start from; the user drags every window where they want it.</summary>
    Free,
}

/// <summary>
/// Built-in layouts for 2–8 clients. Presets are built for a concrete monitor and stored in %,
/// so the result resolves to whole, edge-sharing pixels on that monitor.
/// </summary>
public static class Presets
{
    public const int MinClients = 2;
    public const int MaxClients = 8;

    /// <summary>Overlay followers: aspect of each window, share of the screen width they may use, gap and margin.</summary>
    private const double OverlayAspect = 1.6;
    private const double OverlayBand = 0.84, OverlayMaxCell = 0.20, OverlayGap = 0.015, OverlayBottomMargin = 0.04;
    private const int OverlayPerRow = 4;
    /// <summary>When scoring grid shapes, width beyond 16:9 counts as wasted.</summary>
    private const double GridMaxUsefulAspect = 16.0 / 9;

    public static string DisplayName(PresetKind kind) => kind switch
    {
        PresetKind.Side => "Main + side",
        PresetKind.Overlay => "Overlay",
        PresetKind.Grid => "Grid",
        PresetKind.Dual => "Main + second monitor",
        PresetKind.Free => "Free for all",
        _ => kind.ToString(),
    };

    /// <param name="monitor">"primary" or a 1-based index: where the main window goes.</param>
    /// <exception cref="ArgumentException">Bad client count, unknown monitor, or Dual with only one monitor.</exception>
    public static LayoutProfile Create(PresetKind kind, int clients, IReadOnlyList<MonitorInfo> monitors,
        string monitor = "primary", LayoutOptions? options = null)
    {
        if (clients is < MinClients or > MaxClients)
            throw new ArgumentOutOfRangeException(nameof(clients), clients, $"Presets support {MinClients}–{MaxClients} clients.");
        options ??= new LayoutOptions();
        var main = Monitors.Find(monitors, monitor) ?? throw new ArgumentException($"Monitor \"{monitor}\" not found.", nameof(monitor));
        var area = main.Area(options.UseTaskbarArea);
        var mainRef = MonitorRef(main);

        var slots = kind switch
        {
            PresetKind.Side => Side(clients, area, options.MinAspect, LayoutEngine.FrameSize(options, main.Dpi), mainRef),
            PresetKind.Overlay => Overlay(clients, area, mainRef),
            PresetKind.Grid => Grid(clients, area, options.MinAspect, mainRef),
            PresetKind.Dual => Dual(clients, main, monitors, options),
            PresetKind.Free => Free(clients, area, mainRef),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return new LayoutProfile { Name = $"{DisplayName(kind)} {clients}", Clients = clients, Preset = kind, Slots = slots, Options = options };
    }

    private static string MonitorRef(MonitorInfo m) => m.IsPrimary ? "primary" : m.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // The launcher's "FillHeight" rule, generalised: the follower column is exactly as wide as a 5:4 window of one
    // row's height, so the followers fill the full height without letterboxing; the main window gets the rest.
    // The column is kept between 25 % and 40 % of the width (50 % for two columns) so the main window stays big.
    private static List<LayoutSlot> Side(int clients, PixelRect area, double minAspect, (int Width, int Height) frame, string monitor)
    {
        var followers = clients - 1;
        var cols = followers <= 4 ? 1 : 2;
        var rows = (followers + cols - 1) / cols;
        // the game area (window minus frame) of one row is exactly minAspect wide
        var cellW = Geometry.Ceiling(minAspect * (Geometry.Round((double)area.Height / rows) - frame.Height)) + frame.Width;
        var block = Math.Clamp(cellW * cols, Geometry.Ceiling(area.Width * 0.25), Geometry.Floor(area.Width * (cols == 1 ? 0.40 : 0.50)));
        var split = area.Width - block;

        var slots = new List<LayoutSlot> { Slot(1, SlotRole.Main, monitor, area, 0, 0, split, area.Height) with { Fit = FitMode.None } };
        for (var i = 0; i < followers; i++)
        {
            int col = i / rows, row = i % rows;
            var x = split + Geometry.Round((double)block * col / cols);
            var right = split + Geometry.Round((double)block * (col + 1) / cols);
            var y = Geometry.Round((double)area.Height * row / rows);
            var bottom = Geometry.Round((double)area.Height * (row + 1) / rows);
            slots.Add(Slot(i + 2, SlotRole.Follower, monitor, area, x, y, right - x, bottom - y) with { Anchor = TowardsMiddle(row, rows) });
        }
        return slots;
    }

    /// <summary>If a follower must shrink, it moves towards the vertical middle (the launcher's Bottom/Top anchors).</summary>
    private static Anchor TowardsMiddle(int row, int rows)
    {
        if (rows == 1) return Anchor.Top;
        if (rows % 2 == 1 && row == rows / 2) return Anchor.Center;
        return row < rows / 2 ? Anchor.Bottom : Anchor.Top;
    }

    private static List<LayoutSlot> Overlay(int clients, PixelRect area, string monitor)
    {
        var followers = clients - 1;
        var perRow = Math.Min(followers, OverlayPerRow);
        var rows = (followers + perRow - 1) / perRow;
        var gap = Geometry.Round(area.Width * OverlayGap);
        var cellW = Math.Min(Geometry.Round(area.Width * OverlayMaxCell), Geometry.Floor((area.Width * OverlayBand - gap * (perRow - 1)) / perRow));
        var cellH = Geometry.Round(cellW / OverlayAspect);
        var margin = Geometry.Round(area.Height * OverlayBottomMargin);

        var slots = new List<LayoutSlot> { Slot(1, SlotRole.Main, monitor, area, 0, 0, area.Width, area.Height) with { Z = ZOrder.Bottom, Fit = FitMode.None } };
        for (var row = 0; row < rows; row++)
        {
            // Row 0 is the bottom row; it is filled first.
            var inRow = Math.Min(perRow, followers - row * perRow);
            var rowWidth = inRow * cellW + (inRow - 1) * gap;
            var x0 = (area.Width - rowWidth) / 2;
            var y = area.Height - margin - (row + 1) * cellH - row * gap;
            for (var i = 0; i < inRow; i++)
                slots.Add(Slot(slots.Count + 1, SlotRole.Follower, monitor, area, x0 + i * (cellW + gap), y, cellW, cellH) with { Z = ZOrder.Top });
        }
        return slots;
    }

    // Main at 55 % of the area, the followers as a staircase of 5:4 windows down the right; everything stays inside.
    private static List<LayoutSlot> Free(int clients, PixelRect area, string monitor)
    {
        var slots = new List<LayoutSlot>
        {
            Slot(1, SlotRole.Main, monitor, area, area.Width * 0.03, area.Height * 0.05, area.Width * 0.55, area.Height * 0.62) with { Fit = FitMode.None },
        };
        var followers = clients - 1;
        var h = Math.Min(area.Height * 0.3, area.Height * 0.9 / Math.Max(followers, 1) * 1.6);
        var w = h * 1.4;
        var stepY = followers > 1 ? (area.Height - h - area.Height * 0.04) / (followers - 1) : 0;
        var stepX = followers > 1 ? Math.Min(area.Width * 0.035, (area.Width * 0.4 - w * 0.2) / followers) : 0;
        for (var i = 0; i < followers; i++)
        {
            var x = Math.Min(area.Width * 0.6 + i * stepX, area.Width - w);
            var y = area.Height * 0.02 + i * stepY;
            slots.Add(Slot(i + 2, SlotRole.Follower, monitor, area, x, Math.Min(y, area.Height - h), w, h) with { Fit = FitMode.None });
        }
        return slots;
    }

    private static List<LayoutSlot> Grid(int clients, PixelRect area, double minAspect, string monitor) =>
        GridCells(clients, area, minAspect)
            .Select((c, i) => Slot(i + 1, i == 0 ? SlotRole.Main : SlotRole.Follower, monitor, area, c.X, c.Y, c.Width, c.Height) with { Anchor = Anchor.Center })
            .ToList();

    private static List<LayoutSlot> Dual(int clients, MonitorInfo main, IReadOnlyList<MonitorInfo> monitors, LayoutOptions options)
    {
        var second = monitors.FirstOrDefault(m => m.Index != main.Index)
            ?? throw new ArgumentException("This preset needs two monitors.", nameof(monitors));
        var mainArea = main.Area(options.UseTaskbarArea);
        var area = second.Area(options.UseTaskbarArea);
        var slots = new List<LayoutSlot> { Slot(1, SlotRole.Main, MonitorRef(main), mainArea, 0, 0, mainArea.Width, mainArea.Height) with { Fit = FitMode.None } };
        slots.AddRange(GridCells(clients - 1, area, options.MinAspect)
            .Select((c, i) => Slot(i + 2, SlotRole.Follower, MonitorRef(second), area, c.X, c.Y, c.Width, c.Height) with { Anchor = Anchor.Center }));
        return slots;
    }

    /// <summary>
    /// Picks the column count that gives the biggest window after the 5:4 fit (fewer empty cells on a tie)
    /// and returns the cells relative to the area. A short last row is centred.
    /// </summary>
    internal static List<PixelRect> GridCells(int count, PixelRect area, double minAspect)
    {
        var (cols, rows) = Enumerable.Range(1, count)
            .Select(c => (cols: c, rows: (count + c - 1) / c))
            .Select(g =>
            {
                double w = (double)area.Width / g.cols, h = (double)area.Height / g.rows;
                if (minAspect > 0 && w / h < minAspect) h = w / minAspect;
                if (w / h > GridMaxUsefulAspect) w = h * GridMaxUsefulAspect;   // a 7:1 strip is not a usable game window
                return (g.cols, g.rows, size: Math.Round(w * h), empty: g.cols * g.rows - count);
            })
            .OrderByDescending(g => g.size).ThenBy(g => g.empty)
            .Select(g => (g.cols, g.rows))
            .First();

        var cells = new List<PixelRect>();
        double cellW = (double)area.Width / cols;
        for (var i = 0; i < count; i++)
        {
            int row = i / cols, col = i % cols;
            var inRow = Math.Min(cols, count - row * cols);
            var offset = (cols - inRow) * cellW / 2;
            var x = Geometry.Round(offset + cellW * col);
            var right = Geometry.Round(offset + cellW * (col + 1));
            var y = Geometry.Round((double)area.Height * row / rows);
            var bottom = Geometry.Round((double)area.Height * (row + 1) / rows);
            cells.Add(new PixelRect(x, y, right - x, bottom - y));
        }
        return cells;
    }

    /// <summary>A slot from pixels relative to the area, stored as % (4 decimals is far below one pixel).</summary>
    private static LayoutSlot Slot(int n, SlotRole role, string monitor, PixelRect area, double x, double y, double w, double h) => new()
    {
        N = n,
        Role = role,
        Monitor = monitor,
        X = Math.Round(Geometry.Percent(x, area.Width), 4),
        Y = Math.Round(Geometry.Percent(y, area.Height), 4),
        W = Math.Round(Geometry.Percent(w, area.Width), 4),
        H = Math.Round(Geometry.Percent(h, area.Height), 4),
    };
}
