using System.Windows.Media;

namespace WoWBoxer.Layouts.App;

/// <summary>A WoW class for colour accents (official class colours, as on the website). Names only, no Blizzard art.</summary>
public sealed record WowClass(string Id, string Name, Color Color)
{
    public SolidColorBrush Brush { get; } = Freeze(new SolidColorBrush(Color));

    private static SolidColorBrush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }

    public override string ToString() => Name;
}

public static class WowClasses
{
    /// <summary>"No class chosen": the slot gets a default accent for its position.</summary>
    public static readonly WowClass Automatic = new("", "Automatic", Color.FromRgb(0x5A, 0x63, 0x78));

    public static readonly IReadOnlyList<WowClass> All =
    [
        new("warrior", "Warrior", Color.FromRgb(0xC6, 0x9B, 0x6D)),
        new("paladin", "Paladin", Color.FromRgb(0xF4, 0x8C, 0xBA)),
        new("hunter", "Hunter", Color.FromRgb(0xAA, 0xD3, 0x72)),
        new("rogue", "Rogue", Color.FromRgb(0xFF, 0xF4, 0x68)),
        new("priest", "Priest", Color.FromRgb(0xFF, 0xFF, 0xFF)),
        new("shaman", "Shaman", Color.FromRgb(0x00, 0x70, 0xDD)),
        new("mage", "Mage", Color.FromRgb(0x3F, 0xC7, 0xEB)),
        new("warlock", "Warlock", Color.FromRgb(0x87, 0x88, 0xEE)),
        new("druid", "Druid", Color.FromRgb(0xFF, 0x7C, 0x0A)),
    ];

    /// <summary>Accent order when a slot has no class yet (the website's party: paladin, priest, warlock …).</summary>
    private static readonly string[] DefaultOrder = ["paladin", "priest", "warlock", "warrior", "mage", "druid", "hunter", "rogue"];

    public static WowClass? Find(string? id) => All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The slot's class colour, or a default accent for its position.</summary>
    public static Color ColorFor(string? id, int index) => (Find(id) ?? Find(DefaultOrder[index % DefaultOrder.Length])!).Color;
}
