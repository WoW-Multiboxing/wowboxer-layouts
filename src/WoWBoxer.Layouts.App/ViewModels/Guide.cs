using System.Windows.Input;
using WoWBoxer.Layouts.App.Infrastructure;

namespace WoWBoxer.Layouts.App.ViewModels;

/// <summary>One chapter of the quick guide (quest-log style: objectives, a "good to know" note, an optional helper).</summary>
public sealed class GuideChapter(int number, string title, string intro, string[] objectives, string note, string? action = null, ICommand? actionCommand = null,
    HoverViewModel? hover = null) : ObservableObject
{
    private bool _isCurrent;

    public int Number { get; } = number;
    public string Title { get; } = title;
    public string Intro { get; } = intro;
    public IReadOnlyList<string> Objectives { get; } = objectives;
    public string Note { get; } = note;
    public string? Action { get; } = action;
    public ICommand? ActionCommand { get; } = actionCommand;
    public bool HasAction => Action is not null;
    /// <summary>The chapter's own hover-to-activate switch (Switch between windows only).</summary>
    public HoverViewModel? Hover { get; } = hover;
    public bool HasHover => Hover is not null;
    public string Mark => IsCurrent ? "!" : "?";

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (Set(ref _isCurrent, value)) OnPropertyChanged(nameof(Mark));
        }
    }
}

public sealed class GuideViewModel : ObservableObject
{
    private GuideChapter _current;

    public const int SwitchingChapter = 4;

    public GuideViewModel(HoverViewModel hover)
    {
        Chapters =
        [
            new(1, "Prepare your WoW clients",
                "A few game settings make every window look sharp and run smoothly. You only do this once – all clients from one install share them.",
                [
                    "Esc → Options → Graphics → Display Mode: Windowed. Forever starts in \"Windowed (Fullscreen)\" – then it maximises itself again and cannot be placed, so switch it once.",
                    "Render Scale: 100 % so the picture stays crisp in small windows.",
                    "Raise Background FPS (Graphics → Advanced, or /console maxFPSBk 30) so follower windows stay smooth while you play the main.",
                ],
                "Keep the title bars on (the app's default). In our tests WoW sometimes drew its action bars at the wrong size after a title bar was removed."),
            new(2, "Pick clients and a layout",
                "On the Setup tab, choose how many clients you run, then click a layout card. Your screens update instantly.",
                [
                    "Choose 2–8 clients with the big number buttons – or 1 for a normal single window when you play one character (no need to close the app).",
                    "Pick Main + side, Overlays, Grid, 2 monitors or Free for all.",
                    "Fine-tune by dragging windows on the map. A green dot means the whole action bar fits; a solid dot is measured on the open game window, a ring is the plan.",
                ],
                "Everything is saved automatically, and the app opens with your last setup next time. Layouts work with every WoW install."),
            new(3, "Open your clients",
                "Press Start placing windows: from then on every WoW window that opens goes straight into the next free slot. Until you press it, the app leaves every window alone.",
                [
                    "Start placing windows (bottom right): the app places the squad's windows. Stop leaves them where they are – for example while you play something else.",
                    "Press \"Open N WoW clients\": one login screen per empty slot. You log in to each yourself.",
                    "Prefer Battle.net? Click \"Open Battle.net instead\" and press Play once per account.",
                    "Each slot shows Awaiting… until its window is there, then Connected. Wrong order? Drag the rows under \"Your squad\" by ⋮⋮, or open ▾ and press Identify.",
                    "All connected? The button becomes Re-arrange: it snaps every window back into its slot.",
                ],
                "WoWBoxer never logs in for you and never types anything. Windows of another WoW install (say Forever next to Classic Era) are left alone – \"Include\" adds them. Stop (or the tray menu) leaves all windows alone; the app also opens stopped."),
            new(SwitchingChapter, "Switch between windows",
                "Hover to activate is the WoWBoxer way to switch: the window under your mouse becomes active, so your next key press goes to that character. One key press = one action on one character.",
                [
                    "The switch below turns on Windows' own \"Activate a window by hovering over it with the mouse\" – the window under your mouse becomes active. Turning it on sets the delay to 30 ms, the default: windows do not activate while the mouse just passes over them. Move the slider for faster or slower; Reset to default puts 30 ms back.",
                    "With overlays, leave \"Bring window to front\" off and set the floating followers to \"Always on top\" (the pin in Your squad), so the main window can never cover them.",
                    "Closing WoWBoxer turns hover to activate off again (Settings can keep it on). WoWBoxer also keeps your own Windows values from before you first used the switch: \"Restore my original Windows settings\" in Settings › Switching windows puts them back, and uninstalling does it too.",
                ],
                "Hover to activate is a Windows setting. It sends nothing to the game; WoWBoxer never sends keys or clicks. It is off until you turn it on.",
                hover: hover),
            new(5, "If something looks wrong",
                "Most problems have a simple cause. The app tells you what it sees.",
                [
                    "Windows do not move: Battle.net runs as administrator. Start it normally and try again.",
                    "Action bar ends cut off: the game picture is narrower than 4:3. Auto-fit action bar (on by default) sets one smaller UI Scale the next time all clients are closed. Or Settings › Fix action bar now, or ▾ › Shorten this window instead.",
                    "Still stuck: Settings → Diagnose writes a report you can send. It stays on your PC until you do.",
                ],
                "Reports hide your Windows user name, so they are safe to share."),
            new(6, "What WoWBoxer never does",
                "WoWBoxer only places and sizes your game windows. That is the whole job.",
                [
                    "Never sends keys or mouse clicks to the game – no broadcasting, no round robin.",
                    "Never reads game memory or injects anything.",
                    "Never logs in for you, and sends no telemetry.",
                ],
                "That is why it runs without administrator rights and is fully open source."),
        ];
        _current = Chapters[0];
        _current.IsCurrent = true;
        PickCommand = new RelayCommand(p =>
        {
            if (p is GuideChapter c) Current = c;
        });
    }

    public IReadOnlyList<GuideChapter> Chapters { get; }
    public ICommand PickCommand { get; }
    public string ChapterText => $"CHAPTER {Current.Number} OF {Chapters.Count}";

    public GuideChapter Current
    {
        get => _current;
        set
        {
            _current.IsCurrent = false;
            if (!Set(ref _current, value)) return;
            value.IsCurrent = true;
            OnPropertyChanged(nameof(ChapterText));
        }
    }
}
