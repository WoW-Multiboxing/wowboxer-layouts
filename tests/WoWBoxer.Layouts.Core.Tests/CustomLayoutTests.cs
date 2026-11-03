namespace WoWBoxer.Layouts.Core.Tests;

public class CustomLayoutTests
{
    private static LayoutProfile Side(int n = 3) =>
        Presets.ForScreens(PresetKind.Side, n, TestMonitors.Reference) with { Name = "My squad", Slots = Presets.ForScreens(PresetKind.Side, n, TestMonitors.Reference).Slots.Select(s => s with { Name = $"C{s.N}" }).ToList() };

    private static LayoutProfile Moved(LayoutProfile p) => p with { Slots = p.Slots.Select((s, i) => i == 1 ? s with { X = s.X - 5 } : s).ToList() };

    [Fact]
    public void Editing_a_preset_switches_to_custom_from_that_arrangement()
    {
        var before = Side();
        var (profile, draft) = CustomLayouts.Edited(before, Moved(before), null);
        Assert.Equal(PresetKind.Custom, profile.Preset);
        Assert.True(CustomLayouts.SameGeometry(profile, Moved(before)));
        Assert.Equal(before, draft.Before);
        Assert.True(draft.Unsaved);
        Assert.True(CustomLayouts.AsksBeforeLeaving(profile, draft));
    }

    [Fact]
    public void Editing_custom_keeps_the_original_before()
    {
        var before = Side();
        var (p1, d1) = CustomLayouts.Edited(before, Moved(before), null);
        var (p2, d2) = CustomLayouts.Edited(p1, Moved(p1), d1);
        Assert.Equal(before, d2.Before);
        Assert.True(CustomLayouts.SameGeometry(p2, d2.Custom));
    }

    [Fact]
    public void A_new_change_after_keep_as_draft_is_unsaved_again()
    {
        var before = Side();
        var (p1, d1) = CustomLayouts.Edited(before, Moved(before), null);
        var (_, kept) = CustomLayouts.Leave(d1, LeaveChoice.KeepDraft, switchingLayout: false);
        var (p2, d2) = CustomLayouts.Edited(p1, Moved(p1), kept);
        Assert.True(d2.Unsaved);
        Assert.True(CustomLayouts.AsksBeforeLeaving(p2, d2));
    }

    [Fact]
    public void A_layout_saved_as_custom_never_asks()
    {
        var saved = Side() with { Preset = PresetKind.Custom, Name = "My custom" };
        var (p, d) = CustomLayouts.Edited(saved, Moved(saved), null);
        Assert.False(d.Unsaved);
        Assert.False(CustomLayouts.AsksBeforeLeaving(p, d));
    }

    [Fact]
    public void Choosing_custom_keeps_what_is_on_the_map()
    {
        var current = Side();
        var (profile, draft) = CustomLayouts.Choose(current, null);
        Assert.Equal(PresetKind.Custom, profile.Preset);
        Assert.True(CustomLayouts.SameGeometry(current, profile));   // no jump to fixed positions
        Assert.False(draft.Unsaved);
        Assert.False(CustomLayouts.AsksBeforeLeaving(profile, draft));
    }

    [Fact]
    public void Choosing_custom_brings_the_last_draft_back_with_todays_names()
    {
        var current = Side();
        var (custom, draft) = CustomLayouts.Edited(current, Moved(current), null);
        var renamed = current with { Slots = current.Slots.Select(s => s with { Name = "N" + s.N }).ToList() };
        var (back, _) = CustomLayouts.Choose(renamed, draft with { Unsaved = false });
        Assert.True(CustomLayouts.SameGeometry(back, custom));
        Assert.Equal("N2", back.Slots[1].Name);
    }

    [Fact]
    public void A_draft_for_another_client_count_is_not_forced_on()
    {
        var (_, draft) = CustomLayouts.Edited(Side(3), Moved(Side(3)), null);
        var four = Side(4);
        var (profile, _) = CustomLayouts.Choose(four, draft);
        Assert.True(CustomLayouts.SameGeometry(four, profile));
    }

    [Theory]
    [InlineData(LeaveChoice.SaveAs, false, false, false)]
    [InlineData(LeaveChoice.SaveAs, true, true, false)]
    [InlineData(LeaveChoice.KeepDraft, false, false, true)]
    [InlineData(LeaveChoice.KeepDraft, true, false, true)]
    [InlineData(LeaveChoice.Discard, false, false, false)]
    [InlineData(LeaveChoice.Discard, true, true, false)]
    public void Leaving_custom(LeaveChoice choice, bool switchingLayout, bool putsBack, bool keepsDraft)
    {
        var before = Side();
        var (_, draft) = CustomLayouts.Edited(before, Moved(before), null);
        var (keep, left) = CustomLayouts.Leave(draft, choice, switchingLayout);
        Assert.Equal(putsBack, keep == before);
        Assert.Equal(keepsDraft, left is not null);
        if (left is not null) Assert.False(left.Unsaved);   // asked once: kept on purpose
    }

    [Fact]
    public void More_clients_in_custom_keep_the_existing_places()
    {
        var current = Moved(Side(3)) with { Preset = PresetKind.Custom };
        var start = Presets.ForScreens(PresetKind.Custom, 5, TestMonitors.Reference);
        var slots = CustomLayouts.WithCount(current.Slots, start);
        Assert.Equal(5, slots.Count);
        Assert.Equal(current.Slots[1].X, slots[1].X);
        Assert.Equal(start.Slots[4], slots[4]);
        Assert.Equal([1, 2, 3, 4, 5], slots.Select(s => s.N));
    }

    [Fact]
    public void Drafts_survive_on_disk_and_follow_a_rename()
    {
        var dir = Directory.CreateTempSubdirectory("wbx-drafts-");
        try
        {
            var store = new CustomDraftStore(dir.FullName);
            var before = Side();
            var (_, draft) = CustomLayouts.Edited(before, Moved(before), null);
            store.Save("My squad", draft);
            var loaded = new CustomDraftStore(dir.FullName).Load("My squad")!;
            Assert.True(loaded.Unsaved);
            Assert.Equal(before, loaded.Before);
            Assert.True(CustomLayouts.SameGeometry(draft.Custom, loaded.Custom));
            store.Rename("My squad", "Raid");
            Assert.Null(store.Load("My squad"));
            Assert.NotNull(store.Load("Raid"));
            store.Save("Raid", null);
            Assert.Null(store.Load("Raid"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_broken_draft_is_no_draft()
    {
        var dir = Directory.CreateTempSubdirectory("wbx-drafts-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "X.json"), "{ not json");
            Assert.Null(new CustomDraftStore(dir.FullName).Load("X"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}

public class ImportCodeTests
{
    private static string Code(PresetKind? kind = PresetKind.Side, int n = 3) =>
        ProfileJson.ToShareCode(Presets.ForScreens(PresetKind.Side, n, TestMonitors.Reference) with { Name = "Shared", Preset = kind });

    [Fact]
    public void A_valid_code_is_described()
    {
        var (ok, message, p) = ProfileJson.CheckShareCode(Code());
        Assert.True(ok);
        Assert.Equal("Valid WBX1 code · 3 clients · Main + side", message);
        Assert.Equal(3, p!.Clients);
    }

    [Fact]
    public void Line_breaks_and_spaces_inside_a_pasted_code_are_fine()
    {
        var code = Code(n: 5);
        var wrapped = string.Join("\r\n  ", Enumerable.Range(0, (code.Length + 39) / 40).Select(i => code.Substring(i * 40, Math.Min(40, code.Length - i * 40))));
        Assert.True(ProfileJson.CheckShareCode("  " + wrapped + "\n").Ok);
    }

    [Fact]
    public void Hand_made_and_one_client_wording()
    {
        Assert.EndsWith("· hand-made", ProfileJson.CheckShareCode(Code(kind: null)).Message);
        Assert.Contains("· 1 client ·", ProfileJson.CheckShareCode(Code(n: 1)).Message);
    }

    [Theory]
    [InlineData("", "Paste a code that starts with WBX1:")]
    [InlineData("hello", "A layout code starts with \"WBX1:\".")]
    [InlineData("WBX1:###", "The layout code is damaged (not base64).")]
    public void Clear_errors(string code, string message)
    {
        var (ok, text, p) = ProfileJson.CheckShareCode(code);
        Assert.False(ok);
        Assert.Equal(message, text);
        Assert.Null(p);
    }

    [Fact]
    public void A_cut_off_code_is_an_error_not_a_crash()
    {
        var code = Code();
        Assert.False(ProfileJson.CheckShareCode(code[..(code.Length / 2)]).Ok);
    }
}
