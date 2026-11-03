namespace WoWBoxer.Layouts.Core;

/// <summary>What a squad member does in the group (optional, Squad row details). Used for ready-made swap orders.</summary>
public enum CombatRole { Tank, Healer, Damage }

/// <summary>Which clients "next / previous client to main" walks through.</summary>
public enum SwapCycleScope
{
    /// <summary>Party 1's clients only (the default).</summary>
    Party1,
    /// <summary>The clients of the party whose window is active (falls back to party 1).</summary>
    ActiveParty,
    /// <summary>Every client, into party 1's main slot.</summary>
    All,
}

/// <summary>
/// Window swap (Settings › Advanced, off by default; docs/guide "Swap windows"). Clients keep their number, name and
/// class; only WHERE a client's window sits changes. Positions, sizes, monitor and "always on top" belong to the SLOT.
/// <para>
/// The state is a placement: <c>pos[client] = slot</c> (0-based indices into the layout's slots; identity = the home
/// layout). "Client c to main" exchanges c's slot with the slot of the client in the main slot. The layout file itself
/// is never changed by a swap.
/// </para>
/// </summary>
public static class WindowSwap
{
    /// <summary>The home placement: every client in its own slot.</summary>
    public static int[] Home(int clients) => Enumerable.Range(0, clients).ToArray();

    public static bool IsHome(IReadOnlyList<int> pos) => pos.Select((s, c) => s == c).All(x => x);

    /// <summary>A placement that fits the layout (right length, a permutation) or the home placement.</summary>
    public static int[] Valid(IReadOnlyList<int>? pos, int clients) =>
        pos is not null && pos.Count == clients && pos.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, clients)) ? pos.ToArray() : Home(clients);

    /// <summary>The client sitting in a slot.</summary>
    public static int ClientIn(IReadOnlyList<int> pos, int slot)
    {
        for (var c = 0; c < pos.Count; c++)
            if (pos[c] == slot) return c;
        return -1;
    }

    // ---------- main slots ----------

    /// <summary>
    /// The main slot of each party, as 0-based slot indices (index 0 = party 1, 1 = party 2; -1 = none chosen).
    /// The layout's own choice (<see cref="LayoutProfile.SwapMainSlots"/>) wins; else the default: the large slot (role
    /// Main) in Main + side and Overlays, none in layouts where slots are equal (Grid, Custom, captured).
    /// </summary>
    public static int[] MainSlots(LayoutProfile layout)
    {
        var parties = PartyCount(layout.Slots.Count);
        var result = Enumerable.Repeat(-1, parties).ToArray();
        if (layout.SwapMainSlots is { } chosen)
        {
            foreach (var n in chosen)
            {
                var i = n - 1;
                if (i < 0 || i >= layout.Slots.Count) continue;
                var p = PartyIndexOf(i, layout.Slots.Count);
                if (result[p] < 0) result[p] = i;
            }
            return result;
        }
        if (layout.Preset is not (PresetKind.Side or PresetKind.Overlay or PresetKind.Dual)) return result;
        for (var i = 0; i < layout.Slots.Count; i++)
        {
            var p = PartyIndexOf(i, layout.Slots.Count);
            if (layout.Slots[i].Role == SlotRole.Main && result[p] < 0) result[p] = i;
        }
        return result;
    }

    /// <summary>True when the layout's main slots are the default (Main + side / Overlays) – no need to pick one.</summary>
    public static bool NeedsChoice(LayoutProfile layout) => MainSlots(layout).Contains(-1);

    /// <summary>
    /// Makes <paramref name="slot"/> its party's main slot (or, if it already is, clears it). Returns the 1-based slot
    /// numbers to store in <see cref="LayoutProfile.SwapMainSlots"/>.
    /// </summary>
    public static IReadOnlyList<int> ToggleMain(LayoutProfile layout, int slot)
    {
        var mains = MainSlots(layout);
        var p = PartyIndexOf(slot, layout.Slots.Count);
        mains[p] = mains[p] == slot ? -1 : slot;
        return mains.Where(m => m >= 0).Select(m => m + 1).ToList();
    }

    public static int PartyCount(int clients) => clients > Presets.PartySize ? 2 : 1;

    /// <summary>0 for slots/clients 1–5, 1 for 6–10 (0-based index in, 0-based party out).</summary>
    public static int PartyIndexOf(int index, int clients) => clients > Presets.PartySize && index >= Presets.PartySize ? 1 : 0;

    // ---------- swapping ----------

    /// <summary>
    /// Client <paramref name="client"/> into <paramref name="mainSlot"/>; the client that sat there takes the chosen
    /// client's old slot. Nothing changes if the client is already there.
    /// </summary>
    public static int[] ToMain(IReadOnlyList<int> pos, int client, int mainSlot)
    {
        var next = pos.ToArray();
        var other = ClientIn(pos, mainSlot);
        if (other < 0 || other == client) return next;
        (next[client], next[other]) = (next[other], next[client]);
        return next;
    }

    /// <summary>The main slot a client goes to with "client N to main": its own party's, else party 1's; -1 = none.</summary>
    public static int MainSlotFor(int[] mains, int client, int clients)
    {
        var p = PartyIndexOf(client, clients);
        return p < mains.Length && mains[p] >= 0 ? mains[p] : mains.Length > 0 ? mains[0] : -1;
    }

    /// <summary>The clients (0-based) a cycle walks through, in number order. <paramref name="party"/>: 0 = party 1, 1 = party 2.</summary>
    public static IReadOnlyList<int> NumberOrder(int clients, SwapCycleScope scope, int party)
    {
        if (scope == SwapCycleScope.All || clients <= Presets.PartySize) return Enumerable.Range(0, clients).ToList();
        return party == 1 ? Enumerable.Range(Presets.PartySize, clients - Presets.PartySize).ToList() : Enumerable.Range(0, Presets.PartySize).ToList();
    }

    /// <summary>
    /// "Next / previous client to main". <paramref name="order"/> is the cycle (0-based clients; may repeat, e.g.
    /// 1 → 3 → 1 → 4); <paramref name="cursor"/> is where the cycle stood last time (-1 = start from the client in the
    /// main slot). Clients whose window is not open are skipped; the walk wraps around. Returns the client to bring to
    /// main and the new cursor, or (-1, cursor) when no other open client is in the cycle.
    /// </summary>
    public static (int Client, int Cursor) Step(IReadOnlyList<int> pos, int mainSlot, IReadOnlyList<int> order, Func<int, bool> isOpen, int cursor, bool forward)
    {
        if (order.Count == 0 || mainSlot < 0) return (-1, cursor);
        var inMain = ClientIn(pos, mainSlot);
        if (cursor < 0 || cursor >= order.Count || order[cursor] != inMain)
        {
            // start from the main slot's client (its first place in the order), or just before the start
            var at = order.ToList().IndexOf(inMain);
            cursor = at >= 0 ? at : forward ? -1 : order.Count;
        }
        for (var k = 1; k <= order.Count; k++)
        {
            var i = forward ? cursor + k : cursor - k;
            i = ((i % order.Count) + order.Count) % order.Count;
            var c = order[i];
            if (c == inMain || c < 0 || c >= pos.Count || !isOpen(c)) continue;
            return (c, i);
        }
        return (-1, cursor);
    }

    // ---------- applying ----------

    /// <summary>
    /// The layout as placed right now: client i keeps its number, name, class and role in the group, and gets the place
    /// of slot <c>pos[i]</c> (monitor, position, size, on top, anchor, fit and how it is drawn).
    /// </summary>
    public static LayoutProfile Apply(LayoutProfile home, IReadOnlyList<int> pos)
    {
        if (pos.Count != home.Slots.Count || IsHome(pos)) return home;
        var slots = home.Slots.Select((s, i) => WithPlaceOf(s, home.Slots[pos[i]])).ToList();
        return home with { Slots = slots };
    }

    /// <summary>
    /// An edit made on the placed layout (client i dragged or resized) written back to the home layout: the change belongs
    /// to the slot client i sits in (<c>pos[i]</c>); names and classes stay with the client.
    /// </summary>
    public static LayoutProfile Unapply(LayoutProfile home, IReadOnlyList<int> pos, int client, LayoutSlot edited)
    {
        var slots = home.Slots.ToList();
        var slot = pos.Count == slots.Count ? pos[client] : client;
        slots[slot] = WithPlaceOf(slots[slot], edited);
        slots[client] = slots[client] with { Name = edited.Name, Class = edited.Class, CombatRole = edited.CombatRole };
        return home with { Slots = slots };
    }

    private static LayoutSlot WithPlaceOf(LayoutSlot who, LayoutSlot where) => who with
    {
        Role = where.Role, Monitor = where.Monitor, X = where.X, Y = where.Y, W = where.W, H = where.H,
        Z = where.Z, Anchor = where.Anchor, Fit = where.Fit,
    };

    // ---------- ready-made cycle orders ----------

    /// <summary>A ready-made "next client" order: its name and the 1-based client numbers.</summary>
    public sealed record OrderPreset(string Name, IReadOnlyList<int> Clients);

    /// <summary>
    /// "All in number order" and "Leader ↔ each follower" always; "Leader ↔ healer" only when a healer is known (a row
    /// with role Healer, or – when no row has a role – a priest).
    /// </summary>
    public static IReadOnlyList<OrderPreset> OrderPresets(LayoutProfile layout)
    {
        var n = Math.Min(layout.Slots.Count, Presets.PartySize);
        var list = new List<OrderPreset> { new("All in number order", Enumerable.Range(1, n).ToList()) };
        var healer = HealerOf(layout);
        if (healer > 1) list.Add(new("Leader ↔ healer", [1, healer]));
        if (n >= 2) list.Add(new("Leader ↔ each follower", Enumerable.Range(2, n - 1).SelectMany(f => new[] { 1, f }).ToList()));
        return list;
    }

    /// <summary>The 1-based number of party 1's healer, or 0.</summary>
    public static int HealerOf(LayoutProfile layout)
    {
        var party = layout.Slots.Take(Presets.PartySize).ToList();
        var byRole = party.FirstOrDefault(s => s.CombatRole == CombatRole.Healer);
        if (byRole is not null) return byRole.N;
        if (party.Any(s => s.CombatRole is not null)) return 0;
        return party.Where(s => s.N != 1).FirstOrDefault(s => string.Equals(s.Class, "priest", StringComparison.OrdinalIgnoreCase))?.N ?? 0;
    }

    /// <summary>A custom order (1-based numbers) as 0-based clients that exist in the layout; null/empty = none.</summary>
    public static IReadOnlyList<int>? CustomOrder(IReadOnlyList<int>? numbers, int clients)
    {
        var list = numbers?.Where(n => n >= 1 && n <= clients).Select(n => n - 1).ToList();
        return list is { Count: >= 2 } ? list : null;
    }
}
