namespace WoWBoxer.Layouts.Core.Tests;

public class WindowSwapTests
{
    private static LayoutProfile Layout(PresetKind kind, int clients, int monitors = 1)
    {
        var screens = monitors == 1 ? TestMonitors.Reference : TestMonitors.Setups["dual-1080p"];
        var named = Presets.ForScreens(kind, clients, screens);
        return named with { Slots = named.Slots.Select(s => s with { Name = $"Char{s.N}", Class = s.N == 3 ? "priest" : "mage" }).ToList() };
    }

    // ---------- main slots per layout ----------

    [Theory]
    [InlineData(PresetKind.Side, 3)]
    [InlineData(PresetKind.Overlay, 5)]
    [InlineData(PresetKind.Side, 1)]
    public void Main_plus_side_and_overlays_default_to_the_large_slot(PresetKind kind, int clients)
    {
        var l = Layout(kind, clients);
        Assert.Equal([0], WindowSwap.MainSlots(l));
        Assert.False(WindowSwap.NeedsChoice(l));
    }

    [Theory]
    [InlineData(PresetKind.Side)]
    [InlineData(PresetKind.Overlay)]
    public void Six_to_ten_clients_get_one_main_slot_per_party(PresetKind kind)
    {
        var l = Layout(kind, 8);
        Assert.Equal([0, 5], WindowSwap.MainSlots(l));
    }

    [Fact]
    public void The_custom_start_uses_its_large_window_as_main() =>
        Assert.Equal([0], WindowSwap.MainSlots(Layout(PresetKind.Custom, 4)));

    [Theory]
    [InlineData(PresetKind.Grid)]
    public void Equal_slot_layouts_need_a_choice(PresetKind kind)
    {
        var l = Layout(kind, 4);
        Assert.Equal([-1], WindowSwap.MainSlots(l));
        Assert.True(WindowSwap.NeedsChoice(l));
        var chosen = l with { SwapMainSlots = WindowSwap.ToggleMain(l, 2) };
        Assert.Equal([2], WindowSwap.MainSlots(chosen));
        Assert.False(WindowSwap.NeedsChoice(chosen));
    }

    [Fact]
    public void Captured_layouts_with_equal_windows_need_a_choice() =>
        Assert.True(WindowSwap.NeedsChoice(Layout(PresetKind.Grid, 4) with { Preset = null }));

    [Fact]
    public void Picking_a_main_slot_replaces_that_partys_main_only()
    {
        var l = Layout(PresetKind.Grid, 10);
        l = l with { SwapMainSlots = WindowSwap.ToggleMain(l, 1) };   // party 1
        l = l with { SwapMainSlots = WindowSwap.ToggleMain(l, 7) };   // party 2
        Assert.Equal([1, 7], WindowSwap.MainSlots(l));
        l = l with { SwapMainSlots = WindowSwap.ToggleMain(l, 3) };   // party 1 again: replaces 1
        Assert.Equal([3, 7], WindowSwap.MainSlots(l));
        l = l with { SwapMainSlots = WindowSwap.ToggleMain(l, 3) };   // toggled off
        Assert.Equal([-1, 7], WindowSwap.MainSlots(l));
        Assert.True(WindowSwap.NeedsChoice(l));
    }

    [Fact]
    public void Main_slot_choice_survives_json() =>
        Assert.Equal([2, 7], ProfileJson.Deserialize(ProfileJson.Serialize(Layout(PresetKind.Grid, 8) with { SwapMainSlots = [2, 7] })).SwapMainSlots);

    // ---------- swap and reset ----------

    [Fact]
    public void Client_to_main_exchanges_slots_with_the_client_there()
    {
        var pos = WindowSwap.ToMain(WindowSwap.Home(4), 2, 0);
        Assert.Equal([2, 1, 0, 3], pos);
        Assert.Equal(2, WindowSwap.ClientIn(pos, 0));
        // client 4 to main: client 3 (in main) takes client 4's old slot
        pos = WindowSwap.ToMain(pos, 3, 0);
        Assert.Equal([2, 1, 3, 0], pos);
    }

    [Fact]
    public void Client_already_in_main_changes_nothing() =>
        Assert.Equal([0, 1, 2], WindowSwap.ToMain(WindowSwap.Home(3), 0, 0));

    [Fact]
    public void Swap_then_reset_round_trip()
    {
        var home = Layout(PresetKind.Side, 3);
        var pos = WindowSwap.ToMain(WindowSwap.Home(3), 2, 0);
        var placed = WindowSwap.Apply(home, pos);
        // client 3 keeps its number, name and class but sits where slot 1 was (and is drawn as the main)
        Assert.Equal(3, placed.Slots[2].N);
        Assert.Equal("Char3", placed.Slots[2].Name);
        Assert.Equal("priest", placed.Slots[2].Class);
        Assert.Equal((home.Slots[0].X, home.Slots[0].Y, home.Slots[0].W, home.Slots[0].H, home.Slots[0].Z), (placed.Slots[2].X, placed.Slots[2].Y, placed.Slots[2].W, placed.Slots[2].H, placed.Slots[2].Z));
        Assert.Equal(SlotRole.Main, placed.Slots[2].Role);
        Assert.Equal((home.Slots[2].X, home.Slots[2].Y), (placed.Slots[0].X, placed.Slots[0].Y));
        Assert.Equal("Char1", placed.Slots[0].Name);
        // back home
        Assert.Equal(home, WindowSwap.Apply(home, WindowSwap.Home(3)));
        Assert.True(WindowSwap.IsHome(WindowSwap.Home(3)));
    }

    [Fact]
    public void Always_on_top_belongs_to_the_slot()
    {
        var home = Layout(PresetKind.Overlay, 3);
        Assert.Equal(ZOrder.Top, home.Slots[1].Z);
        var placed = WindowSwap.Apply(home, WindowSwap.ToMain(WindowSwap.Home(3), 1, 0));
        Assert.NotEqual(ZOrder.Top, placed.Slots[1].Z);   // client 2 is the main now: not on top
        Assert.Equal(ZOrder.Top, placed.Slots[0].Z);      // client 1 floats in client 2's old place
    }

    [Fact]
    public void Many_swaps_always_stay_a_permutation_and_reset_restores_the_layout()
    {
        var home = Layout(PresetKind.Side, 5);
        var pos = WindowSwap.Home(5);
        var rnd = new Random(7);
        for (var i = 0; i < 500; i++)
        {
            pos = WindowSwap.ToMain(pos, rnd.Next(5), 0);
            Assert.Equal([0, 1, 2, 3, 4], pos.OrderBy(x => x));
        }
        Assert.Equal(home, WindowSwap.Apply(home, WindowSwap.Valid(null, 5)));
    }

    [Fact]
    public void Invalid_saved_positions_fall_back_to_home()
    {
        Assert.Equal([0, 1, 2], WindowSwap.Valid([0, 0, 1], 3));
        Assert.Equal([0, 1, 2], WindowSwap.Valid([1, 0], 3));
        Assert.Equal([1, 0, 2], WindowSwap.Valid([1, 0, 2], 3));
    }

    [Fact]
    public void Editing_the_placed_layout_changes_the_slot_not_the_client()
    {
        var home = Layout(PresetKind.Side, 3);
        var pos = WindowSwap.ToMain(WindowSwap.Home(3), 2, 0);
        var placed = WindowSwap.Apply(home, pos);
        var edited = placed.Slots[2] with { W = placed.Slots[2].W - 10, Name = "Healer" };   // client 3 (in the main slot) resized and renamed
        var newHome = WindowSwap.Unapply(home, pos, 2, edited);
        Assert.Equal(home.Slots[0].W - 10, newHome.Slots[0].W);   // the main slot got smaller
        Assert.Equal("Char1", newHome.Slots[0].Name);             // ... and still belongs to client 1 at home
        Assert.Equal("Healer", newHome.Slots[2].Name);            // the name stays with client 3
        Assert.Equal(home.Slots[2].W, newHome.Slots[2].W);
    }

    // ---------- next / previous ----------

    private static readonly Func<int, bool> AllOpen = _ => true;

    [Fact]
    public void Next_walks_in_number_order_and_wraps()
    {
        var pos = WindowSwap.Home(4);
        var order = WindowSwap.NumberOrder(4, SwapCycleScope.Party1, 0);
        var seen = new List<int>();
        var cursor = -1;
        for (var i = 0; i < 5; i++)
        {
            (var c, cursor) = WindowSwap.Step(pos, 0, order, AllOpen, cursor, forward: true);
            pos = WindowSwap.ToMain(pos, c, 0);
            seen.Add(c + 1);
        }
        Assert.Equal([2, 3, 4, 1, 2], seen);
    }

    [Fact]
    public void Previous_walks_backwards()
    {
        var pos = WindowSwap.Home(4);
        var order = WindowSwap.NumberOrder(4, SwapCycleScope.Party1, 0);
        var (c, _) = WindowSwap.Step(pos, 0, order, AllOpen, -1, forward: false);
        Assert.Equal(3, c);   // client 4
    }

    [Fact]
    public void Closed_windows_are_skipped()
    {
        var pos = WindowSwap.Home(4);
        var order = WindowSwap.NumberOrder(4, SwapCycleScope.Party1, 0);
        var (c, _) = WindowSwap.Step(pos, 0, order, x => x != 1 && x != 2, -1, forward: true);
        Assert.Equal(3, c);   // 2 and 3 closed: client 4
    }

    [Fact]
    public void Nothing_to_swap_when_only_the_main_is_open() =>
        Assert.Equal(-1, WindowSwap.Step(WindowSwap.Home(3), 0, [0, 1, 2], x => x == 0, -1, true).Client);

    [Fact]
    public void Main_slot_occupant_counts_even_after_swaps()
    {
        // client 3 sits in main: next is client 4, not client 2
        var pos = WindowSwap.ToMain(WindowSwap.Home(4), 2, 0);
        Assert.Equal(3, WindowSwap.Step(pos, 0, [0, 1, 2, 3], AllOpen, -1, true).Client);
    }

    [Fact]
    public void Custom_order_leader_and_healer_toggles()
    {
        var pos = WindowSwap.Home(5);
        IReadOnlyList<int> order = [0, 2];   // 1 -> 3 -> 1
        var cursor = -1;
        var seen = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            (var c, cursor) = WindowSwap.Step(pos, 0, order, AllOpen, cursor, true);
            pos = WindowSwap.ToMain(pos, c, 0);
            seen.Add(c + 1);
        }
        Assert.Equal([3, 1, 3, 1], seen);
    }

    [Fact]
    public void Custom_order_with_repeats_leader_and_each_follower()
    {
        var pos = WindowSwap.Home(4);
        var order = WindowSwap.CustomOrder([1, 2, 1, 3, 1, 4], 4)!;
        var cursor = -1;
        var seen = new List<int>();
        for (var i = 0; i < 7; i++)
        {
            (var c, cursor) = WindowSwap.Step(pos, 0, order, AllOpen, cursor, true);
            pos = WindowSwap.ToMain(pos, c, 0);
            seen.Add(c + 1);
        }
        Assert.Equal([2, 1, 3, 1, 4, 1, 2], seen);
    }

    [Fact]
    public void Parties_cycle_within_their_own_clients()
    {
        Assert.Equal([0, 1, 2, 3, 4], WindowSwap.NumberOrder(8, SwapCycleScope.Party1, 0));
        Assert.Equal([5, 6, 7], WindowSwap.NumberOrder(8, SwapCycleScope.ActiveParty, 1));
        Assert.Equal(Enumerable.Range(0, 8), WindowSwap.NumberOrder(8, SwapCycleScope.All, 0));
        // party 2 cycle with its main slot (slot 6)
        var pos = WindowSwap.Home(8);
        var (c, _) = WindowSwap.Step(pos, 5, WindowSwap.NumberOrder(8, SwapCycleScope.ActiveParty, 1), AllOpen, -1, true);
        Assert.Equal(6, c);   // client 7
    }

    [Fact]
    public void Client_to_main_uses_its_own_partys_main_slot()
    {
        int[] mains = [0, 5];
        Assert.Equal(0, WindowSwap.MainSlotFor(mains, 2, 8));
        Assert.Equal(5, WindowSwap.MainSlotFor(mains, 7, 8));
        Assert.Equal(0, WindowSwap.MainSlotFor([0, -1], 7, 8));   // party 2 has none: party 1's
        Assert.Equal(-1, WindowSwap.MainSlotFor([-1], 1, 3));
    }

    // ---------- ready-made orders ----------

    [Fact]
    public void Healer_preset_only_with_a_known_healer()
    {
        var l = Layout(PresetKind.Side, 4);   // client 3 is a priest
        Assert.Contains(WindowSwap.OrderPresets(l), p => p.Name == "Leader ↔ healer" && p.Clients.SequenceEqual([1, 3]));
        var noPriest = l with { Slots = l.Slots.Select(s => s with { Class = "mage" }).ToList() };
        Assert.DoesNotContain(WindowSwap.OrderPresets(noPriest), p => p.Name == "Leader ↔ healer");
        var roles = noPriest with { Slots = noPriest.Slots.Select(s => s with { CombatRole = s.N == 4 ? CombatRole.Healer : CombatRole.Damage }).ToList() };
        Assert.Contains(WindowSwap.OrderPresets(roles), p => p.Name == "Leader ↔ healer" && p.Clients.SequenceEqual([1, 4]));
    }

    [Fact]
    public void Leader_and_each_follower_order()
    {
        var p = WindowSwap.OrderPresets(Layout(PresetKind.Side, 4)).Single(x => x.Name == "Leader ↔ each follower");
        Assert.Equal([1, 2, 1, 3, 1, 4], p.Clients);
    }

    [Fact]
    public void Combat_role_survives_json()
    {
        var l = Layout(PresetKind.Side, 2);
        l = l with { Slots = [l.Slots[0] with { CombatRole = CombatRole.Tank }, l.Slots[1]] };
        Assert.Equal(CombatRole.Tank, ProfileJson.Deserialize(ProfileJson.Serialize(l)).Slots[0].CombatRole);
    }
}

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Shift+Right")]
    [InlineData("Ctrl+Alt+Shift+1")]
    [InlineData("Ctrl+Alt+Shift+Backspace")]
    [InlineData("Alt+F5")]
    [InlineData("Ctrl+Shift+Num3")]
    public void Parse_and_print_round_trip(string text)
    {
        Assert.True(Hotkeys.TryParse(text, out var k));
        Assert.Equal(text, k.ToString());
    }

    [Theory]
    [InlineData("shift+control+alt+right", "Ctrl+Alt+Shift+Right")]
    [InlineData("Ctrl + Alt + Back", "Ctrl+Alt+Backspace")]
    [InlineData("Ctrl+Alt+PgDn", "Ctrl+Alt+PageDown")]
    public void Parse_is_forgiving(string text, string expected)
    {
        Assert.True(Hotkeys.TryParse(text, out var k));
        Assert.Equal(expected, k.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Nonsense")]
    public void Parse_rejects(string text) => Assert.False(Hotkeys.TryParse(text, out _));

    [Fact]
    public void Defaults_are_the_recommended_set_and_have_no_warnings()
    {
        Assert.Equal("Ctrl+Alt+Shift+Right", Hotkeys.Defaults[SwapActions.Next].ToString());
        Assert.Equal("Ctrl+Alt+Shift+Left", Hotkeys.Defaults[SwapActions.Previous].ToString());
        Assert.Equal("Ctrl+Alt+Shift+0", Hotkeys.Defaults[SwapActions.Client(10)].ToString());
        Assert.Equal("Ctrl+Alt+Shift+Backspace", Hotkeys.Defaults[SwapActions.Home].ToString());
        Assert.Equal(13, Hotkeys.Defaults.Count);
        Assert.All(Hotkeys.Defaults.Values, k => Assert.Empty(Hotkeys.Warnings(k)));
        Assert.Empty(Hotkeys.Duplicates(Hotkeys.Defaults));
    }

    [Fact]
    public void User_choices_override_defaults_and_empty_clears()
    {
        var keys = Hotkeys.Effective(new Dictionary<string, string> { [SwapActions.Next] = "Ctrl+Alt+N", [SwapActions.Home] = "" });
        Assert.Equal("Ctrl+Alt+N", keys[SwapActions.Next].ToString());
        Assert.False(keys.ContainsKey(SwapActions.Home));
        Assert.Equal("Ctrl+Alt+Shift+Left", keys[SwapActions.Previous].ToString());
    }

    [Fact]
    public void Duplicates_are_reported_against_the_first_action()
    {
        var keys = Hotkeys.Effective(new Dictionary<string, string> { [SwapActions.Home] = "Ctrl+Alt+Shift+Right" });
        Assert.Equal(SwapActions.Next, Hotkeys.Duplicates(keys)[SwapActions.Home]);
    }

    [Theory]
    [InlineData("Alt+Tab")]
    [InlineData("Win+Ctrl+D")]
    [InlineData("Alt+Shift+K")]
    [InlineData("Ctrl+Shift+Esc")]
    [InlineData("Alt+F4")]
    public void Windows_shortcuts_warn(string text)
    {
        Assert.True(Hotkeys.TryParse(text, out var k));
        Assert.Contains(Hotkeys.Warnings(k), w => w.StartsWith("Windows uses"));
    }

    [Theory]
    [InlineData("F5")]
    [InlineData("Ctrl+F5")]
    public void Fewer_than_two_modifiers_warn(string text)
    {
        Assert.True(Hotkeys.TryParse(text, out var k));
        Assert.Contains(Hotkeys.Warnings(k), w => w.Contains("fewer than two"));
    }

    [Fact]
    public void Every_action_has_a_description()
    {
        Assert.Equal(13, SwapActions.All.Count);
        Assert.Equal("Client 7 to main", SwapActions.Describe("client:7"));
        Assert.All(SwapActions.All, a => Assert.DoesNotContain(":", SwapActions.Describe(a)));
    }
}
