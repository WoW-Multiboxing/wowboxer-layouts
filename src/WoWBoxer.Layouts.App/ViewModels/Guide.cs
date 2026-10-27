using System.Diagnostics;
using System.Windows.Input;
using WoWBoxer.Layouts.App.Infrastructure;

namespace WoWBoxer.Layouts.App.ViewModels;

/// <summary>One chapter of the quick guide (quest-log style: objectives, a "good to know" note, an optional helper).</summary>
public sealed class GuideChapter(int number, string title, string intro, string[] objectives, string note, string? action = null, ICommand? actionCommand = null) : ObservableObject
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

    public GuideViewModel()
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
                    "Choose 2–8 clients with the big number buttons.",
                    "Pick Main + side, Overlays, Grid, 2 monitors or Free for all.",
                    "Fine-tune by dragging windows on the map. A green dot means the whole action bar fits; a solid dot is measured on the open game window, a ring is the plan.",
                ],
                "Everything is saved automatically, and the app opens with your last setup next time."),
            new(3, "Start your squad",
                "Press Start squad. Each window snaps into its place as it opens – you choose how the clients start.",
                [
                    "Battle.net (default): press Play with account 1, then 2, then 3 … – pick the account in Battle.net each time.",
                    "Login screens: the app opens one WoW login screen per missing client; you log in to each yourself.",
                    "Wrong order? Drag the rows on the Squad tab by ⋮⋮, or use Identify to see which window is which.",
                ],
                "WoWBoxer never logs in for you and never types anything. \"Close clients I started\" only closes the login screens it opened."),
            new(4, "Switch between windows",
                "Two ways to move between clients. Pick what feels right – both keep one key press = one action on one character.",
                [
                    "Alt+Tab or clicking a window works without any extra setting.",
                    "Hover to activate: Windows can activate the window your mouse points at (Ease of Access → \"Make the mouse easier to use\").",
                    "With overlays, set the floating followers to \"Always on top\" so the main window cannot cover them – and leave the main window itself off \"on top\".",
                ],
                "Blizzard has not clearly said whether hover-to-activate counts as \"streamlining\". Alt+Tab and clicking are the safe defaults.",
                "Open the Windows hover setting",
                new RelayCommand(OpenHoverSetting)),
            new(5, "If something looks wrong",
                "Most problems have a simple cause. The app tells you what it sees.",
                [
                    "Windows do not move: Battle.net runs as administrator. Start it normally and try again.",
                    "Action bar ends cut off: the game picture is narrower than 4:3. Press \"Fix action bar\" (one smaller UI Scale for all clients – written while WoW is closed, or it tells you what to set in game), or \"Shorten window instead\".",
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

    /// <summary>Opens the Windows page that holds "Activate a window by hovering over it with the mouse".</summary>
    private static void OpenHoverSetting() =>
        Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.EaseOfAccessCenter /page pageEasierToUseMouse") { UseShellExecute = true })?.Dispose();
}
