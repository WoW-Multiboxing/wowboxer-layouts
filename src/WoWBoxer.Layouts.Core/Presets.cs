namespace WoWBoxer.Layouts.Core;

public enum PresetKind
{
    /// <summary>
    /// Main window big, followers in a column beside it. On 2+ monitors the main fills monitor 1 and the followers
    /// spread over the others. 6–10 clients: one such block per party.
    /// </summary>
    Side,
    /// <summary>Main window fills the monitor; followers float on top in a row along the bottom (one row per party).</summary>
    Overlay,
    /// <summary>All windows in an even grid, spread over the chosen monitors.</summary>
    Grid,
    /// <summary>Older layouts only (the "2 monitors" card): now <see cref="Side"/> on two monitors.</summary>
    Dual,
    /// <summary>A loose start spread over the chosen monitors; the user drags every window where they want it.</summary>
    Free,
}

/// <summary>How the two parties (6–10 clients) share one monitor: one half each.</summary>
public enum PartySplit { SideBySide, Stacked }

/// <summary>
/// Built-in layouts for 1–10 clients on one or more chosen monitors (1 = a single window filling the monitor).
/// Clients 1–5 are party 1, 6–10 party 2, with client 6 as party 2's main. Presets are built for concrete monitors and
/// stored in %, so the result resolves to whole, edge-sharing pixels there.
/// </summary>
public static class Presets
{
    public const int MinClients = 1;
    public const int MaxClients = 10;
    /// <summary>Clients per party: 1–5 are party 1, 6–10 party 2.</summary>
    public const int PartySize = 5;

    /// <summary>Overlay followers: aspect of each window, share of the screen width they may use, gap and margin.</summary>
    private const double OverlayAspect = 1.6;
    private const double OverlayBand = 0.84, OverlayMaxCell = 0.20, OverlayGap = 0.015, OverlayBottomMargin = 0.04;
    private const int OverlayPerRow = 4;
    /// <summary>When scoring grid shapes, width beyond 16:9 counts as wasted.</summary>
    private const double GridMaxUsefulAspect = 16.0 / 9;

    /// <summary>The layouts the app offers, in its order.</summary>
    public static IReadOnlyList<PresetKind> Offered { get; } = [PresetKind.Side, PresetKind.Overlay, PresetKind.Grid, PresetKind.Free];

    public static string DisplayName(PresetKind kind) => kind switch
    {
        PresetKind.Side or PresetKind.Dual => "Main + side",
        PresetKind.Overlay => "Overlays",
        PresetKind.Grid => "Grid",
        PresetKind.Free => "Free for all",
        _ => kind.ToString(),
    };

    /// <summary>1 for clients 1–5, 2 for clients 6–10 (slot numbers are 1-based).</summary>
    public static int PartyOf(int n) => n <= PartySize ? 1 : 2;

    /// <summary>Slot 1 is the main; with more than 5 clients slot 6 is party 2's main.</summary>
    public static SlotRole RoleOf(int n, int clients) => n == 1 || (clients > PartySize && n == PartySize + 1) ? SlotRole.Main : SlotRole.Follower;

    /// <summary>One monitor (the CLI's <c>--monitor</c>): the layout on that monitor alone. <see cref="PresetKind.Dual"/> adds the next one.</summary>
    /// <param name="monitor">"primary" or a 1-based index: where the main window goes.</param>
    /// <exception cref="ArgumentException">Bad client count, unknown monitor, or Dual with only one monitor.</exception>
    public static LayoutProfile Create(PresetKind kind, int clients, IReadOnlyList<MonitorInfo> monitors,
        string monitor = "primary", LayoutOptions? options = null)
    {
        var main = Monitors.Find(monitors, monitor) ?? throw new ArgumentException($"Monitor \"{monitor}\" not found.", nameof(monitor));
        if (kind != PresetKind.Dual) return ForScreens(kind, clients, [main], options);
        var second = ScreenSet.Ordered(monitors).FirstOrDefault(m => m.Index != main.Index)
            ?? throw new ArgumentException("This preset needs two monitors.", nameof(monitors));
        return ForScreens(PresetKind.Side, clients, [main, second], options);
    }

    /// <summary>The layout across the chosen monitors, in order (monitor 1 holds the main).</summary>
    /// <param name="split">6–10 clients on one monitor: how the parties share it; null = the better one (<see cref="BestSplit"/>).</param>
    /// <param name="followersOnSecond">Overlays with up to 5 clients: the followers' row goes on monitor 2.</param>
    public static LayoutProfile ForScreens(PresetKind kind, int clients, IReadOnlyList<MonitorInfo> screens, LayoutOptions? options = null,
        PartySplit? split = null, bool followersOnSecond = false)
    {
        if (clients is < MinClients or > MaxClients)
            throw new ArgumentOutOfRangeException(nameof(clients), clients, $"Presets support {MinClients}–{MaxClients} clients.");
        if (screens.Count == 0) throw new ArgumentException("No monitor chosen.", nameof(screens));
        if (kind == PresetKind.Dual) kind = PresetKind.Side;
        options ??= new LayoutOptions();

        // One client: a plain single-window setup (not multiboxing) – the window fills monitor 1, whatever the layout.
        var slots = clients == 1
            ? [Slot(1, SlotRole.Main, screens[0], options, Whole(screens[0], options)) with { Fit = FitMode.None, Z = ZOrder.Normal }]
            : kind switch
            {
                PresetKind.Side => Side(clients, screens, options, split),
                PresetKind.Overlay => Overlay(clients, screens, options, followersOnSecond),
                PresetKind.Grid => Spread(Enumerable.Range(1, clients).ToArray(), screens, options, clients).ToList(),
                PresetKind.Free => Free(clients, screens, options),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        return new LayoutProfile
        {
            Name = $"{DisplayName(kind)} {clients}",
            Clients = clients,
            Preset = kind,
            Slots = slots.OrderBy(s => s.N).ToList(),
            Options = options,
            Screens = screens.Select(ScreenSet.ToRef).ToList(),
            Split = split,
            FollowersOnSecond = followersOnSecond && kind == PresetKind.Overlay,
        };
    }

    /// <summary>Whether this layout splits one monitor between two parties (and so has a split to flip).</summary>
    public static bool HasSplit(PresetKind kind, int clients, int screens) => kind is PresetKind.Side or PresetKind.Dual && clients > PartySize && screens == 1;

    /// <summary>
    /// The split of one monitor between the two parties that gives the bigger game windows overall (the product of every
    /// window's usable game area, so neither the mains nor the followers are sacrificed).
    /// </summary>
    public static PartySplit BestSplit(int clients, MonitorInfo screen, LayoutOptions options)
    {
        var area = screen.Area(options.UseTaskbarArea);
        var frame = LayoutEngine.FrameSize(options, screen.Dpi);
        var minAspect = options.MinAspect > 0 ? options.MinAspect : Geometry.DefaultMinAspect;
        double Score(PartySplit split)
        {
            var parties = Parties(clients);
            var (a, b) = Halves(screen, options, split);
            return SideBlock(parties[0], screen, a, options, clients).Concat(SideBlock(parties[1], screen, b, options, clients)).Sum(s =>
            {
                var (w, h) = LayoutEngine.GameArea(LayoutEngine.Resolve(s, area, options, screen.Dpi), frame);
                if (w <= 0 || h <= 0) return -100;
                double gw = w, gh = h;
                if (gw / gh < minAspect) gh = gw / minAspect;   // a too-narrow window letterboxes
                if (gw / gh > GridMaxUsefulAspect) gw = gh * GridMaxUsefulAspect;
                return Math.Log(gw * gh);
            });
        }
        return Score(PartySplit.SideBySide) >= Score(PartySplit.Stacked) ? PartySplit.SideBySide : PartySplit.Stacked;
    }

    private static IReadOnlyList<int[]> Parties(int clients) => clients <= PartySize
        ? [Enumerable.Range(1, clients).ToArray()]
        : [Enumerable.Range(1, PartySize).ToArray(), Enumerable.Range(PartySize + 1, clients - PartySize).ToArray()];

    private static PixelRect Whole(MonitorInfo m, LayoutOptions o) => new(0, 0, m.Area(o.UseTaskbarArea).Width, m.Area(o.UseTaskbarArea).Height);

    /// <summary>Two halves of a monitor (relative to its area).</summary>
    private static (PixelRect A, PixelRect B) Halves(MonitorInfo m, LayoutOptions o, PartySplit split)
    {
        var a = Whole(m, o);
        if (split == PartySplit.SideBySide)
        {
            var half = Geometry.Round(a.Width / 2.0);
            return (a with { Width = half }, new PixelRect(half, 0, a.Width - half, a.Height));
        }
        var mid = Geometry.Round(a.Height / 2.0);
        return (a with { Height = mid }, new PixelRect(0, mid, a.Width, a.Height - mid));
    }

    // ---------- Main + side ----------

    private static List<LayoutSlot> Side(int clients, IReadOnlyList<MonitorInfo> screens, LayoutOptions o, PartySplit? split)
    {
        var parties = Parties(clients);
        if (parties.Count == 1) return SideOn(parties[0], screens, o, clients).ToList();
        if (screens.Count == 1)
        {
            var (a, b) = Halves(screens[0], o, split ?? BestSplit(clients, screens[0], o));
            return [.. SideBlock(parties[0], screens[0], a, o, clients), .. SideBlock(parties[1], screens[0], b, o, clients)];
        }
        // 2+ monitors: each party gets its own (party 1 the first half of them, rounded up).
        var first = (screens.Count + 1) / 2;
        return [.. SideOn(parties[0], screens.Take(first).ToList(), o, clients), .. SideOn(parties[1], screens.Skip(first).ToList(), o, clients)];
    }

    /// <summary>One party on its monitors: one monitor = the side block; more = the main fills the first, followers spread over the rest.</summary>
    private static IEnumerable<LayoutSlot> SideOn(int[] members, IReadOnlyList<MonitorInfo> screens, LayoutOptions o, int clients)
    {
        if (screens.Count == 1) return SideBlock(members, screens[0], Whole(screens[0], o), o, clients);
        var main = Slot(members[0], SlotRole.Main, screens[0], o, Whole(screens[0], o)) with { Fit = FitMode.None };
        return Spread(members[1..], screens.Skip(1).ToList(), o, clients).Prepend(main);
    }

    // The launcher's "FillHeight" rule, generalised: the follower column is exactly as wide as a 5:4 window of one
    // row's height, so the followers fill the full height without letterboxing; the main window gets the rest.
    // The column is kept between 25 % and 40 % of the block's width (50 % for two columns) so the main window stays big.
    private static IEnumerable<LayoutSlot> SideBlock(int[] members, MonitorInfo m, PixelRect block, LayoutOptions o, int clients)
    {
        var frame = LayoutEngine.FrameSize(o, m.Dpi);
        var followers = members.Length - 1;
        if (followers == 0)
        {
            yield return Slot(members[0], RoleOf(members[0], clients), m, o, block) with { Fit = FitMode.None };
            yield break;
        }
        var cols = followers <= 4 ? 1 : 2;
        var rows = (followers + cols - 1) / cols;
        // the game area (window minus frame) of one row is exactly minAspect wide
        var cellW = Geometry.Ceiling(o.MinAspect * (Geometry.Round((double)block.Height / rows) - frame.Height)) + frame.Width;
        var width = Math.Clamp(cellW * cols, Geometry.Ceiling(block.Width * 0.25), Geometry.Floor(block.Width * (cols == 1 ? 0.40 : 0.50)));
        var split = block.Width - width;

        yield return Slot(members[0], RoleOf(members[0], clients), m, o, block with { Width = split }) with { Fit = FitMode.None };
        for (var i = 0; i < followers; i++)
        {
            int col = i / rows, row = i % rows;
            var x = split + Geometry.Round((double)width * col / cols);
            var right = split + Geometry.Round((double)width * (col + 1) / cols);
            var y = Geometry.Round((double)block.Height * row / rows);
            var bottom = Geometry.Round((double)block.Height * (row + 1) / rows);
            yield return Slot(members[i + 1], SlotRole.Follower, m, o, new PixelRect(block.X + x, block.Y + y, right - x, bottom - y)) with { Anchor = TowardsMiddle(row, rows) };
        }
    }

    /// <summary>If a follower must shrink, it moves towards the vertical middle (the launcher's Bottom/Top anchors).</summary>
    private static Anchor TowardsMiddle(int row, int rows)
    {
        if (rows == 1) return Anchor.Top;
        if (rows % 2 == 1 && row == rows / 2) return Anchor.Center;
        return row < rows / 2 ? Anchor.Bottom : Anchor.Top;
    }

    // ---------- Overlays ----------

    private static List<LayoutSlot> Overlay(int clients, IReadOnlyList<MonitorInfo> screens, LayoutOptions o, bool followersOnSecond)
    {
        var parties = Parties(clients);
        var slots = new List<LayoutSlot> { MainUnder(1, screens[0], o) };
        if (parties.Count == 1)
        {
            var rowScreen = followersOnSecond && screens.Count > 1 ? screens[1] : screens[0];
            slots.AddRange(OverlayRows(parties[0][1..].Chunk(OverlayPerRow).ToList(), rowScreen, o, clients));
        }
        else if (screens.Count == 1)
        {
            // one monitor: party 1's followers in the bottom row, the whole of party 2 in a row above it
            slots.AddRange(OverlayRows([parties[0][1..], parties[1]], screens[0], o, clients));
        }
        else
        {
            // one party per monitor, each with its main underneath
            slots.AddRange(OverlayRows([parties[0][1..]], screens[0], o, clients));
            slots.Add(MainUnder(parties[1][0], screens[1], o));
            if (parties[1].Length > 1) slots.AddRange(OverlayRows([parties[1][1..]], screens[1], o, clients));
        }
        return slots;
    }

    private static LayoutSlot MainUnder(int n, MonitorInfo m, LayoutOptions o) =>
        Slot(n, SlotRole.Main, m, o, Whole(m, o)) with { Z = ZOrder.Bottom, Fit = FitMode.None };

    /// <summary>Rows of floating windows, the first row at the bottom; every window the same size, each row centred.</summary>
    private static IEnumerable<LayoutSlot> OverlayRows(IReadOnlyList<int[]> rows, MonitorInfo m, LayoutOptions o, int clients)
    {
        var area = Whole(m, o);
        var perRow = Math.Max(1, rows.Max(r => r.Length));
        var gap = Geometry.Round(area.Width * OverlayGap);
        var cellW = Math.Min(Geometry.Round(area.Width * OverlayMaxCell), Geometry.Floor((area.Width * OverlayBand - gap * (perRow - 1)) / perRow));
        var cellH = Geometry.Round(cellW / OverlayAspect);
        var margin = Geometry.Round(area.Height * OverlayBottomMargin);
        for (var row = 0; row < rows.Count; row++)
        {
            var inRow = rows[row].Length;
            var rowWidth = inRow * cellW + (inRow - 1) * gap;
            var x0 = (area.Width - rowWidth) / 2;
            var y = area.Height - margin - (row + 1) * cellH - row * gap;
            for (var i = 0; i < inRow; i++)
                yield return Slot(rows[row][i], RoleOf(rows[row][i], clients), m, o, new PixelRect(x0 + i * (cellW + gap), y, cellW, cellH)) with { Z = ZOrder.Top };
        }
    }

    // ---------- Grid and spreading over monitors ----------

    /// <summary>How many of <paramref name="total"/> windows each of <paramref name="screens"/> monitors gets: as even as possible, the first ones first.</summary>
    internal static int[] Counts(int total, int screens) =>
        Enumerable.Range(0, screens).Select(i => total / screens + (i < total % screens ? 1 : 0)).ToArray();

    /// <summary>The windows spread evenly over the monitors, an even grid on each.</summary>
    private static IEnumerable<LayoutSlot> Spread(int[] numbers, IReadOnlyList<MonitorInfo> screens, LayoutOptions o, int clients)
    {
        var counts = Counts(numbers.Length, screens.Count);
        var next = 0;
        for (var s = 0; s < screens.Count; s++)
        {
            var cells = GridCells(counts[s], Whole(screens[s], o), o.MinAspect);
            foreach (var c in cells)
            {
                var n = numbers[next++];
                yield return Slot(n, RoleOf(n, clients), screens[s], o, c) with { Anchor = Anchor.Center };
            }
        }
    }

    /// <summary>
    /// Picks the column count that gives the biggest window after the 5:4 fit (fewer empty cells on a tie)
    /// and returns the cells relative to the area. A short last row is centred.
    /// </summary>
    internal static List<PixelRect> GridCells(int count, PixelRect area, double minAspect)
    {
        if (count <= 0) return [];
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
            cells.Add(new PixelRect(area.X + x, area.Y + y, right - x, bottom - y));
        }
        return cells;
    }

    // ---------- Free for all ----------

    // Spread over the monitors like the grid. Monitor 1: the main at 55 % with a staircase of 5:4 windows down the right;
    // the others: a staircase across the screen. Everything stays inside its monitor.
    private static List<LayoutSlot> Free(int clients, IReadOnlyList<MonitorInfo> screens, LayoutOptions o)
    {
        var counts = Counts(clients, screens.Count);
        var slots = new List<LayoutSlot>();
        var next = 1;
        for (var s = 0; s < screens.Count; s++)
        {
            var m = screens[s];
            var area = Whole(m, o);
            var numbers = Enumerable.Range(next, counts[s]).ToArray();
            next += counts[s];
            if (numbers.Length == 0) continue;
            var stairs = numbers;
            double left = area.Width * 0.08, room = area.Width * 0.8;
            if (s == 0)
            {
                slots.Add(Slot(1, SlotRole.Main, m, o, Rect(area.Width * 0.03, area.Height * 0.05, area.Width * 0.55, area.Height * 0.62)) with { Fit = FitMode.None });
                stairs = numbers[1..];
                left = area.Width * 0.6;
                room = area.Width * 0.4;
            }
            var count = stairs.Length;
            var h = Math.Min(area.Height * 0.3, area.Height * 0.9 / Math.Max(count, 1) * 1.6);
            var w = h * 1.4;
            var stepY = count > 1 ? (area.Height - h - area.Height * 0.04) / (count - 1) : 0;
            var stepX = count > 1 ? Math.Min(area.Width * 0.035, (room - w * 0.2) / count) : 0;
            for (var i = 0; i < count; i++)
            {
                var x = Math.Min(left + i * stepX, area.Width - w);
                var y = Math.Min(area.Height * 0.02 + i * stepY, area.Height - h);
                slots.Add(Slot(stairs[i], RoleOf(stairs[i], clients), m, o, Rect(x, y, w, h)) with { Fit = FitMode.None });
            }
        }
        return slots;
    }

    private static PixelRect Rect(double x, double y, double w, double h)
    {
        int l = Geometry.Round(x), t = Geometry.Round(y);
        return new PixelRect(l, t, Geometry.Round(x + w) - l, Geometry.Round(y + h) - t);
    }

    /// <summary>A slot from pixels relative to the monitor's area, stored as % (4 decimals is far below one pixel).</summary>
    private static LayoutSlot Slot(int n, SlotRole role, MonitorInfo m, LayoutOptions o, PixelRect r)
    {
        var area = m.Area(o.UseTaskbarArea);
        return new LayoutSlot
        {
            N = n,
            Role = role,
            Monitor = ScreenSet.Ref(m),
            X = Math.Round(Geometry.Percent(r.X, area.Width), 4),
            Y = Math.Round(Geometry.Percent(r.Y, area.Height), 4),
            W = Math.Round(Geometry.Percent(r.Width, area.Width), 4),
            H = Math.Round(Geometry.Percent(r.Height, area.Height), 4),
        };
    }
}
