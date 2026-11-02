using System.Windows.Interop;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Owns the window-swap hotkeys: a hidden message-only window that receives WM_HOTKEY. Registers exactly the hotkeys
/// given (all of them are removed again when swap is turned off or the app quits). Windows delivers a registered
/// combination only to this window, so the game never receives it. Nothing else is watched or sent.
/// </summary>
internal sealed class SwapHotkeyHost : IDisposable
{
    private const int FirstId = 0x5741;   // "WA"
    private readonly HwndSource _window;
    private readonly Action<string> _onAction;
    private readonly Dictionary<int, string> _ids = [];

    public SwapHotkeyHost(Action<string> onAction)
    {
        _onAction = onAction;
        // HWND_MESSAGE (-3): a window that is never shown and never takes focus, only for messages
        _window = new HwndSource(new HwndSourceParameters("WoWBoxer swap hotkeys") { ParentWindow = new nint(-3), WindowStyle = 0 });
        _window.AddHook(WndProc);
    }

    /// <summary>The actions whose hotkey is registered now.</summary>
    public IReadOnlyCollection<string> Active => _ids.Values;

    /// <summary>
    /// Replaces the registered hotkeys. Returns the ones Windows refused, with the reason (another program uses it, …).
    /// </summary>
    public IReadOnlyDictionary<string, string> Apply(IReadOnlyDictionary<string, Hotkey> keys)
    {
        Clear();
        var failed = new Dictionary<string, string>();
        var id = FirstId;
        foreach (var action in SwapActions.All)
        {
            if (!keys.TryGetValue(action, out var key)) continue;
            var error = GlobalHotkeys.Register(_window.Handle, id, key);
            if (error == 0) _ids[id++] = action;
            else failed[action] = error == GlobalHotkeys.ErrorAlreadyRegistered ? "used by another program" : $"Windows refused it (error {error})";
        }
        return failed;
    }

    public void Clear()
    {
        foreach (var id in _ids.Keys) GlobalHotkeys.Unregister(_window.Handle, id);
        _ids.Clear();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == GlobalHotkeys.WM_HOTKEY && _ids.TryGetValue((int)wParam, out var action))
        {
            handled = true;
            _onAction(action);
        }
        return 0;
    }

    public void Dispose()
    {
        Clear();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }
}
