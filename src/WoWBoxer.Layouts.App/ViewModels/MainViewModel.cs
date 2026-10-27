using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
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

public sealed class ProfileItem(StoredProfile stored) : ObservableObject
{
    public string Path { get; set; } = stored.Path;
    public LayoutProfile Profile { get; set; } = stored.Profile;
    public string Name => Profile.Name;
    public string Summary => $"{Profile.Clients} clients";
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Summary));
    }
}

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

public sealed class MainViewModel : ObservableObject
{
    private readonly ProfileStore _store = new(AppSettings.ProfilesFolder);
    private readonly AppSettings _settings;
    private readonly IUserDialogs _dialogs;
    private readonly Stack<LayoutProfile> _undo = new();
    private ProfileItem? _selectedProfile;
    private LayoutProfile? _current;
    private int _selectedSlotIndex = -1;
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private PresetKind _presetKind = PresetKind.Side;
    private int _presetClients = 3;
    private CancellationTokenSource? _watch;
    private bool _busy;

    public MainViewModel(AppSettings settings, IUserDialogs dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;

        NewProfileCommand = new RelayCommand(NewProfile);
        DuplicateCommand = new RelayCommand(Duplicate, () => _selectedProfile is not null);
        RenameCommand = new RelayCommand(Rename, () => _selectedProfile is not null);
        DeleteCommand = new RelayCommand(Delete, () => _selectedProfile is not null);
        ApplyPresetCommand = new RelayCommand(ApplyPreset, () => _current is not null);
        UndoCommand = new RelayCommand(Undo, () => _undo.Count > 0);
        CopyCodeCommand = new RelayCommand(CopyCode, () => _current is not null);
        ImportCodeCommand = new RelayCommand(ImportCode);
        ImportFileCommand = new RelayCommand(ImportFile);
        ExportFileCommand = new RelayCommand(ExportFile, () => _current is not null);
        CaptureCommand = new RelayCommand(Capture);
        RefreshWindowsCommand = new RelayCommand(RefreshWindows);
        IdentifyCommand = new RelayCommand(Identify);
        MoveWindowUpCommand = new RelayCommand(p => MoveWindow(p as GameWindowItem, -1));
        MoveWindowDownCommand = new RelayCommand(p => MoveWindow(p as GameWindowItem, 1));
        ArrangeCommand = new RelayCommand(() => _ = ArrangeAsync(), () => !_busy && _current is not null);
        StartSquadCommand = new RelayCommand(() => _ = StartSquadAsync(), () => !_busy && _current is not null);
        StopCommand = new RelayCommand(() => _watch?.Cancel(), () => IsWatching);
        RestoreFramesCommand = new RelayCommand(RestoreFrames);
        OpenLogsCommand = new RelayCommand(() => OpenFolder(StatusLog.LogFolder));
        DiagnoseCommand = new RelayCommand(() => _ = DiagnoseAsync(), () => !_busy);

        RefreshMonitors();
        LoadProfiles();
        RefreshWindows();
    }

    // ---------- state ----------

    public StatusLog Status { get; } = new();
    public ObservableCollection<ProfileItem> Profiles { get; } = [];
    public ObservableCollection<GameWindowItem> Windows { get; } = [];
    public IReadOnlyList<PresetKind> PresetKinds { get; } = Enum.GetValues<PresetKind>();
    public IReadOnlyList<int> ClientCounts { get; } = Enumerable.Range(Presets.MinClients, Presets.MaxClients - Presets.MinClients + 1).ToList();
    public IReadOnlyList<AspectChoice> AspectChoices { get; } =
    [
        new(1.25, "5:4 – smallest WoW shows without black bars"),
        new(4.0 / 3, "4:3 – full Classic action bar visible"),
        new(0, "Off – use the slot as drawn"),
    ];

    public IReadOnlyList<MonitorInfo> Monitors { get => _monitors; private set => Set(ref _monitors, value); }

    public string MonitorSummary => string.Join("   ", Monitors.Select(m => $"{m.Index}: {m.Bounds.Width}×{m.Bounds.Height} @ {m.ScalePercent} %"));

    public ProfileItem? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!Set(ref _selectedProfile, value)) return;
            _undo.Clear();
            Current = value?.Profile;
            SelectedSlotIndex = Current is { Slots.Count: > 0 } ? 0 : -1;
            if (value is not null)
            {
                _settings.LastProfile = value.Name;
                _settings.Save();
                PresetClients = value.Profile.Clients is >= Presets.MinClients and <= Presets.MaxClients ? value.Profile.Clients : PresetClients;
            }
        }
    }

    public LayoutProfile? Current
    {
        get => _current;
        private set
        {
            if (!Set(ref _current, value)) return;
            OnPropertyChanged(nameof(Borderless));
            OnPropertyChanged(nameof(UseTaskbarArea));
            OnPropertyChanged(nameof(SelectedAspect));
            OnPropertyChanged(nameof(ProfileTitle));
            RaiseSlotProperties();
        }
    }

    public string ProfileTitle => Current is null ? "No layout" : $"{Current.Name} · {Current.Clients} clients";

    public int SelectedSlotIndex
    {
        get => _selectedSlotIndex;
        set
        {
            if (Set(ref _selectedSlotIndex, value)) RaiseSlotProperties();
        }
    }

    public PresetKind PresetKind { get => _presetKind; set => Set(ref _presetKind, value); }
    public int PresetClients { get => _presetClients; set => Set(ref _presetClients, value); }

    public bool Forever
    {
        get => _settings.Forever;
        set
        {
            _settings.Forever = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool RenameWindows
    {
        get => _settings.RenameWindows;
        set
        {
            _settings.RenameWindows = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool IsWatching => _watch is not null;

    public bool Busy
    {
        get => _busy;
        private set
        {
            if (Set(ref _busy, value)) CommandManager.InvalidateRequerySuggested();
        }
    }

    // ---------- layout options ----------

    public bool Borderless
    {
        get => Current?.Options.Borderless ?? false;
        set => EditOptions(o => o with { Borderless = value });
    }

    public bool UseTaskbarArea
    {
        get => Current?.Options.UseTaskbarArea ?? false;
        set => EditOptions(o => o with { UseTaskbarArea = value });
    }

    public AspectChoice? SelectedAspect
    {
        get => Current is null ? null : AspectChoices.MinBy(c => Math.Abs(c.Value - Current.Options.MinAspect));
        set
        {
            if (value is not null) EditOptions(o => o with { MinAspect = value.Value });
        }
    }

    // ---------- selected slot ----------

    private LayoutSlot? Slot => Current is not null && SelectedSlotIndex >= 0 && SelectedSlotIndex < Current.Slots.Count ? Current.Slots[SelectedSlotIndex] : null;

    private PixelRect SlotRect => Slot is { } s && Monitors.Count > 0
        ? SlotEditing.Raw(s, Core.Monitors.Find(Monitors, s.Monitor) ?? Core.Monitors.Primary(Monitors), Current!.Options)
        : default;

    public bool HasSlot => Slot is not null;
    public string SlotHeader => Slot is { } s ? $"Slot {s.N}{(s.Role == SlotRole.Main ? " · main" : "")}" : "No slot selected";

    public string SlotName
    {
        get => Slot?.Name ?? "";
        set => EditSlot(s => s with { Name = string.IsNullOrWhiteSpace(value) ? null : value.Trim() });
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
            var fitted = LayoutEngine.Fit(raw, s.Fit ?? Current.Options.Fit, s.Anchor, Current.Options.MinAspect);
            var aspect = fitted.Height == 0 ? 0 : (double)fitted.Width / fitted.Height;
            var text = $"Monitor {s.Monitor} · window {fitted.Width}×{fitted.Height} · aspect {aspect:0.00}";
            if (fitted != raw) text += $"\nShrunk from {raw.Width}×{raw.Height} to keep {Current.Options.MinAspect:0.00} (anchored {s.Anchor.ToString().ToLowerInvariant()}).";
            if (aspect < 4.0 / 3 - 0.001) text += "\nNarrower than 4:3 – the ends of the Classic action bar are clipped (all buttons stay visible down to about 1.2).";
            return text;
        }
    }

    private void RaiseSlotProperties()
    {
        foreach (var name in new[] { nameof(HasSlot), nameof(SlotHeader), nameof(SlotName), nameof(SlotX), nameof(SlotY), nameof(SlotW), nameof(SlotH), nameof(SlotOnTop), nameof(SlotKeepSize), nameof(SlotInfo) })
            OnPropertyChanged(name);
    }

    // ---------- commands ----------

    public ICommand NewProfileCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand UndoCommand { get; }
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
    public ICommand DiagnoseCommand { get; }

    // ---------- profiles ----------

    public void RefreshMonitors()
    {
        Monitors = MonitorReader.GetMonitors();
        OnPropertyChanged(nameof(MonitorSummary));
        RaiseSlotProperties();
    }

    private void LoadProfiles()
    {
        var errors = new List<string>();
        var stored = _store.LoadAll(errors);
        foreach (var e in errors) Status.Warn($"Skipped a broken layout file: {e}");
        if (stored.Count == 0 && Monitors.Count > 0)
        {
            // First start: one ready-to-use layout for 3 clients.
            var profile = Presets.Create(PresetKind.Side, 3, Monitors) with { Name = "Trio" };
            stored.Add(new StoredProfile(_store.Save(profile), profile));
        }
        Profiles.Clear();
        foreach (var s in stored) Profiles.Add(new ProfileItem(s));
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name == _settings.LastProfile) ?? Profiles.FirstOrDefault();
    }

    /// <summary>Selects a profile by name (used by "--start NAME" from a desktop shortcut).</summary>
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

    private void NewProfile()
    {
        if (!TryPreset(PresetKind, PresetClients, out var profile)) return;
        AddAndSelect(profile);
        Status.Ok($"New layout \"{SelectedProfile!.Name}\" from preset {Presets.DisplayName(PresetKind)}.");
    }

    private void Duplicate()
    {
        if (Current is null) return;
        AddAndSelect(Current with { Name = Current.Name + " copy" });
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
        if (_selectedProfile is null) return;
        if (!_dialogs.Confirm("Delete layout", $"Delete \"{_selectedProfile.Name}\"?")) return;
        _store.Delete(_selectedProfile.Path);
        var index = Profiles.IndexOf(_selectedProfile);
        Profiles.Remove(_selectedProfile);
        SelectedProfile = Profiles.Count == 0 ? null : Profiles[Math.Min(index, Profiles.Count - 1)];
    }

    /// <summary>Every edit goes through here: undo, auto-save, refresh.</summary>
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
        var previous = _undo.Pop();
        Update(previous, pushUndo: false);
        RaiseSlotProperties();
    }

    private bool TryPreset(PresetKind kind, int clients, out LayoutProfile profile)
    {
        try
        {
            var options = Current?.Options ?? new LayoutOptions();
            profile = Presets.Create(kind, clients, Monitors, options: options with { Fit = FitMode.ShrinkAnchor });
            return true;
        }
        catch (ArgumentException e)
        {
            Status.Warn(e.Message.Split(" (Parameter")[0]);
            profile = null!;
            return false;
        }
    }

    private void ApplyPreset()
    {
        if (Current is null || !TryPreset(PresetKind, PresetClients, out var preset)) return;
        // Keep the layout's name and the slot names the user typed.
        var slots = preset.Slots.Select((s, i) => i < Current.Slots.Count ? s with { Name = Current.Slots[i].Name } : s).ToList();
        Update(Current with { Clients = preset.Clients, Slots = slots });
        SelectedSlotIndex = 0;
        Status.Info($"Applied preset {Presets.DisplayName(PresetKind)} for {PresetClients} clients. Ctrl+Z undoes it.");
    }

    private void CopyCode()
    {
        if (Current is null) return;
        _dialogs.CopyToClipboard(ProfileJson.ToShareCode(Current));
        Status.Ok("Layout code copied (WBX1:…). Paste it to a friend or into the website.");
    }

    private void ImportCode()
    {
        var code = _dialogs.Prompt("Import layout code", "Paste a WBX1: code:", "");
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

    /// <summary>Makes a new layout from where the WoW windows are now (arranged by hand, Snap, …).</summary>
    private void Capture()
    {
        var open = WindowFinder.FindGameWindows();
        if (open.Count == 0)
        {
            Status.Warn("No World of Warcraft windows found to capture.");
            return;
        }
        var borderless = open.All(w => (w.Style & 0x00C00000) == 0);
        var profile = ProfileCapture.FromWindows(open.Select(w => WindowFinder.VisibleRectOf(w.Handle)).ToList(), Monitors, borderless, "Captured");
        AddAndSelect(profile);
        Status.Ok($"Captured {open.Count} windows as \"{SelectedProfile!.Name}\".");
    }

    // ---------- squad ----------

    public void RefreshWindows()
    {
        var found = WindowFinder.FindGameWindows();
        // keep the user's order for windows we already know, new ones go to the end (launch order)
        var known = Windows.Select(w => w.Window.Handle).ToList();
        var ordered = found.OrderBy(w => known.IndexOf(w.Handle) is var i && i >= 0 ? i : int.MaxValue).ToList();
        Windows.Clear();
        for (var i = 0; i < ordered.Count; i++) Windows.Add(new GameWindowItem(ordered[i]) { Slot = i + 1 });
    }

    private void MoveWindow(GameWindowItem? item, int delta)
    {
        if (item is null) return;
        var i = Windows.IndexOf(item);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Windows.Count) return;
        Windows.Move(i, j);
        for (var k = 0; k < Windows.Count; k++) Windows[k].Slot = k + 1;
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

    private async Task ArrangeAsync()
    {
        if (Plan() is not { } plan) return;
        if (Windows.Count == 0 || Windows.Any(w => !WindowFinder.Exists(w.Window.Handle))) RefreshWindows();
        var windows = Windows.Select(w => w.Window).ToList();
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

    public async Task StartSquadAsync()
    {
        if (Plan() is not { } plan || IsWatching) return;
        Busy = true;
        _watch = new CancellationTokenSource();
        OnPropertyChanged(nameof(IsWatching));
        try
        {
            Status.Title($"Start squad: \"{plan.Profile.Name}\"");
            SquadWatcher.EnsureBattleNet(Status, _settings.BattleNetExe, plan.ToolIsAdmin);
            Status.Title(SquadWatcher.BattleNetInstructions(Forever));
            Status.Title("Then pick the account and press Play – in this order:");
            for (var i = 0; i < plan.Slots.Count; i++)
            {
                var account = i < _settings.Accounts.Count ? $"   ({_settings.Accounts[i]})" : "";
                Status.Title($"   {i + 1}. {plan.Slots[i].Slot.Label}{account}");
            }
            Status.Info("Each game window snaps into place as soon as it appears.");
            var watcher = new SquadWatcher(plan, Status);
            var token = _watch.Token;
            await Task.Run(() => watcher.RunAsync(TimeSpan.FromMinutes(15), token));
            RefreshWindows();
        }
        finally
        {
            _watch.Dispose();
            _watch = null;
            Busy = false;
            OnPropertyChanged(nameof(IsWatching));
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
            var version = typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "?";
            await Task.Run(() => Diagnostics.RunAsync(plan, windows, report, $"WoWBoxer Window Layouts {version}", "per-monitor aware"));
            Directory.CreateDirectory(StatusLog.LogFolder);
            File.WriteAllText(path, report.Text.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase));
            Status.Ok($"Diagnose report saved: {path}");
            OpenFolder(StatusLog.LogFolder);
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
        public string Text { get { lock (_text) return _text.ToString(); } }
        public void Report(StatusLevel level, string message)
        {
            lock (_text) _text.AppendLine((level == StatusLevel.Title ? Environment.NewLine : "") + message);
        }
    }

    private static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true })?.Dispose();
    }
}
