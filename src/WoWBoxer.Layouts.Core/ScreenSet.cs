using System.Globalization;
using System.Text.Json.Serialization;

namespace WoWBoxer.Layouts.Core;

/// <summary>
/// A monitor a layout is made for, remembered so the same screen is found again: Windows' device ID (stable per screen)
/// and, as a fallback when IDs change, its place and size.
/// </summary>
public sealed record ScreenRef
{
    /// <summary>The monitor value the slots used for this screen when the layout was made ("primary" or a 1-based index).</summary>
    public string Ref { get; init; } = "primary";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>Which monitors a layout uses: the order (1 = the main's monitor), finding them again, and their names.</summary>
public static class ScreenSet
{
    /// <summary>All monitors in the app's order: the primary first, then left to right (top to bottom on a tie).</summary>
    public static IReadOnlyList<MonitorInfo> Ordered(IReadOnlyList<MonitorInfo> monitors) =>
        monitors.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();

    /// <summary>The value a slot uses for this monitor: "primary" or its 1-based index.</summary>
    public static string Ref(MonitorInfo m) => m.IsPrimary ? "primary" : m.Index.ToString(CultureInfo.InvariantCulture);

    public static ScreenRef ToRef(MonitorInfo m) => new()
    {
        Ref = Ref(m),
        Id = string.IsNullOrEmpty(m.DeviceId) ? null : m.DeviceId,
        X = m.Bounds.X,
        Y = m.Bounds.Y,
        Width = m.Bounds.Width,
        Height = m.Bounds.Height,
    };

    /// <summary>
    /// Finds each remembered screen among the monitors now: by device ID, else by place and size, else by size alone
    /// (when only one unused monitor has it). Null where a screen is not connected.
    /// </summary>
    public static IReadOnlyList<MonitorInfo?> Match(IReadOnlyList<ScreenRef> screens, IReadOnlyList<MonitorInfo> monitors)
    {
        var result = new MonitorInfo?[screens.Count];
        var used = new HashSet<int>();
        void Pass(Func<ScreenRef, MonitorInfo, bool> same, bool unique = false)
        {
            for (var i = 0; i < screens.Count; i++)
            {
                if (result[i] is not null) continue;
                var candidates = monitors.Where(m => !used.Contains(m.Index) && same(screens[i], m)).ToList();
                if (candidates.Count == 0 || (unique && candidates.Count > 1)) continue;
                result[i] = candidates[0];
                used.Add(candidates[0].Index);
            }
        }
        Pass((s, m) => s.Id is { Length: > 0 } id && string.Equals(id, m.DeviceId, StringComparison.OrdinalIgnoreCase));
        Pass((s, m) => s.X == m.Bounds.X && s.Y == m.Bounds.Y && s.Width == m.Bounds.Width && s.Height == m.Bounds.Height);
        Pass((s, m) => s.Width == m.Bounds.Width && s.Height == m.Bounds.Height, unique: true);
        return result;
    }

    /// <summary>For layouts without remembered screens (older files, captures): the monitors the slots use, the main's first.</summary>
    public static IReadOnlyList<MonitorInfo> Used(LayoutProfile profile, IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0) return [];
        var used = profile.Slots.OrderBy(s => s.N).Select(s => Monitors.Find(monitors, s.Monitor) ?? Monitors.Primary(monitors)).DistinctBy(m => m.Index).ToList();
        if (used.Count == 0) return [Monitors.Primary(monitors)];
        var order = Ordered(monitors).Select(m => m.Index).ToList();
        return [used[0], .. used.Skip(1).OrderBy(m => order.IndexOf(m.Index))];
    }

    /// <summary>The monitors the layout uses right now, in its order. <c>Missing</c>: a remembered screen is not connected.</summary>
    public static (IReadOnlyList<MonitorInfo> Chosen, bool Missing) Resolve(LayoutProfile profile, IReadOnlyList<MonitorInfo> monitors)
    {
        if (profile.Screens is not { Count: > 0 } screens) return (Used(profile, monitors), false);
        var found = Match(screens, monitors);
        return (found.OfType<MonitorInfo>().ToList(), found.Any(m => m is null));
    }

    /// <summary>
    /// Points the slots and the remembered screens at the monitors as Windows numbers them now (indices and IDs can change
    /// after a restart or re-plugging). Returns the same profile when nothing changed or a screen is missing.
    /// </summary>
    public static LayoutProfile Remap(LayoutProfile profile, IReadOnlyList<MonitorInfo> monitors)
    {
        if (profile.Screens is not { Count: > 0 } screens) return profile;
        var found = Match(screens, monitors);
        if (found.Any(m => m is null)) return profile;
        var now = found.Select(m => ToRef(m!)).ToList();
        if (now.SequenceEqual(screens)) return profile;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < screens.Count; i++) map.TryAdd(screens[i].Ref, now[i].Ref);
        var slots = profile.Slots.Select(s => map.TryGetValue(s.Monitor, out var r) ? s with { Monitor = r } : s).ToList();
        return profile with { Slots = slots, Screens = now };
    }

    /// <summary>Every way to pick <paramref name="count"/> monitors, in the app's order; the first is the default (primary, then left to right).</summary>
    public static IReadOnlyList<IReadOnlyList<MonitorInfo>> Choices(IReadOnlyList<MonitorInfo> monitors, int count)
    {
        var ordered = Ordered(monitors);
        var result = new List<IReadOnlyList<MonitorInfo>>();
        void Pick(int start, List<MonitorInfo> chosen)
        {
            if (chosen.Count == count)
            {
                result.Add(chosen.ToList());
                return;
            }
            for (var i = start; i < ordered.Count; i++)
            {
                chosen.Add(ordered[i]);
                Pick(i + 1, chosen);
                chosen.RemoveAt(chosen.Count - 1);
            }
        }
        if (count >= 1 && count <= ordered.Count) Pick(0, []);
        return result;
    }

    /// <summary>Where a monitor sits, seen from the primary: "primary", "left", "right", "above", "below" (numbered if two share a side).</summary>
    public static string Position(MonitorInfo m, IReadOnlyList<MonitorInfo> monitors)
    {
        string Side(MonitorInfo x)
        {
            if (x.IsPrimary) return "primary";
            var p = Monitors.Primary(monitors).Bounds;
            int cx = x.Bounds.X + x.Bounds.Width / 2 - (p.X + p.Width / 2), cy = x.Bounds.Y + x.Bounds.Height / 2 - (p.Y + p.Height / 2);
            return Math.Abs(cx) >= Math.Abs(cy) ? (cx < 0 ? "left" : "right") : (cy < 0 ? "above" : "below");
        }
        var side = Side(m);
        var same = Ordered(monitors).Where(x => Side(x) == side).ToList();
        return same.Count > 1 ? $"{side} {same.FindIndex(x => x.Index == m.Index) + 1}" : side;
    }

    /// <summary>"Primary + right · 2560×1440 + 1920×1080".</summary>
    public static string Describe(IReadOnlyList<MonitorInfo> chosen, IReadOnlyList<MonitorInfo> monitors)
    {
        var names = string.Join(" + ", chosen.Select(m => Position(m, monitors)));
        return $"{char.ToUpperInvariant(names[0])}{names[1..]} · {string.Join(" + ", chosen.Select(m => $"{m.Bounds.Width}×{m.Bounds.Height}"))}";
    }
}
