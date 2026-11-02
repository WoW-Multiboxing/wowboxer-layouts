namespace WoWBoxer.Layouts.Core;

/// <summary>Modifier keys of a hotkey (the same bits as Windows' RegisterHotKey).</summary>
[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

/// <summary>
/// A global hotkey: modifiers + one key (a Windows virtual-key code). Written as text like "Ctrl+Alt+Shift+Right" in
/// settings. Used only by window swap (Settings › Advanced); the app registers it with RegisterHotKey, so the key press
/// goes to WoWBoxer and never to the game.
/// </summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, int Key)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Hotkeys.KeyName(Key));
        return string.Join("+", parts);
    }

    public int ModifierCount => (Modifiers.HasFlag(HotkeyModifiers.Ctrl) ? 1 : 0) + (Modifiers.HasFlag(HotkeyModifiers.Alt) ? 1 : 0)
        + (Modifiers.HasFlag(HotkeyModifiers.Shift) ? 1 : 0) + (Modifiers.HasFlag(HotkeyModifiers.Win) ? 1 : 0);
}

/// <summary>The window-swap actions a hotkey can trigger.</summary>
public static class SwapActions
{
    public const string Next = "next";
    public const string Previous = "previous";
    public const string Home = "home";
    public static string Client(int n) => $"client:{n}";

    /// <summary>Every action in the order the hotkey editor lists them.</summary>
    public static IReadOnlyList<string> All { get; } = [Next, Previous, .. Enumerable.Range(1, Presets.MaxClients).Select(Client), Home];

    public static string Describe(string action) => action switch
    {
        Next => "Next client to main",
        Previous => "Previous client to main",
        Home => "Back to home layout",
        _ when ClientNumber(action) is { } n => $"Client {n} to main",
        _ => action,
    };

    /// <summary>1–10 for "client:N", else null.</summary>
    public static int? ClientNumber(string action) =>
        action.StartsWith("client:", StringComparison.Ordinal) && int.TryParse(action.AsSpan(7), out var n) && n is >= 1 and <= Presets.MaxClients ? n : null;
}

public static class Hotkeys
{
    public const int VkBack = 0x08, VkTab = 0x09, VkReturn = 0x0D, VkEscape = 0x1B, VkSpace = 0x20,
        VkPageUp = 0x21, VkPageDown = 0x22, VkEnd = 0x23, VkHome = 0x24, VkLeft = 0x25, VkUp = 0x26, VkRight = 0x27, VkDown = 0x28,
        VkInsert = 0x2D, VkDelete = 0x2E, VkF1 = 0x70, VkNumPad0 = 0x60, VkSnapshot = 0x2C;

    private const HotkeyModifiers Cas = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift;

    /// <summary>The recommended defaults: Ctrl+Alt+Shift with arrows, 1…0 and Backspace (rarely used by Windows or WoW).</summary>
    public static IReadOnlyDictionary<string, Hotkey> Defaults { get; } = new Dictionary<string, Hotkey>
    {
        [SwapActions.Next] = new(Cas, VkRight),
        [SwapActions.Previous] = new(Cas, VkLeft),
        [SwapActions.Home] = new(Cas, VkBack),
        [SwapActions.Client(1)] = new(Cas, '1'), [SwapActions.Client(2)] = new(Cas, '2'), [SwapActions.Client(3)] = new(Cas, '3'),
        [SwapActions.Client(4)] = new(Cas, '4'), [SwapActions.Client(5)] = new(Cas, '5'), [SwapActions.Client(6)] = new(Cas, '6'),
        [SwapActions.Client(7)] = new(Cas, '7'), [SwapActions.Client(8)] = new(Cas, '8'), [SwapActions.Client(9)] = new(Cas, '9'),
        [SwapActions.Client(10)] = new(Cas, '0'),
    };

    /// <summary>
    /// The hotkeys in effect: the user's text per action (missing = the default, empty = cleared). Unreadable text
    /// counts as cleared.
    /// </summary>
    public static IReadOnlyDictionary<string, Hotkey> Effective(IReadOnlyDictionary<string, string>? user)
    {
        var result = new Dictionary<string, Hotkey>();
        foreach (var action in SwapActions.All)
        {
            if (user is not null && user.TryGetValue(action, out var text))
            {
                if (TryParse(text, out var k)) result[action] = k;
            }
            else if (Defaults.TryGetValue(action, out var d)) result[action] = d;
        }
        return result;
    }

    public static string KeyName(int vk) => vk switch
    {
        >= '0' and <= '9' or >= 'A' and <= 'Z' => ((char)vk).ToString(),
        >= VkF1 and <= VkF1 + 23 => $"F{vk - VkF1 + 1}",
        >= VkNumPad0 and <= VkNumPad0 + 9 => $"Num{vk - VkNumPad0}",
        VkBack => "Backspace", VkTab => "Tab", VkReturn => "Enter", VkEscape => "Esc", VkSpace => "Space",
        VkPageUp => "PageUp", VkPageDown => "PageDown", VkEnd => "End", VkHome => "Home",
        VkLeft => "Left", VkUp => "Up", VkRight => "Right", VkDown => "Down", VkInsert => "Insert", VkDelete => "Delete",
        VkSnapshot => "PrintScreen",
        0xBA => ";", 0xBB => "=", 0xBC => ",", 0xBD => "-", 0xBE => ".", 0xBF => "/", 0xC0 => "`", 0xDB => "[", 0xDC => "\\", 0xDD => "]", 0xDE => "'",
        _ => $"Key{vk}",
    };

    private static readonly Dictionary<string, int> Names = BuildNames();

    private static Dictionary<string, int> BuildNames()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var vk = 1; vk < 256; vk++)
        {
            var name = KeyName(vk);
            if (!name.StartsWith("Key", StringComparison.Ordinal)) d.TryAdd(name, vk);
        }
        d["Back"] = VkBack;
        d["Escape"] = VkEscape;
        d["Del"] = VkDelete;
        d["Ins"] = VkInsert;
        d["PgUp"] = VkPageUp;
        d["PgDn"] = VkPageDown;
        d["Return"] = VkReturn;
        return d;
    }

    /// <summary>Reads "Ctrl+Alt+Shift+Right" (any order, case-insensitive; "Control" also works). Needs exactly one non-modifier key.</summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = new Hotkey(HotkeyModifiers.None, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        var mods = HotkeyModifiers.None;
        int? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            var part = raw.Length == 0 && text.TrimEnd().EndsWith('+') ? "=" : raw;   // tolerate a literal '+' key typed as "Ctrl++"
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= HotkeyModifiers.Ctrl; continue;
                case "alt": mods |= HotkeyModifiers.Alt; continue;
                case "shift": mods |= HotkeyModifiers.Shift; continue;
                case "win" or "windows": mods |= HotkeyModifiers.Win; continue;
            }
            if (key is not null || !Names.TryGetValue(part, out var vk)) return false;
            key = vk;
        }
        if (key is null) return false;
        hotkey = new Hotkey(mods, key.Value);
        return true;
    }

    /// <summary>
    /// Warnings for one hotkey: fewer than two modifiers (easy to hit in game), or a combination Windows itself uses.
    /// Empty = fine.
    /// </summary>
    public static IReadOnlyList<string> Warnings(Hotkey k)
    {
        var list = new List<string>();
        if (k.ModifierCount < 2) list.Add("Uses fewer than two modifier keys – easy to press by accident in the game.");
        if (KnownWindowsShortcut(k) is { } what) list.Add($"Windows uses this combination ({what}).");
        return list;
    }

    /// <summary>A short description if Windows uses this combination, else null.</summary>
    public static string? KnownWindowsShortcut(Hotkey k)
    {
        var m = k.Modifiers;
        if (m.HasFlag(HotkeyModifiers.Win)) return "Windows key shortcuts";
        if (m == (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt) && k.Key == VkDelete) return "Ctrl+Alt+Delete";
        if (m == HotkeyModifiers.Alt && k.Key is VkTab or VkF1 + 3 or VkSpace or VkEscape) return k.Key == VkF1 + 3 ? "Alt+F4 closes the window" : "Alt+Tab / Alt+Space / Alt+Esc";
        if (m == (HotkeyModifiers.Alt | HotkeyModifiers.Shift) && k.Key == VkTab) return "Alt+Shift+Tab";
        if (m == HotkeyModifiers.Ctrl && k.Key == VkEscape) return "Ctrl+Esc opens Start";
        if (m == (HotkeyModifiers.Ctrl | HotkeyModifiers.Shift) && k.Key == VkEscape) return "Ctrl+Shift+Esc opens Task Manager";
        if (k.Key == VkSnapshot) return "Print Screen";
        if (m == (HotkeyModifiers.Alt | HotkeyModifiers.Shift)) return "Alt+Shift switches the keyboard language on many PCs";
        if (m == (HotkeyModifiers.Ctrl | HotkeyModifiers.Shift) && k.Key is >= '0' and <= '9') return "Ctrl+Shift+number switches keyboard layouts on some PCs";
        if (m == (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt) && k.Key is VkLeft or VkRight or VkUp or VkDown) return "screen rotation on some graphics drivers";
        return null;
    }

    /// <summary>Actions that share the same hotkey with an earlier action: action → the action it clashes with.</summary>
    public static IReadOnlyDictionary<string, string> Duplicates(IReadOnlyDictionary<string, Hotkey> keys)
    {
        var seen = new Dictionary<Hotkey, string>();
        var clashes = new Dictionary<string, string>();
        foreach (var action in SwapActions.All)
        {
            if (!keys.TryGetValue(action, out var k)) continue;
            if (seen.TryGetValue(k, out var first)) clashes[action] = first;
            else seen[k] = action;
        }
        return clashes;
    }
}
