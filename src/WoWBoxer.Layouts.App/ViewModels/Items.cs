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

public enum PartyState { Waiting, Placed, Problem }

/// <summary>One character of the squad on the Squad tab (party-frame style).</summary>
public sealed class PartyMember : ObservableObject
{
    private PartyState _state;
    private string _account = "";
    private string _label = "";
    private Brush _classBrush = Brushes.Gray;

    public int Number { get; init; }
    public string Label { get => _label; set => Set(ref _label, value); }
    public Brush ClassBrush { get => _classBrush; set => Set(ref _classBrush, value); }
    public Action<int, string>? AccountChanged { get; init; }

    public string Account
    {
        get => _account;
        set
        {
            if (Set(ref _account, value)) AccountChanged?.Invoke(Number, value);
        }
    }

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
        PartyState.Placed => "In place",
        PartyState.Problem => "Check Activity",
        _ => "Waiting",
    };
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
