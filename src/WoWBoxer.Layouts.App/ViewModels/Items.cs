using System.Windows.Input;
using System.Windows.Media;
using WoWBoxer.Layouts.App.Infrastructure;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App.ViewModels;

public enum AppTab { Setup, Squad, Guide, Settings, About }

/// <summary>A saved layout in the "Saved layouts" list.</summary>
public sealed class ProfileItem(StoredProfile stored) : ObservableObject
{
    public string Path { get; set; } = stored.Path;
    public LayoutProfile Profile { get; set; } = stored.Profile;
    public string Name => Profile.Name;
    public string Summary => $"{Profile.Clients} clients · {(Profile.Preset is { } p ? Presets.DisplayName(p) : "custom")}";

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Summary));
    }

    public override string ToString() => Name;
}

/// <summary>One of the big layout choices on the Setup tab, with a live mini preview for the current client count.</summary>
public sealed class LayoutCard(PresetKind kind, string title, string blurb, ICommand apply) : ObservableObject
{
    private LayoutProfile? _preview;
    private bool _isSelected;
    private string? _unavailable;

    public PresetKind Kind { get; } = kind;
    public string Title { get; } = title;
    public string Blurb { get; } = blurb;
    public ICommand Apply { get; } = apply;
    public LayoutProfile? Preview { get => _preview; set => Set(ref _preview, value); }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    /// <summary>Why this card cannot be used right now (e.g. only one monitor), or null.</summary>
    public string? Unavailable
    {
        get => _unavailable;
        set
        {
            if (Set(ref _unavailable, value)) OnPropertyChanged(nameof(IsAvailable));
        }
    }

    public bool IsAvailable => _unavailable is null;
}

public enum PartyState { Waiting, Placed, Elsewhere, Problem }

/// <summary>
/// One slot of the squad on the Squad tab: the character (name, class – set by hand, never guessed), the WoW account
/// it plays on (picked from the WTF folders), the window currently in that slot and its status.
/// </summary>
public sealed class PartyMember : ObservableObject
{
    private PartyState _state;
    private string _account = "";
    private string _name = "";
    private WowClass _class = WowClasses.Automatic;
    private string _window = "No window yet";
    private bool _dropTarget;

    public int Number { get; init; }
    public int Index => Number - 1;
    public string Role { get; init; } = "";
    public IReadOnlyList<WowClass> Classes { get; } = [WowClasses.Automatic, .. WowClasses.All];
    public IReadOnlyList<string> Accounts { get; init; } = [];
    /// <summary>Last characters per WoW account (from WTF folder times): a hint, never a fact.</summary>
    public IReadOnlyDictionary<string, WtfCharacter?> LastPlayed { get; init; } = new Dictionary<string, WtfCharacter?>();
    public Action<int, string>? NameChanged { get; init; }
    public Action<int, WowClass>? ClassChanged { get; init; }
    public Action<int, string>? AccountChanged { get; init; }
    public ICommand? Identify { get; init; }
    public ICommand UseHint => new RelayCommand(() => { if (Hint is { } h) Name = h.Name; }, () => Hint is not null && Hint.Name != Name);

    /// <summary>Character name typed by the user ("" = not set).</summary>
    public string Name
    {
        get => _name;
        set
        {
            value = value.Trim();
            if (!Set(ref _name, value)) return;
            OnPropertyChanged(nameof(Title));
            NameChanged?.Invoke(Index, value);
        }
    }

    public WowClass Class
    {
        get => _class;
        set
        {
            value ??= WowClasses.Automatic;
            if (!Set(ref _class, value)) return;
            OnPropertyChanged(nameof(ClassBrush));
            OnPropertyChanged(nameof(Title));
            ClassChanged?.Invoke(Index, value);
        }
    }

    public Brush ClassBrush => _class.Brush;
    public string Title => _name.Length > 0 ? _name : Role == "Main" ? "Main" : $"Follower {Number - 1}";

    /// <summary>The WoW account folder this slot plays on (local setting, never in shared layouts).</summary>
    public string Account
    {
        get => _account;
        set
        {
            value ??= "";
            if (!Set(ref _account, value)) return;
            OnPropertyChanged(nameof(Hint));
            OnPropertyChanged(nameof(HintText));
            AccountChanged?.Invoke(Index, value);
        }
    }

    public WtfCharacter? Hint => _account.Length > 0 && LastPlayed.TryGetValue(_account, out var c) ? c : null;
    public string HintText => Hint is { } h ? $"Last played here: {h.Display}" : _account.Length > 0 ? "No characters found for this account" : "Pick the WoW account for hints";

    public string Window { get => _window; set => Set(ref _window, value); }

    /// <summary>True while a row is dragged over this one (drop highlight).</summary>
    public bool IsDropTarget { get => _dropTarget; set => Set(ref _dropTarget, value); }

    public PartyState State
    {
        get => _state;
        set
        {
            if (Set(ref _state, value)) OnPropertyChanged(nameof(StateText));
        }
    }

    public string StateText => State switch
    {
        PartyState.Placed => "Placed",
        PartyState.Elsewhere => "Not in place",
        PartyState.Problem => "Check Activity",
        _ => "No window",
    };

    /// <summary>Sets values from the layout without calling back (no save loop).</summary>
    public void Load(string name, WowClass cls, string account)
    {
        _name = name;
        _class = cls;
        _account = account;
        foreach (var p in new[] { nameof(Name), nameof(Class), nameof(ClassBrush), nameof(Title), nameof(Account), nameof(Hint), nameof(HintText) }) OnPropertyChanged(p);
    }
}

/// <summary>A line of the "Start your squad" checklist.</summary>
public sealed class CheckItem(string label) : ObservableObject
{
    private bool _done;
    public string Label { get; } = label;
    public bool Done { get => _done; set => Set(ref _done, value); }
}

/// <summary>An open WoW window in launch order (the order decides who gets which slot).</summary>
public sealed class GameWindowItem(WindowInfo window) : ObservableObject
{
    private int _slot;
    public WindowInfo Window { get; } = window;
    public int Slot { get => _slot; set => Set(ref _slot, value); }
    public string Process => $"{Window.ProcessName}.exe";
    public string Detail => $"pid {Window.Pid} · {WindowFinder.VisibleRectOf(Window.Handle)}";
}

public sealed record AspectChoice(double Value, string Label)
{
    public override string ToString() => Label;
}
