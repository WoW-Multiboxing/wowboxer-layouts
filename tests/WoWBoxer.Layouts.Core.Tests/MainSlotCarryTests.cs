namespace WoWBoxer.Layouts.Core.Tests;

/// <summary>
/// Beta bug 2026-11-04: after a drag-swap "Trio" (Main + side) became Custom without a main slot, and every swap hotkey
/// only logged "click the crown". The main slot is now carried over, and Custom finds the clearly largest window.
/// </summary>
public class MainSlotCarryTests
{
    private static LayoutProfile Side(int clients = 3) => Presets.ForScreens(PresetKind.Side, clients, TestMonitors.Reference);

    /// <summary>Windows <paramref name="a"/> and <paramref name="b"/> trade places (what dropping one on the other does).</summary>
    private static LayoutProfile Swapped(LayoutProfile l, int a, int b)
    {
        var slots = l.Slots.ToList();
        LayoutSlot PlaceOf(LayoutSlot who, LayoutSlot where) => who with { Role = who.Role, Monitor = where.Monitor, X = where.X, Y = where.Y, W = where.W, H = where.H, Anchor = where.Anchor, Fit = where.Fit };
        slots[a] = PlaceOf(l.Slots[a], l.Slots[b]);
        slots[b] = PlaceOf(l.Slots[b], l.Slots[a]);
        return l with { Slots = slots };
    }

    private static LayoutProfile Custom(params (double W, double H)[] sizes) => Side(sizes.Length) with
    {
        Preset = PresetKind.Custom,
        Slots = sizes.Select((s, i) => new LayoutSlot { N = i + 1, Role = i == 0 ? SlotRole.Main : SlotRole.Follower, X = i * 10, Y = 0, W = s.W, H = s.H }).ToList(),
    };

    // ---------- carry-over when a preset turns into Custom ----------

    [Fact]
    public void Drag_swap_on_main_plus_side_keeps_a_main_slot_where_the_large_window_is()
    {
        var before = Side();
        var (after, _) = CustomLayouts.Edited(before, Swapped(before, 0, 1), null);
        Assert.Equal(PresetKind.Custom, after.Preset);
        Assert.Equal([2], after.SwapMainSlots);              // the large place is slot 2's now
        Assert.Equal([1], WindowSwap.MainSlots(after));
        Assert.False(WindowSwap.NeedsChoice(after));
        Assert.Equal(before.Slots[0].W, after.Slots[WindowSwap.MainSlots(after)[0]].W);
    }

    [Fact]
    public void Moving_the_main_window_keeps_it_the_main_slot()
    {
        var before = Side();
        var slots = before.Slots.ToList();
        slots[0] = slots[0] with { X = slots[0].X + 2, W = slots[0].W - 2 };
        var (after, _) = CustomLayouts.Edited(before, before with { Slots = slots }, null);
        Assert.Equal([1], after.SwapMainSlots);
        Assert.Equal([0], WindowSwap.MainSlots(after));
    }

    [Fact]
    public void The_carried_crown_stays_even_when_windows_become_the_same_size()
    {
        var (custom, draft) = CustomLayouts.Edited(Side(), Swapped(Side(), 0, 1), null);
        var equal = custom with { Slots = custom.Slots.Select(s => s with { W = 33, H = 50 }).ToList() };
        var (after, _) = CustomLayouts.Edited(custom, equal, draft);
        Assert.Equal([1], WindowSwap.MainSlots(after));
    }

    [Fact]
    public void A_second_drag_swap_in_custom_moves_the_crown_with_the_large_place()
    {
        var (custom, draft) = CustomLayouts.Edited(Side(), Swapped(Side(), 0, 1), null);
        var (after, _) = CustomLayouts.Edited(custom, Swapped(custom, 1, 2), draft);
        Assert.Equal([2], WindowSwap.MainSlots(after));
    }

    [Fact]
    public void Each_party_keeps_its_main_slot()
    {
        var before = Side(8);
        var mains = WindowSwap.MainSlots(before);
        var (after, _) = CustomLayouts.Edited(before, Swapped(before, mains[1], mains[1] + 1), null);
        Assert.Equal([mains[0], mains[1] + 1], WindowSwap.MainSlots(after));
    }

    [Fact]
    public void The_custom_card_keeps_the_presets_main_slot()
    {
        var (custom, _) = CustomLayouts.Choose(Side(), null);
        Assert.Equal(PresetKind.Custom, custom.Preset);
        Assert.Equal([0], WindowSwap.MainSlots(custom));
        Assert.Equal([1], custom.SwapMainSlots);
    }

    [Fact]
    public void A_draft_brought_back_by_the_custom_card_gets_the_large_place_as_main()
    {
        var swapped = Swapped(Side(), 0, 1) with { Preset = PresetKind.Custom };
        var (custom, _) = CustomLayouts.Choose(Side(), new CustomDraft(swapped, null, Unsaved: false));
        Assert.Equal([1], WindowSwap.MainSlots(custom));
    }

    [Fact]
    public void A_draft_keeps_its_own_crown()
    {
        var draft = Custom((30, 50), (30, 50), (30, 50)) with { SwapMainSlots = [3] };
        var (custom, _) = CustomLayouts.Choose(Side(), new CustomDraft(draft, null, Unsaved: false));
        Assert.Equal([2], WindowSwap.MainSlots(custom));
    }

    [Fact]
    public void Nothing_is_stored_when_there_was_no_main_slot()
    {
        var grid = Presets.ForScreens(PresetKind.Grid, 4, TestMonitors.Reference);
        var slots = grid.Slots.ToList();
        slots[0] = slots[0] with { X = slots[0].X + 1 };
        var (after, _) = CustomLayouts.Edited(grid, grid with { Slots = slots }, null);
        Assert.Null(after.SwapMainSlots);
    }

    [Fact]
    public void A_cleared_crown_stays_cleared()
    {
        var l = Custom((66, 100), (34, 50), (34, 50)) with { SwapMainSlots = [] };
        Assert.True(WindowSwap.NeedsChoice(l));
        var (after, _) = CustomLayouts.Edited(l, Swapped(l, 0, 1), new CustomDraft(l, null, false));
        Assert.Equal([-1], WindowSwap.MainSlots(after));
    }

    // ---------- Custom without a crown: the clearly largest window ----------

    [Fact]
    public void Custom_without_a_crown_uses_the_clearly_largest_window()
    {
        var l = Custom((34, 50), (66, 100), (34, 50));
        Assert.Null(l.SwapMainSlots);
        Assert.Equal([1], WindowSwap.MainSlots(l));
        Assert.False(WindowSwap.NeedsChoice(l));
    }

    [Theory]
    [InlineData(33, 50, 33, 50)]      // equal
    [InlineData(34, 50, 33, 50)]      // 3 % larger: about equal
    public void Custom_with_about_equal_windows_asks(double w1, double h1, double w2, double h2)
    {
        var l = Custom((w1, h1), (w2, h2), (w2, h2));
        Assert.Equal([-1], WindowSwap.MainSlots(l));
        Assert.True(WindowSwap.NeedsChoice(l));
    }

    [Fact]
    public void Custom_auto_pick_works_per_party()
    {
        var l = Custom((50, 50), (20, 20), (20, 20), (20, 20), (20, 20), (20, 20), (20, 20), (40, 40), (20, 20));
        Assert.Equal([0, 7], WindowSwap.MainSlots(l));
    }

    [Fact]
    public void A_loaded_captured_layout_finds_its_large_window() =>
        Assert.Equal([0], WindowSwap.MainSlots(Side() with { Preset = null }));

    [Fact]
    public void Grid_still_asks_even_when_cells_differ() =>
        Assert.True(WindowSwap.NeedsChoice(Custom((50, 50), (20, 20), (20, 20)) with { Preset = PresetKind.Grid }));

    [Fact]
    public void A_chosen_crown_beats_the_largest_window() =>
        Assert.Equal([2], WindowSwap.MainSlots(Custom((66, 100), (34, 50), (34, 50)) with { SwapMainSlots = [3] }));

    [Fact]
    public void The_bug_layout_from_the_beta_test_has_a_main_slot_again()
    {
        // Jakob's "Trio" after the drag-swap: Custom, the large window in slot 2, no crown stored
        var trio = Side() with { Preset = PresetKind.Custom, Slots = Swapped(Side(), 0, 1).Slots, SwapMainSlots = null };
        Assert.Equal([1], WindowSwap.MainSlots(trio));
        var mains = WindowSwap.MainSlots(trio);
        Assert.Equal(1, WindowSwap.MainSlotFor(mains, 0, 3));   // "client 1 to main" works
    }
}

public class OverlayShortcutTests
{
    [Theory]
    [InlineData("Alt+Z")]
    [InlineData("Alt+R")]
    [InlineData("Alt+F9")]
    [InlineData("Ctrl+Shift+S")]
    public void Graphics_overlay_shortcuts_get_a_warning(string text)
    {
        Assert.True(Hotkeys.TryParse(text, out var k));
        Assert.NotNull(Hotkeys.KnownOverlayShortcut(k!));
        Assert.Contains(Hotkeys.Warnings(k!), w => w.Contains("overlay"));
    }

    [Fact]
    public void The_default_swap_hotkeys_clash_with_no_overlay() =>
        Assert.All(Hotkeys.Defaults.Values, k => Assert.Null(Hotkeys.KnownOverlayShortcut(k)));

    [Theory]
    [InlineData("Ctrl+Shift+M")]   // the new "Same size" shortcut in the app
    [InlineData("Ctrl+Alt+Shift+Z")]
    public void Other_combinations_are_not_flagged(string text)
    {
        Assert.True(Hotkeys.TryParse(text, out var k));
        Assert.Null(Hotkeys.KnownOverlayShortcut(k!));
    }
}
