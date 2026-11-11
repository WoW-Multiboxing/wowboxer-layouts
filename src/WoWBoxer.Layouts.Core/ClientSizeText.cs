namespace WoWBoxer.Layouts.Core;

/// <summary>What the app says when a game client keeps its own window size (bug 2026-11-10, Forever).</summary>
public static class ClientSizeText
{
    /// <summary>"Window 2: the game keeps this window at 928×696 instead of 870×696 …" with what to change.</summary>
    public static string KeepsSize(int window, PixelRect wanted, PixelRect kept, WowGame game)
    {
        var size = wanted.Width > 0 ? $"{kept.Width}×{kept.Height} instead of {wanted.Width}×{wanted.Height}" : $"{kept.Width}×{kept.Height}";
        var text = $"Window {window}: the game keeps this window at {size}, so WoWBoxer stopped resizing it (trying again only makes it flip between the two sizes).";
        var hint = wanted.Width > 0 && kept.Width > wanted.Width && kept.Height >= wanted.Height
            ? " It does not get narrower than that: pick a wider layout (Layout › Window rules › Minimum width 4:3), or make that window larger."
            : " In WoW set Display Mode = Windowed, then press Re-arrange.";
        return text + hint;
    }
}
