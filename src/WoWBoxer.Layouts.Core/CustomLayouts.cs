using System.Text.Json.Nodes;

namespace WoWBoxer.Layouts.Core;

/// <summary>
/// The Custom sandbox for one saved layout: <see cref="Custom"/> is the user's arrangement (also what the layout file
/// holds while it is Custom), <see cref="Before"/> the layout as it was when Custom began (what Discard goes back to),
/// <see cref="Unsaved"/> true while the arrangement was neither saved under a name nor kept on purpose.
/// </summary>
public sealed record CustomDraft(LayoutProfile Custom, LayoutProfile? Before, bool Unsaved);

/// <summary>The answer to "Save your custom layout?" when leaving Custom with unsaved changes.</summary>
public enum LeaveChoice { SaveAs, KeepDraft, Discard }

/// <summary>
/// Custom (was "Free for all"): choosing it never jumps to fixed positions; editing any preset switches to it; every
/// change is kept as a draft (<see cref="CustomDraftStore"/>), so nothing is lost. Pure rules, no UI.
/// </summary>
public static class CustomLayouts
{
    /// <summary>True when every window has the same place (monitor, position, size) in both layouts.</summary>
    public static bool SameGeometry(LayoutProfile a, LayoutProfile b) =>
        a.Slots.Count == b.Slots.Count && a.Slots.Zip(b.Slots).All(p => p.First.Monitor == p.Second.Monitor
            && Same(p.First.X, p.Second.X) && Same(p.First.Y, p.Second.Y) && Same(p.First.W, p.Second.W) && Same(p.First.H, p.Second.H));

    private static bool Same(double a, double b) => Math.Abs(a - b) < 0.0001;

    /// <summary>
    /// A window of a preset (Main + side, Overlays, Grid) was moved or resized: the layout becomes Custom from exactly
    /// that arrangement, and the preset is remembered for Discard. Edits to a layout that is already Custom keep its draft.
    /// </summary>
    public static (LayoutProfile Profile, CustomDraft Draft) Edited(LayoutProfile before, LayoutProfile edited, CustomDraft? draft)
    {
        if (before.Preset == PresetKind.Custom || before.Preset is null)
        {
            var custom = edited with { Preset = PresetKind.Custom };
            // a change in a Custom session is unsaved again (also after "Keep as draft"); a layout saved as Custom under
            // its own name (no draft) is saved by every change anyway, so it never asks
            return (custom, draft is null ? new CustomDraft(custom, null, Unsaved: false) : new CustomDraft(custom, draft.Before, Unsaved: true));
        }
        var started = edited with { Preset = PresetKind.Custom };
        return (started, new CustomDraft(started, before, Unsaved: true));
    }

    /// <summary>
    /// The Custom card was picked: the last draft of this layout comes back if it fits (same number of clients),
    /// otherwise the arrangement on the map now simply becomes Custom. Never fixed default positions.
    /// </summary>
    public static (LayoutProfile Profile, CustomDraft Draft) Choose(LayoutProfile current, CustomDraft? draft)
    {
        if (current.Preset == PresetKind.Custom) return (current, draft ?? new CustomDraft(current, null, false));
        if (draft is not null && draft.Custom.Slots.Count == current.Slots.Count)
        {
            var restored = current with
            {
                Preset = PresetKind.Custom,
                Slots = draft.Custom.Slots.Select((s, i) => s with { Name = current.Slots[i].Name, Class = current.Slots[i].Class, CombatRole = current.Slots[i].CombatRole }).ToList(),
                Screens = draft.Custom.Screens,
            };
            return (restored, draft with { Custom = restored, Before = current });
        }
        var kept = current with { Preset = PresetKind.Custom };
        return (kept, new CustomDraft(kept, current, Unsaved: false));
    }

    /// <summary>Leaving Custom (another layout card or another saved layout) asks only while there are unsaved changes.</summary>
    public static bool AsksBeforeLeaving(LayoutProfile current, CustomDraft? draft) =>
        current.Preset == PresetKind.Custom && draft is { Unsaved: true };

    /// <summary>
    /// What each answer does to the layout being left and to its draft. <paramref name="switchingLayout"/>: another saved
    /// layout was picked (this one is left as it is, or put back on Discard / after Save as); otherwise another card.
    /// Returns the layout to keep in this file (null = unchanged) and the draft to keep (null = deleted).
    /// </summary>
    public static (LayoutProfile? Keep, CustomDraft? Draft) Leave(CustomDraft draft, LeaveChoice choice, bool switchingLayout) => choice switch
    {
        // the arrangement lives on under its new name; this layout goes back to how it was
        LeaveChoice.SaveAs => (switchingLayout ? draft.Before : null, null),
        // nothing is lost: the draft comes back the next time Custom is chosen
        LeaveChoice.KeepDraft => (null, draft with { Unsaved = false }),
        _ => (switchingLayout ? draft.Before : null, null),
    };

    /// <summary>
    /// A new number of clients in Custom: the windows that exist keep their places; only new ones get a place, from
    /// <paramref name="start"/> (the loose Custom start for the new number).
    /// </summary>
    public static IReadOnlyList<LayoutSlot> WithCount(IReadOnlyList<LayoutSlot> current, LayoutProfile start) =>
        start.Slots.Select((s, i) => i < current.Count ? current[i] with { N = s.N, Role = s.Role } : s).ToList();
}

/// <summary>Custom drafts on disk, one JSON file per saved layout (%APPDATA%\WoWBoxer\Layouts\drafts). Survives restarts.</summary>
public sealed class CustomDraftStore(string folder)
{
    public string Folder { get; } = folder;

    private string PathFor(string layoutName) => System.IO.Path.Combine(Folder, ProfileStore.FileNameFor(layoutName));

    public CustomDraft? Load(string layoutName)
    {
        try
        {
            var path = PathFor(layoutName);
            if (!File.Exists(path)) return null;
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject o || o["custom"] is not JsonObject custom) return null;
            var before = o["before"] is JsonObject b ? ProfileJson.Deserialize(b.ToJsonString()) : null;
            return new CustomDraft(ProfileJson.Deserialize(custom.ToJsonString()), before, o["unsaved"]?.GetValue<bool>() ?? false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;   // a broken draft is just no draft
        }
    }

    public void Save(string layoutName, CustomDraft? draft)
    {
        try
        {
            var path = PathFor(layoutName);
            if (draft is null)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            Directory.CreateDirectory(Folder);
            var o = new JsonObject
            {
                ["unsaved"] = draft.Unsaved,
                ["custom"] = JsonNode.Parse(ProfileJson.Serialize(draft.Custom)),
                ["before"] = draft.Before is null ? null : JsonNode.Parse(ProfileJson.Serialize(draft.Before)),
            };
            var temp = path + ".tmp";
            File.WriteAllText(temp, o.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a draft is a safety net; never crash over it
        }
    }

    /// <summary>The layout was renamed: its draft follows.</summary>
    public void Rename(string oldName, string newName)
    {
        if (Load(oldName) is not { } d) return;
        Save(newName, d);
        Save(oldName, null);
    }
}
