using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WoWBoxer.Layouts.App.Infrastructure;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App.ViewModels;

/// <summary>What the view model needs from the window (dialogs, clipboard, overlays).</summary>
public interface IUserDialogs
{
    string? Prompt(string title, string message, string initial);
    bool Confirm(string title, string message);
    string? OpenFile(string filter);
    string? SaveFile(string filter, string fileName);
    void CopyToClipboard(string text);
    void ShowIdentify(IReadOnlyList<(PixelRect Rect, string Number, string Caption)> labels);
}

/// <summary>
/// The whole app's state. Flow: how many clients → pick a layout card → fine-tune on the canvas → start the squad.
/// The current layout is always a saved profile (auto-saved); every edit can be undone and, with "live" on,
/// moves the running WoW windows right away.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    public const string WebsiteGuideUrl = "https://wowboxer.example/guide";   // placeholder until the website is live
    public const string RepoUrl = "https://github.com/WoW-Multiboxing/wowboxer-layouts";

    private readonly ProfileStore _store = new(AppSettings.ProfilesFolder);
    private readonly AppSettings _settings;
    private readonly IUserDialogs _dialogs;
    private readonly Stack<LayoutProfile> _undo = new();
    private readonly SemaphoreSlim _liveLock = new(1, 1);
    private ProfileItem? _selectedProfile;
    private LayoutProfile? _current;
    private int _selectedSlotIndex = -1;
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private CancellationTokenSource? _watch, _live;
    private bool _busy;
    private AppTab _tab;
    private StatusLine? _lastStatus;
    private bool _loaded;   // opening the app never moves windows; only edits do
    private readonly ClientLauncher _launcher = new();

    public MainViewModel(AppSettings settings, IUserDialogs dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
        _tab = Enum.TryParse<AppTab>(settings.LastTab, out var tab) ? tab : AppTab.Setup;

        SetTabCommand = new RelayCommand(p => SelectedTab = Enum.Parse<AppTab>(p!.ToString()!));
        MoreClientsCommand = new RelayCommand(() => ClientCount++, () => ClientCount < Presets.MaxClients);
        FewerClientsCommand = new RelayCommand(() => ClientCount--, () => ClientCount > Presets.MinClients);
        ResetCommand = new RelayCommand(ResetToPreset, () => Current is not null);
        UndoCommand = new RelayCommand(Undo, () => _undo.Count > 0);
        SaveAsCommand = new RelayCommand(SaveAs, () => Current is not null);
        RenameCommand = new RelayCommand(Rename, () => _selectedProfile is not null);
        DeleteCommand = new RelayCommand(Delete, () => _selectedProfile is not null && Profiles.Count > 1);
        CopyCodeCommand = new RelayCommand(CopyCode, () => Current is not null);
        ImportCodeCommand = new RelayCommand(ImportCode);
        ImportFileCommand = new RelayCommand(ImportFile);
        ExportFileCommand = new RelayCommand(ExportFile, () => Current is not null);
        CaptureCommand = new RelayCommand(Capture);
        RefreshWindowsCommand = new RelayCommand(RefreshWindows);
        IdentifyCommand = new RelayCommand(Identify);
        MoveWindowUpCommand = new RelayCommand(p => MoveWindow(p as GameWindowItem, -1));
        MoveWindowDownCommand = new RelayCommand(p => MoveWindow(p as GameWindowItem, 1));
        ArrangeCommand = new RelayCommand(() => _ = ArrangeAsync(), () => !_busy && Current is not null);
        StartSquadCommand = new RelayCommand(() => _ = StartSquadAsync(), () => !_busy && Current is not null);
        StopCommand = new RelayCommand(() => _watch?.Cancel(), () => IsWatching);
        RestoreFramesCommand = new RelayCommand(RestoreFrames);
        OpenLogsCommand = new RelayCommand(() => Open(StatusLog.LogFolder));
        OpenLayoutsFolderCommand = new RelayCommand(() => Open(AppSettings.ProfilesFolder));
        DiagnoseCommand = new RelayCommand(() => _ = DiagnoseAsync(), () => !_busy);
        OpenUrlCommand = new RelayCommand(p => OpenUrl(p as string));
        BrowseBattleNetCommand = new RelayCommand(BrowseBattleNet);
        ShowFirstRunCommand = new RelayCommand(() => FirstRunRequested?.Invoke());
        StartFromSetupCommand = new RelayCommand(() =>
        {
            SelectedTab = AppTab.Squad;
            _ = StartSquadAsync();
        }, () => !_busy && Current is not null);

        Status.Lines.CollectionChanged += (_, _) => LastStatus = Status.Lines.LastOrDefault();
        Cards =
        [
            Card(PresetKind.Side, "Main + side", "Your main client big on the left, the followers stacked on the right."),
            Card(PresetKind.Overlay, "Overlays", "Main client fills the screen; followers float in a row along the bottom."),
            Card(PresetKind.Grid, "Grid", "Every client the same size, in an even grid."),
            Card(PresetKind.Dual, "2 monitors", "Main client on one monitor, the followers on the other."),
            Card(PresetKind.Free, "Free for all", "A loose start – drag every window wherever you like."),
        ];

        CloseMineCommand = new RelayCommand(() => _launcher.CloseMine(Status), () => _launcher.Started.Count > 0);
        RefreshHintsCommand = new RelayCommand(() =>
        {
            ReadWowAccounts();
            Status.Info($"Read {WowAccounts.Count} WoW account(s) and {WowAccounts.Sum(a => a.Characters.Count)} character(s) from the WTF folder.");
        });
        BrowseGameCommand = new RelayCommand(BrowseGame);
        FitActionBarCommand = new RelayCommand(FitActionBar, () => HasSlot);

        RefreshMonitors();
        WowAccounts = GameInstall.Accounts(GameInstall.FindExe(_settings.GameExe));
        LoadProfiles();
        RefreshWindows();
        _loaded = true;
    }

    private LayoutCard Card(PresetKind kind, string title, string blurb) =>
        new(kind, title, blurb, new RelayCommand(() => ApplyKind(kind)));

    // =====================================================================================
    // State
    // =====================================================================================

    public StatusLog Status { get; } = new();
    public GuideViewModel Guide { get; } = new();
    public ObservableCollection<ProfileItem> Profiles { get; } = [];
    public ObservableCollection<GameWindowItem> Windows { get; } = [];
    public ObservableCollection<PartyMember> Party { get; } = [];
    public ObservableCollection<CheckItem> Checklist { get; } = [];
    public IReadOnlyList<LayoutCard> Cards { get; }
    public IReadOnlyList<int> ClientCounts { get; } = Enumerable.Range(Presets.MinClients, Presets.MaxClients - Presets.MinClients + 1).ToList();
    public IReadOnlyList<WowClass> Classes { get; } = [WowClasses.Automatic, .. WowClasses.All];
    public IReadOnlyList<AspectChoice> AspectChoices { get; } =
    [
        new(4.0 / 3, "4:3 – full Classic action bar"),
        new(1.25, "5:4 – a little narrower allowed"),
        new(0, "Off – use the window as drawn"),
    ];

    public event Action? FirstRunRequested;

    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    public AppTab SelectedTab
    {
        get => _tab;
        set
        {
            if (!Set(ref _tab, value)) return;
            _settings.LastTab = value.ToString();
            _settings.Save();
            if (value == AppTab.Squad) RefreshWindows();
        }
    }

    public StatusLine? LastStatus { get => _lastStatus; private set => Set(ref _lastStatus, value); }

    public IReadOnlyList<MonitorInfo> Monitors { get => _monitors; private set => Set(ref _monitors, value); }

    public string MonitorSummary => string.Join("    ", Monitors.Select(m => $"Monitor {m.Index}: {m.Bounds.Width}×{m.Bounds.Height} · {m.ScalePercent} %"));

    public LayoutProfile? Current
    {
        get => _current;
        private set
        {
            if (!Set(ref _current, value)) return;
            foreach (var name in new[] { nameof(FullActionBar), nameof(Borderless), nameof(CoverTaskbar), nameof(SelectedAspect), nameof(ClientCount), nameof(LayoutTitle), nameof(SavedText) })
                OnPropertyChanged(name);
            RefreshCards();
            RefreshParty();
            RaiseSlotProperties();
        }
    }

    public string LayoutTitle => Current is null ? "No layout" : Current.Name;
    public string SavedText => Current is null ? "" : $"Saved automatically as “{Current.Name}”";

    public ProfileItem? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (value is null || !Set(ref _selectedProfile, value)) return;
            _undo.Clear();
            Current = value.Profile;
            SelectedSlotIndex = 0;
            _settings.LastProfile = value.Name;
            _settings.Save();
            ScheduleLive();
        }
    }

    public int SelectedSlotIndex
    {
        get => _selectedSlotIndex;
        set
        {
            if (Set(ref _selectedSlotIndex, value)) RaiseSlotProperties();
        }
    }

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (Set(ref _busy, value)) CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsWatching => _watch is not null;

    // =====================================================================================
    // Step 1 + 2: clients and layout cards
    // =====================================================================================

    /// <summary>How many clients. Changing it re-applies the current layout type for the new number.</summary>
    public int ClientCount
    {
        get => Current?.Clients ?? 3;
        set
        {
            value = Math.Clamp(value, Presets.MinClients, Presets.MaxClients);
            if (Current is null || value == Current.Clients) return;
            ApplyKind(Current.Preset ?? PresetKind.Side, value);
        }
    }

    private void RefreshCards()
    {
        var n = Current?.Clients ?? 3;
        foreach (var card in Cards)
        {
            card.IsSelected = Current?.Preset == card.Kind;
            if (TryPreset(card.Kind, n, out var preview, quiet: true))
            {
                card.Preview = preview;
                card.Unavailable = null;
            }
            else
            {
                card.Preview = null;
                card.Unavailable = card.Kind == PresetKind.Dual ? "Needs a second monitor" : "Not available";
            }
        }
    }

    /// <summary>Applies a layout type (one click on a card), keeping names and classes. Undo brings the old one back.</summary>
    private void ApplyKind(PresetKind kind, int? clients = null)
    {
        if (Current is null || !TryPreset(kind, clients ?? Current.Clients, out var preset, quiet: false)) return;
        var slots = preset.Slots.Select((s, i) => i < Current.Slots.Count ? s with { Name = Current.Slots[i].Name, Class = Current.Slots[i].Class } : s).ToList();
        Update(Current with { Clients = preset.Clients, Preset = kind, Slots = slots });
        SelectedSlotIndex = 0;
    }

    private void ResetToPreset()
    {
        if (Current is null) return;
        ApplyKind(Current.Preset ?? PresetKind.Side);
        Status.Info($"Reset to {Presets.DisplayName(Current.Preset ?? PresetKind.Side)}. Ctrl+Z brings your changes back.");
    }

    private bool TryPreset(PresetKind kind, int clients, out LayoutProfile profile, bool quiet)
    {
        try
        {
            var options = Current?.Options ?? new LayoutOptions { MinAspect = 4.0 / 3 };
            profile = Presets.Create(kind, clients, Monitors, options: options with { Fit = FitMode.ShrinkAnchor });
            return true;
        }
        catch (ArgumentException e)
        {
            if (!quiet) Status.Warn(e.Message.Split(" (Parameter")[0]);
            profile = null!;
            return false;
        }
    }

    // =====================================================================================
    // Window rules
    // =====================================================================================

    public bool Borderless
    {
        get => Current?.Options.Borderless ?? false;
        set => EditOptions(o => o with { Borderless = value });
    }

    /// <summary>Use the whole monitor, including the taskbar's area.</summary>
    public bool CoverTaskbar
    {
        get => Current?.Options.UseTaskbarArea ?? false;
        set
        {
            EditOptions(o => o with { UseTaskbarArea = value });
            OnPropertyChanged(nameof(TaskbarHint));
        }
    }

    public string TaskbarHint => Taskbar.AutoHides()
        ? "Your taskbar hides itself, so covering its area gives the windows the full screen."
        : CoverTaskbar
            ? "Your taskbar is always shown and stays on top: it will cover the bottom of the windows. Turn on \"Automatically hide the taskbar\" in Windows settings, or switch this off."
            : "The strip at the bottom of each monitor is your taskbar. Windows stay above it.";

    public AspectChoice? SelectedAspect
    {
        get => Current is null ? null : AspectChoices.MinBy(c => Math.Abs(c.Value - Current.Options.MinAspect));
        set
        {
            if (value is not null) EditOptions(o => o with { MinAspect = value.Value });
        }
    }

    // =====================================================================================
    // Selected window (inspector)
    // =====================================================================================

    private LayoutSlot? Slot => Current is not null && SelectedSlotIndex >= 0 && SelectedSlotIndex < Current.Slots.Count ? Current.Slots[SelectedSlotIndex] : null;

    private PixelRect SlotRect => Slot is { } s && Monitors.Count > 0
        ? SlotEditing.Raw(s, Core.Monitors.Find(Monitors, s.Monitor) ?? Core.Monitors.Primary(Monitors), Current!.Options)
        : default;

    public bool HasSlot => Slot is not null;
    public string SlotHeader => Slot is { } s ? $"Window {s.N}{(s.Role == SlotRole.Main ? " · main" : "")}" : "Click a window";
    public Brush SlotBrush => Slot is { } s ? new SolidColorBrush(WowClasses.ColorFor(s.Class)) : Brushes.Gray;

    public string SlotName
    {
        get => Slot?.Name ?? "";
        set => EditSlot(s => s with { Name = string.IsNullOrWhiteSpace(value) ? null : value.Trim() });
    }

    public WowClass SlotClass
    {
        get => WowClasses.Find(Slot?.Class) ?? WowClasses.Automatic;
        set => EditSlot(s => s with { Class = value is null || value == WowClasses.Automatic ? null : value.Id });
    }

    public int SlotX { get => SlotRect.X; set => EditSlotRect(SlotRect with { X = value }); }
    public int SlotY { get => SlotRect.Y; set => EditSlotRect(SlotRect with { Y = value }); }
    public int SlotW { get => SlotRect.Width; set => EditSlotRect(SlotRect with { Width = Math.Max(value, SlotEditing.MinSlotSize) }); }
    public int SlotH { get => SlotRect.Height; set => EditSlotRect(SlotRect with { Height = Math.Max(value, SlotEditing.MinSlotSize) }); }

    public bool SlotOnTop
    {
        get => Slot?.Z == ZOrder.Top;
        set => EditSlot(s => s with { Z = value ? ZOrder.Top : (s.Role == SlotRole.Main ? ZOrder.Bottom : ZOrder.Normal) });
    }

    public bool SlotKeepSize
    {
        get => Slot?.Fit == FitMode.None;
        set => EditSlot(s => s with { Fit = value ? FitMode.None : null });
    }

    public string SlotInfo
    {
        get
        {
            if (Slot is not { } s || Current is null) return "";
            var raw = SlotRect;
            var monitor = Core.Monitors.Find(Monitors, s.Monitor) ?? Core.Monitors.Primary(Monitors);
            var frame = LayoutEngine.FrameSize(Current.Options, monitor.Dpi);
            var fitted = LayoutEngine.Fit(raw, s.Fit ?? Current.Options.Fit, s.Anchor, Current.Options.MinAspect, frame);
            var (gw, gh) = LayoutEngine.GameArea(fitted, frame);
            var aspect = gh == 0 ? 0 : (double)gw / gh;
            var text = $"Monitor {monitor.Index} · window {fitted.Width} × {fitted.Height} · game draws {gw} × {gh} ({aspect:0.00})";
            if (fitted != raw) text += $"\nShortened from {raw.Width} × {raw.Height} to keep the aspect rule.";
            return text;
        }
    }

    /// <summary>The selected window's game-area aspect and what it means for Classic's action bar.</summary>
    private double SlotGameAspect
    {
        get
        {
            if (Slot is not { } s || Current is null || Monitors.Count == 0) return 0;
            var monitor = Core.Monitors.Find(Monitors, s.Monitor) ?? Core.Monitors.Primary(Monitors);
            var frame = LayoutEngine.FrameSize(Current.Options, monitor.Dpi);
            var (w, h) = LayoutEngine.GameArea(LayoutEngine.Fit(SlotRect, s.Fit ?? Current.Options.Fit, s.Anchor, Current.Options.MinAspect, frame), frame);
            return h == 0 ? 0 : (double)w / h;
        }
    }

    public string SlotBarText => $"Aspect {SlotGameAspect:0.00} · {ActionBar.Verdict(SlotGameAspect)}";

    /// <summary>In-game alternative: the UI Scale at which the whole bar fits this window (not yet verified in game).</summary>
    public string SlotUiScaleHint => SlotGameAspect >= 4.0 / 3 - 0.001 || SlotGameAspect <= 0
        ? ""
        : $"Or in game: Options → Use UI Scale on, UI Scale {ActionBar.MaxUiScale(SlotGameAspect):0.00} or lower (not tested yet).";

    /// <summary>Layout option: every window that may be shortened gets a 4:3 game area (the whole Classic bar).</summary>
    public bool FullActionBar
    {
        get => Current is not null && Math.Abs(Current.Options.MinAspect - 4.0 / 3) < 0.01;
        set => EditOptions(o => o with { MinAspect = value ? 4.0 / 3 : 1.25 });
    }

    /// <summary>Shortens the selected window just enough for a 4:3 game area (keeps its width and its anchor edge).</summary>
    private void FitActionBar()
    {
        if (Slot is not { } s || Current is null) return;
        var monitor = Core.Monitors.Find(Monitors, s.Monitor) ?? Core.Monitors.Primary(Monitors);
        var frame = LayoutEngine.FrameSize(Current.Options, monitor.Dpi);
        var raw = SlotRect;
        var fitted = LayoutEngine.Fit(raw, FitMode.ShrinkAnchor, s.Anchor, 4.0 / 3, frame);
        if (fitted == raw)
        {
            Status.Info($"Window {s.N} already shows the whole action bar.");
            return;
        }
        OnSlotEdited(SelectedSlotIndex, SlotEditing.WithRect(s, fitted, Monitors, Current.Options) with { Fit = FitMode.None });
        Status.Ok($"Window {s.N} is now {fitted.Width} × {fitted.Height}: the whole action bar fits.");
    }
    public Brush SlotBarBrush => ActionBar.Brush(SlotGameAspect);

    private void RaiseSlotProperties()
    {
        foreach (var name in new[] { nameof(SlotBarText), nameof(SlotBarBrush), nameof(SlotUiScaleHint), nameof(FullActionBar), nameof(HasSlot), nameof(SlotHeader), nameof(SlotBrush), nameof(SlotName), nameof(SlotClass), nameof(SlotX), nameof(SlotY), nameof(SlotW), nameof(SlotH), nameof(SlotOnTop), nameof(SlotKeepSize), nameof(SlotInfo) })
            OnPropertyChanged(name);
    }

    // =====================================================================================
    // Commands
    // =====================================================================================

    public ICommand SetTabCommand { get; }
    public ICommand MoreClientsCommand { get; }
    public ICommand FewerClientsCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CopyCodeCommand { get; }
    public ICommand ImportCodeCommand { get; }
    public ICommand ImportFileCommand { get; }
    public ICommand ExportFileCommand { get; }
    public ICommand CaptureCommand { get; }
    public ICommand RefreshWindowsCommand { get; }
    public ICommand IdentifyCommand { get; }
    public ICommand MoveWindowUpCommand { get; }
    public ICommand MoveWindowDownCommand { get; }
    public ICommand ArrangeCommand { get; }
    public ICommand StartSquadCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestoreFramesCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand OpenLayoutsFolderCommand { get; }
    public ICommand DiagnoseCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public ICommand BrowseBattleNetCommand { get; }
    public ICommand ShowFirstRunCommand { get; }
    public ICommand StartFromSetupCommand { get; }
    public ICommand CloseMineCommand { get; }
    public ICommand RefreshHintsCommand { get; }
    public ICommand BrowseGameCommand { get; }
    public ICommand FitActionBarCommand { get; }

    // =====================================================================================
    // Saved layouts
    // =====================================================================================

    public void RefreshMonitors()
    {
        var monitors = MonitorReader.GetMonitors();
        if (monitors.SequenceEqual(Monitors)) return;
        Monitors = monitors;
        OnPropertyChanged(nameof(MonitorSummary));
        RefreshCards();
        RaiseSlotProperties();
    }

    private void LoadProfiles()
    {
        var errors = new List<string>();
        var stored = _store.LoadAll(errors);
        foreach (var e in errors) Status.Warn($"Skipped a broken layout file: {e}");
        if (stored.Count == 0 && Monitors.Count > 0)
        {
            // First start: a ready-to-use layout for 3 clients.
            var profile = Presets.Create(PresetKind.Side, 3, Monitors, options: new LayoutOptions { MinAspect = 4.0 / 3 }) with { Name = "My squad" };
            stored.Add(new StoredProfile(_store.Save(profile), profile));
        }
        Profiles.Clear();
        foreach (var s in stored) Profiles.Add(new ProfileItem(s));
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name == _settings.LastProfile) ?? Profiles.FirstOrDefault();
    }

    /// <summary>Selects a layout by name (used by "--start NAME" from a desktop shortcut).</summary>
    public bool SelectProfile(string name)
    {
        var item = Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (item is null) return false;
        SelectedProfile = item;
        return true;
    }

    private void AddAndSelect(LayoutProfile profile)
    {
        profile = profile with { Name = _store.UniqueName(profile.Name) };
        var item = new ProfileItem(new StoredProfile(_store.Save(profile), profile));
        Profiles.Add(item);
        SelectedProfile = item;
    }

    private void SaveAs()
    {
        if (Current is null) return;
        var name = _dialogs.Prompt("Save as a new layout", "Name for the new layout:", Current.Name + " copy");
        if (string.IsNullOrWhiteSpace(name)) return;
        AddAndSelect(Current with { Name = name.Trim() });
        Status.Ok($"Saved \"{SelectedProfile!.Name}\".");
    }

    private void Rename()
    {
        if (_selectedProfile is null || Current is null) return;
        var name = _dialogs.Prompt("Rename layout", "New name:", Current.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == Current.Name) return;
        Update(Current with { Name = _store.UniqueName(name, _selectedProfile.Path) }, pushUndo: false);
    }

    private void Delete()
    {
        if (_selectedProfile is null || Profiles.Count <= 1) return;
        if (!_dialogs.Confirm("Delete layout", $"Delete \"{_selectedProfile.Name}\"? This cannot be undone.")) return;
        _store.Delete(_selectedProfile.Path);
        var index = Profiles.IndexOf(_selectedProfile);
        Profiles.Remove(_selectedProfile);
        _selectedProfile = null;
        SelectedProfile = Profiles[Math.Min(index, Profiles.Count - 1)];
    }

    /// <summary>Every edit goes through here: undo, auto-save, refresh, live apply.</summary>
    private void Update(LayoutProfile profile, bool pushUndo = true)
    {
        if (_selectedProfile is null || Current is null || profile == Current) return;
        if (pushUndo) _undo.Push(Current);
        try
        {
            _selectedProfile.Path = _store.Save(profile, _selectedProfile.Path);
        }
        catch (IOException e)
        {
            Status.Warn($"Could not save the layout: {e.Message}");
        }
        _selectedProfile.Profile = profile;
        _selectedProfile.Refresh();
        Current = profile;
        _settings.LastProfile = profile.Name;
        _settings.Save();
        ScheduleLive();
    }

    public void OnSlotEdited(int index, LayoutSlot slot)
    {
        if (Current is null || index < 0 || index >= Current.Slots.Count) return;
        var slots = Current.Slots.ToList();
        slots[index] = slot;
        Update(Current with { Slots = slots });
        RaiseSlotProperties();
    }

    private void EditSlot(Func<LayoutSlot, LayoutSlot> change)
    {
        if (Slot is { } s) OnSlotEdited(SelectedSlotIndex, change(s));
    }

    private void EditSlotRect(PixelRect rect)
    {
        if (Slot is { } s && Current is not null) OnSlotEdited(SelectedSlotIndex, SlotEditing.WithRect(s, rect, Monitors, Current.Options));
    }

    private void EditOptions(Func<LayoutOptions, LayoutOptions> change)
    {
        if (Current is not null) Update(Current with { Options = change(Current.Options) });
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        Update(_undo.Pop(), pushUndo: false);
        RaiseSlotProperties();
    }

    private void CopyCode()
    {
        if (Current is null) return;
        _dialogs.CopyToClipboard(ProfileJson.ToShareCode(Current));
        Status.Ok("Layout code copied (WBX1:…). Paste it to a friend or into the website.");
    }

    private void ImportCode()
    {
        var code = _dialogs.Prompt("Paste a layout code", "Paste a WBX1: code from a friend or the website:", "");
        if (string.IsNullOrWhiteSpace(code)) return;
        try
        {
            AddAndSelect(ProfileJson.FromShareCode(code));
            Status.Ok($"Imported \"{SelectedProfile!.Name}\".");
        }
        catch (FormatException e)
        {
            Status.Warn(e.Message);
        }
    }

    private void ImportFile()
    {
        var path = _dialogs.OpenFile("Layout files (*.json)|*.json");
        if (path is null) return;
        try
        {
            AddAndSelect(ProfileJson.Deserialize(File.ReadAllText(path)));
            Status.Ok($"Imported \"{SelectedProfile!.Name}\" from {Path.GetFileName(path)}.");
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            Status.Warn(e.Message);
        }
    }

    private void ExportFile()
    {
        if (Current is null) return;
        var path = _dialogs.SaveFile("Layout files (*.json)|*.json", ProfileStore.FileNameFor(Current.Name));
        if (path is null) return;
        File.WriteAllText(path, ProfileJson.Serialize(Current));
        Status.Ok($"Saved {Path.GetFileName(path)}.");
    }

    /// <summary>Makes a new layout from where the WoW windows are now (arranged by hand, Windows Snap …).</summary>
    private void Capture()
    {
        var open = WindowFinder.FindGameWindows();
        if (open.Count == 0)
        {
            Status.Warn("No World of Warcraft windows found to capture. Arrange your game windows first.");
            return;
        }
        var borderless = open.All(w => (w.Style & 0x00C00000) == 0);
        var profile = ProfileCapture.FromWindows(open.Select(w => WindowFinder.VisibleRectOf(w.Handle)).ToList(), Monitors, borderless, "Captured layout");
        AddAndSelect(profile);
        Status.Ok($"Captured {open.Count} windows as \"{SelectedProfile!.Name}\".");
    }

    // =====================================================================================
    // Settings
    // =====================================================================================

    public bool Forever
    {
        get => _settings.Forever;
        set => SetSetting(() => _settings.Forever = value);
    }

    public bool RenameWindows
    {
        get => _settings.RenameWindows;
        set => SetSetting(() => _settings.RenameWindows = value);
    }

    public bool LiveApply
    {
        get => _settings.LiveApply;
        set => SetSetting(() => _settings.LiveApply = value);
    }

    public bool Animations
    {
        get => _settings.Animations;
        set => SetSetting(() => _settings.Animations = value);
    }

    public bool CheckForUpdates
    {
        get => _settings.CheckForUpdates;
        set => SetSetting(() => _settings.CheckForUpdates = value);
    }

    public string BattleNetExe
    {
        get => _settings.BattleNetExe ?? BattleNet.FindExe() ?? "";
        set => SetSetting(() => _settings.BattleNetExe = string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    public string GameExe
    {
        get => _settings.GameExe ?? GameInstall.FindExe(null) ?? "";
        set
        {
            SetSetting(() => _settings.GameExe = string.IsNullOrWhiteSpace(value) ? null : value.Trim());
            ReadWowAccounts();
        }
    }

    public bool LaunchLoginScreens
    {
        get => _settings.LaunchLoginScreens;
        set
        {
            SetSetting(() => _settings.LaunchLoginScreens = value);
            OnPropertyChanged(nameof(LaunchViaBattleNet));
        }
    }

    public bool LaunchViaBattleNet
    {
        get => !LaunchLoginScreens;
        set => LaunchLoginScreens = !value;
    }

    private void BrowseGame()
    {
        var path = _dialogs.OpenFile("WoW client|Wow*.exe|Programs (*.exe)|*.exe");
        if (path is not null) GameExe = path;
    }

    private void SetSetting(Action change, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        change();
        _settings.Save();
        OnPropertyChanged(name);
    }

    private void BrowseBattleNet()
    {
        var path = _dialogs.OpenFile("Battle.net|Battle.net.exe|Programs (*.exe)|*.exe");
        if (path is not null) BattleNetExe = path;
    }

    // =====================================================================================
    // Squad
    // =====================================================================================

    public string SquadHint => Windows.Count == 0
        ? "No WoW windows are open yet. Press Start squad, then press Play in Battle.net once per account – each window jumps into its place."
        : $"{Windows.Count} WoW window(s) open. Arrange now puts them in this order; use ▲▼ or Identify to check who is who.";

    /// <summary>
    /// Rows mirror the layout's slots (name, class) plus the local WoW-account choice. Rows are reused when the slot
    /// count is unchanged, so editing one field never rebuilds the list under the cursor.
    /// </summary>
    private void RefreshParty()
    {
        if (Current is null) return;
        if (Party.Count != Current.Slots.Count)
        {
            var accounts = WowAccounts.Select(a => a.Name).Prepend("").ToList();
            var lastPlayed = WowAccounts.ToDictionary(a => a.Name, a => a.LastPlayed, StringComparer.OrdinalIgnoreCase);
            var states = Party.ToDictionary(p => p.Number, p => p.State);
            Party.Clear();
            for (var i = 0; i < Current.Slots.Count; i++)
            {
                var index = i;
                Party.Add(new PartyMember
                {
                    Number = i + 1,
                    Role = Current.Slots[i].Role == SlotRole.Main ? "Main" : "Follower",
                    Accounts = accounts,
                    LastPlayed = lastPlayed,
                    Identify = new RelayCommand(() => IdentifySlot(index)),
                    NameChanged = (k, name) => EditSlotAt(k, s => s with { Name = name.Length == 0 ? null : name }),
                    ClassChanged = (k, c) => EditSlotAt(k, s => s with { Class = c == WowClasses.Automatic ? null : c.Id }),
                    AccountChanged = SaveAccount,
                    State = states.GetValueOrDefault(i + 1),
                });
            }
        }
        for (var i = 0; i < Party.Count; i++)
        {
            var slot = Current.Slots[i];
            Party[i].Load(slot.Name ?? "", WowClasses.Find(slot.Class) ?? WowClasses.Automatic, i < _settings.Accounts.Count ? _settings.Accounts[i] : "");
        }
        UpdatePartyWindows();
    }

    /// <summary>WoW accounts and characters from the game's WTF folder (read-only hints; refreshed on demand).</summary>
    public IReadOnlyList<WtfAccount> WowAccounts { get; private set; } = [];

    private void ReadWowAccounts()
    {
        try
        {
            WowAccounts = GameInstall.Accounts(GameInstall.FindExe(_settings.GameExe));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            WowAccounts = [];
        }
        Party.Clear();   // rebuild rows with the new lists
        RefreshParty();
    }

    private void EditSlotAt(int index, Func<LayoutSlot, LayoutSlot> change)
    {
        if (Current is null || index < 0 || index >= Current.Slots.Count) return;
        OnSlotEdited(index, change(Current.Slots[index]));
    }

    /// <summary>
    /// Squad drag and drop: the character in row <paramref name="from"/> (name, class, WoW account and its open window)
    /// moves to slot position <paramref name="to"/>; the others shift. Slot rectangles stay where they are, so the
    /// canvas, the rows and – with live on – the real windows all follow the same order.
    /// </summary>
    public void MoveCharacter(int from, int to)
    {
        if (Current is null || from == to || from < 0 || to < 0 || from >= Current.Slots.Count || to >= Current.Slots.Count) return;
        var people = Current.Slots.Select(s => (s.Name, s.Class)).ToList();
        var person = people[from];
        people.RemoveAt(from);
        people.Insert(to, person);
        var slots = Current.Slots.Select((s, i) => s with { Name = people[i].Name, Class = people[i].Class }).ToList();

        while (_settings.Accounts.Count < Current.Slots.Count) _settings.Accounts.Add("");
        var account = _settings.Accounts[from];
        _settings.Accounts.RemoveAt(from);
        _settings.Accounts.Insert(to, account);
        _settings.Save();

        if (from < Windows.Count && to < Windows.Count)
        {
            Windows.Move(from, to);
            for (var k = 0; k < Windows.Count; k++) Windows[k].Slot = k + 1;
        }
        Update(Current with { Slots = slots });
        // The window order changed even when names did not (unnamed rows): always refresh and move the windows.
        RefreshParty();
        ScheduleLive();
        Status.Info($"Moved {person.Name ?? $"row {from + 1}"} to slot {to + 1}.");
    }

    /// <summary>
    /// Canvas drag and drop: dropping window <paramref name="a"/> onto window <paramref name="b"/> swaps their places on
    /// screen. Each keeps its number, character and class; with live on the real windows swap too.
    /// </summary>
    public void SwapSlots(int a, int b)
    {
        if (Current is null || a == b || a < 0 || b < 0 || a >= Current.Slots.Count || b >= Current.Slots.Count) return;
        LayoutSlot sa = Current.Slots[a], sb = Current.Slots[b];
        var slots = Current.Slots.ToList();
        slots[a] = sa with { Monitor = sb.Monitor, X = sb.X, Y = sb.Y, W = sb.W, H = sb.H, Z = sb.Z, Anchor = sb.Anchor, Fit = sb.Fit };
        slots[b] = sb with { Monitor = sa.Monitor, X = sa.X, Y = sa.Y, W = sa.W, H = sa.H, Z = sa.Z, Anchor = sa.Anchor, Fit = sa.Fit };
        Update(Current with { Slots = slots });
        SelectedSlotIndex = a;
        Status.Info($"Swapped the places of window {a + 1} and window {b + 1}. Ctrl+Z undoes it.");
    }

    /// <summary>Row texts, statuses and the start checklist from the open windows.</summary>
    private void UpdatePartyWindows()
    {
        if (Current is null || Monitors.Count == 0) return;
        var placed = LayoutEngine.Resolve(Current, Monitors);
        for (var i = 0; i < Party.Count; i++)
        {
            var w = i < Windows.Count ? Windows[i] : null;
            Party[i].Window = w is null ? "No window yet" : $"{w.Process} · {w.Detail}";
            if (Party[i].State == PartyState.Problem && w is null) continue;
            Party[i].State = w is null ? PartyState.Waiting
                : i < placed.Count && WindowPlacer.IsAt(w.Window.Handle, placed[i].Rect) ? PartyState.Placed
                : PartyState.Elsewhere;
        }
        var steps = new List<(string Label, bool Done)> { ("Battle.net is running", BattleNet.IsRunning()) };
        for (var i = 0; i < Party.Count; i++)
        {
            var account = Party[i].Account is { Length: > 0 } a ? a : $"account {i + 1}";
            steps.Add(($"Log in with {account} – {Party[i].Title}", Party[i].State == PartyState.Placed));
        }
        if (Checklist.Count != steps.Count || Checklist.Select(c => c.Label).Where((l, i) => l != steps[i].Label).Any())
        {
            Checklist.Clear();
            foreach (var (label, _) in steps) Checklist.Add(new CheckItem(label));
        }
        for (var i = 0; i < steps.Count; i++) Checklist[i].Done = steps[i].Done;
        OnPropertyChanged(nameof(HeaderStatus));
    }

    /// <summary>Short live status for the title bar.</summary>
    public string HeaderStatus => IsWatching ? "Watching for game windows …" : Windows.Count switch
    {
        0 => "No WoW windows open",
        1 => "1 WoW window open",
        var n => $"{n} WoW windows open",
    };

    private void IdentifySlot(int index)
    {
        RefreshWindows();
        if (index >= Windows.Count)
        {
            Status.Warn($"Slot {index + 1} has no WoW window yet.");
            return;
        }
        var w = Windows[index];
        _dialogs.ShowIdentify([(WindowFinder.VisibleRectOf(w.Window.Handle), w.Slot.ToString(), Party.ElementAtOrDefault(index)?.Title ?? w.Process)]);
    }

    /// <summary>Called every couple of seconds while the Squad tab is open, so statuses stay live.</summary>
    public void Tick()
    {
        if (!Busy) RefreshWindows();
    }

    private void SaveAccount(int index, string account)
    {
        while (_settings.Accounts.Count <= index) _settings.Accounts.Add("");
        _settings.Accounts[index] = account.Trim();
        _settings.Save();
        UpdatePartyWindows();
    }

    private void SetPartyState(int slotIndex, PartyState state)
    {
        if (slotIndex >= 0 && slotIndex < Party.Count) Party[slotIndex].State = state;
    }

    public void RefreshWindows()
    {
        var found = WindowFinder.FindGameWindows();
        // keep the user's order for windows we already know; new ones go to the end (launch order)
        var known = Windows.Select(w => w.Window.Handle).ToList();
        var ordered = known.Count == 0 ? MatchToSlots(found) : found.OrderBy(w => known.IndexOf(w.Handle) is var i && i >= 0 ? i : int.MaxValue).ToList();
        Windows.Clear();
        for (var i = 0; i < ordered.Count; i++) Windows.Add(new GameWindowItem(ordered[i]) { Slot = i + 1 });
        CommandManager.InvalidateRequerySuggested();
        OnPropertyChanged(nameof(SquadHint));
        UpdatePartyWindows();
    }

    /// <summary>
    /// First look at the open windows: a window that already sits exactly in a slot gets that slot, the rest fill the
    /// free slots in launch order. So rows, canvas and screen agree even when the windows were arranged earlier.
    /// </summary>
    private List<WindowInfo> MatchToSlots(List<WindowInfo> found)
    {
        if (Current is null || Monitors.Count == 0) return found;
        var placed = LayoutEngine.Resolve(Current, Monitors);
        var bySlot = new WindowInfo?[Math.Max(found.Count, placed.Count)];
        var rest = new List<WindowInfo>();
        foreach (var w in found)
        {
            var k = Enumerable.Range(0, placed.Count).FirstOrDefault(i => bySlot[i] is null && WindowPlacer.IsAt(w.Handle, placed[i].Rect), -1);
            if (k >= 0) bySlot[k] = w;
            else rest.Add(w);
        }
        var queue = new Queue<WindowInfo>(rest);
        for (var i = 0; i < bySlot.Length && queue.Count > 0; i++) bySlot[i] ??= queue.Dequeue();
        return bySlot.Where(w => w is not null).Select(w => w!).ToList();
    }

    private void MoveWindow(GameWindowItem? item, int delta)
    {
        if (item is null) return;
        var i = Windows.IndexOf(item);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Windows.Count) return;
        Windows.Move(i, j);
        for (var k = 0; k < Windows.Count; k++) Windows[k].Slot = k + 1;
        UpdatePartyWindows();
        ScheduleLive();
    }

    private void Identify()
    {
        RefreshWindows();
        if (Windows.Count == 0)
        {
            Status.Warn("No World of Warcraft windows found.");
            return;
        }
        var labels = Windows.Select(w =>
        {
            var name = Current is not null && w.Slot - 1 < Current.Slots.Count ? Current.Slots[w.Slot - 1].Name : null;
            return (WindowFinder.VisibleRectOf(w.Window.Handle), w.Slot.ToString(), name ?? w.Process);
        }).ToList();
        _dialogs.ShowIdentify(labels);
        Status.Info("Showing the slot number on each WoW window. Reorder the list to change who goes where.");
    }

    private SquadPlan? Plan()
    {
        if (Current is null) return null;
        RefreshMonitors();
        return new SquadPlan(Current, Monitors, RenameWindows && Current.Slots.Any(s => s.Name is { Length: > 0 }));
    }

    private List<WindowInfo> OrderedWindows()
    {
        if (Windows.Count == 0 || Windows.Any(w => !WindowFinder.Exists(w.Window.Handle))) RefreshWindows();
        return Windows.Select(w => w.Window).ToList();
    }

    private async Task ArrangeAsync()
    {
        if (Plan() is not { } plan) return;
        var windows = OrderedWindows();
        Busy = true;
        try
        {
            Status.Title($"Arrange now: \"{plan.Profile.Name}\"");
            await Task.Run(() => Arranger.ArrangeAsync(plan, windows, Status));
        }
        finally
        {
            Busy = false;
            RefreshWindows();
        }
    }

    /// <summary>"Live": after an edit, move the open WoW windows to the new layout (debounced, quiet).</summary>
    private void ScheduleLive()
    {
        if (!_loaded || !LiveApply || IsWatching || Busy) return;
        _live?.Cancel();
        _live = new CancellationTokenSource();
        _ = LiveAsync(_live.Token);
    }

    private async Task LiveAsync(CancellationToken cancel)
    {
        try
        {
            await Task.Delay(350, cancel);
            if (WindowFinder.FindGameWindows().Count == 0 || Plan() is not { } plan) return;
            var windows = OrderedWindows();
            await _liveLock.WaitAsync(cancel);
            try
            {
                var quiet = new QuietSink(Status);
                var ok = await Task.Run(() => Arranger.ArrangeAsync(plan, windows, quiet, cancel), cancel);
                if (ok) Status.Ok($"Moved {Math.Min(windows.Count, plan.Slots.Count)} WoW window(s) to \"{plan.Profile.Name}\".");
                RefreshWindows();
            }
            finally
            {
                _liveLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // a newer edit replaced this one
        }
        catch (Exception e)
        {
            Status.Report(StatusLevel.Error, $"Moving the windows failed: {e.Message}");
        }
    }

    /// <summary>Only warnings and errors get through (live moves would otherwise flood the activity list).</summary>
    private sealed class QuietSink(IStatusSink inner) : IStatusSink
    {
        public void Report(StatusLevel level, string message)
        {
            if (level is StatusLevel.Warn or StatusLevel.Error or StatusLevel.Detail) inner.Report(level, message);
        }
    }

    public async Task StartSquadAsync()
    {
        if (Plan() is not { } plan || IsWatching) return;
        _live?.Cancel();
        Busy = true;
        _watch = new CancellationTokenSource();
        OnPropertyChanged(nameof(IsWatching));
        OnPropertyChanged(nameof(HeaderStatus));
        foreach (var p in Party) p.State = PartyState.Waiting;
        try
        {
            Status.Title($"Start squad: \"{plan.Profile.Name}\"");
            if (LaunchLoginScreens)
            {
                var missing = Math.Max(0, plan.Slots.Count - WindowFinder.FindGameWindows().Count);
                if (missing == 0) Status.Info("All clients are already open.");
                else if (GameInstall.FindExe(_settings.GameExe) is { } exe) _launcher.Start(exe, missing, Status);
                else Status.Warn("WoW was not found. Set the game exe in Settings.");
                Status.Title("Log in to each login screen yourself – in this order:");
            }
            else
            {
                SquadWatcher.EnsureBattleNet(Status, _settings.BattleNetExe, plan.ToolIsAdmin);
                Status.Title(SquadWatcher.BattleNetInstructions(Forever));
                Status.Title("Then pick the account and press Play – in this order:");
            }
            for (var i = 0; i < plan.Slots.Count; i++)
            {
                var account = i < _settings.Accounts.Count && _settings.Accounts[i].Length > 0 ? $"   ({_settings.Accounts[i]})" : "";
                Status.Title($"   {i + 1}. {Party.ElementAtOrDefault(i)?.Title ?? plan.Slots[i].Slot.Label}{account}");
            }
            Status.Info("Each game window snaps into place as soon as it appears.");
            var watcher = new SquadWatcher(plan, Status);
            var dispatcher = Application.Current.Dispatcher;
            watcher.AssignmentChanged += (_, slot) => dispatcher.BeginInvoke(() =>
            {
                RefreshWindows();
                if (slot >= 0) SetPartyState(slot, PartyState.Placed);
            });
            var token = _watch.Token;
            var ok = await Task.Run(() => watcher.RunAsync(TimeSpan.FromMinutes(15), token));
            RefreshWindows();
            if (!ok) foreach (var p in Party.Where(p => p.State != PartyState.Placed)) p.State = PartyState.Problem;
        }
        finally
        {
            _watch.Dispose();
            _watch = null;
            Busy = false;
            OnPropertyChanged(nameof(IsWatching));
            OnPropertyChanged(nameof(HeaderStatus));
        }
    }

    private void RestoreFrames()
    {
        var open = WindowFinder.FindGameWindows();
        foreach (var w in open) WindowPlacer.RestoreFrame(w.Handle);
        Status.Info($"Gave {open.Count} WoW window(s) their title bars back.");
    }

    /// <summary>Writes the Diagnose report (the same as "wbx diagnose") to the logs folder.</summary>
    private async Task DiagnoseAsync()
    {
        if (Plan() is not { } plan) return;
        RefreshWindows();
        var path = Path.Combine(StatusLog.LogFolder, $"diagnose-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var report = new ReportSink();
        Busy = true;
        try
        {
            Status.Title("Diagnose: writing a report and trying one placement per window …");
            var windows = Windows.Select(w => w.Window).ToList();
            await Task.Run(() => Diagnostics.RunAsync(plan, windows, report, $"WoWBoxer Window Layouts {Version}", "per-monitor aware"));
            Directory.CreateDirectory(StatusLog.LogFolder);
            File.WriteAllText(path, report.Text.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase));
            Status.Ok($"Diagnose report saved: {path}");
            Open(StatusLog.LogFolder);
        }
        finally
        {
            Busy = false;
            RefreshWindows();
        }
    }

    private sealed class ReportSink : IStatusSink
    {
        private readonly System.Text.StringBuilder _text = new();

        public string Text
        {
            get
            {
                lock (_text) return _text.ToString();
            }
        }

        public void Report(StatusLevel level, string message)
        {
            lock (_text) _text.AppendLine((level == StatusLevel.Title ? Environment.NewLine : "") + message);
        }
    }

    private static void Open(string folder)
    {
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true })?.Dispose();
    }

    private static void OpenUrl(string? url)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
    }
}
