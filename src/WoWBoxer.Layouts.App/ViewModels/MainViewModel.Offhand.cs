using System.Windows.Input;
using WoWBoxer.Layouts.App.Infrastructure;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App.ViewModels;

public sealed record OffhandClientChoice(nint Handle, uint Pid, DateTime? ProcessStartedUtc, string Label)
{
    public bool Matches(WindowInfo w) => Handle == w.Handle && Pid == w.Pid && ProcessStartedUtc == w.ProcessStartedUtc;
    public override string ToString() => Label;
}
public sealed record OffhandDisplayChoice(MonitorInfo Monitor, string Label)
{
    public override string ToString() => Label;
}

public sealed partial class MainViewModel
{
    private readonly OffhandWindow _offhand = new();
    private OffhandClientChoice? _offhandClient;
    private OffhandDisplayChoice? _offhandMain, _offhandWorkspace;
    private int _offhandPercent = 50;
    private bool _offhandWorking, _offhandWasActive;
    private string _offhandStatus = "";

    public IReadOnlyList<OffhandClientChoice> OffhandClients { get; private set; } = [];
    public IReadOnlyList<OffhandDisplayChoice> OffhandDisplays { get; private set; } = [];
    public IReadOnlyList<int> OffhandPercentages { get; } = [25, 50, 75, 100];
    public ICommand ExpandOffhandCommand { get; private set; } = null!;
    public ICommand RestoreOffhandCommand { get; private set; } = null!;
    public ICommand IdentifyOffhandCommand { get; private set; } = null!;
    public bool CanChooseOffhand => !_offhandWorking && _offhand.ActiveWindow is null;

    public OffhandClientChoice? OffhandClient
    {
        get => _offhandClient;
        set { if (Set(ref _offhandClient, value)) OffhandSelectionChanged(); }
    }

    public OffhandDisplayChoice? OffhandMain
    {
        get => _offhandMain;
        set { if (Set(ref _offhandMain, value)) OffhandSelectionChanged(); }
    }

    public OffhandDisplayChoice? OffhandWorkspace
    {
        get => _offhandWorkspace;
        set { if (Set(ref _offhandWorkspace, value)) OffhandSelectionChanged(); }
    }

    public int OffhandPercent
    {
        get => _offhandPercent;
        set { if (Set(ref _offhandPercent, value)) OffhandSelectionChanged(); }
    }

    public string OffhandStatus { get => _offhandStatus; private set => Set(ref _offhandStatus, value); }

    public string OffhandPreview
    {
        get
        {
            if (OffhandClients.Count == 0) return "Open the WoW client you want to extend.";
            if (OffhandClient is null) return "Select a running WoW client. Identify marks that window on your desktop.";
            if (OffhandDisplays.Count < 2) return "Connect two monitors and extend your desktop in Windows Display settings.";
            if (!TryOffhandBounds(out var bounds, out var error)) return error;
            return $"{bounds.Width} × {bounds.Height} · {OffhandSpan.Calibration(OffhandMain!.Monitor, OffhandWorkspace!.Monitor, bounds)}";
        }
    }

    private void InitializeOffhand()
    {
        ExpandOffhandCommand = new RelayCommand(() => _ = ChangeOffhandAsync(restore: false),
            () => !Busy && CanChooseOffhand && OffhandClient is not null && TryOffhandBounds(out _, out _));
        RestoreOffhandCommand = new RelayCommand(() => _ = ChangeOffhandAsync(restore: true),
            () => !Busy && !_offhandWorking && _offhand.ActiveWindow is not null);
        IdentifyOffhandCommand = new RelayCommand(() =>
        {
            if (FindOffhandClient() is { } w)
                _dialogs.ShowIdentify([(WindowFinder.VisibleRectOf(w.Handle), w.Pid.ToString(), w.Title)]);
        }, () => OffhandClient is not null);
    }

    private void OffhandSelectionChanged()
    {
        if (CanChooseOffhand) OffhandStatus = "";
        OnPropertyChanged(nameof(OffhandPreview));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshOffhandClients(IReadOnlyList<WindowInfo> all)
    {
        var choices = all.Select(w =>
        {
            var slot = _keeper.Slots.TryGetValue(w.Handle, out var i) ? $"Slot {i + 1} · " : "";
            var name = string.IsNullOrWhiteSpace(w.Title) ? w.ProcessName : w.Title;
            return new OffhandClientChoice(w.Handle, w.Pid, w.ProcessStartedUtc, $"{slot}{name} · PID {w.Pid}");
        }).ToList();
        if (!choices.SequenceEqual(OffhandClients))
        {
            var previous = OffhandClient;
            OffhandClients = choices;
            OnPropertyChanged(nameof(OffhandClients));
            OffhandClient = previous is null ? null : choices.FirstOrDefault(w =>
                w.Handle == previous.Handle && w.Pid == previous.Pid && w.ProcessStartedUtc == previous.ProcessStartedUtc);
            OnPropertyChanged(nameof(OffhandPreview));
        }
        if (_offhandWasActive && !_offhandWorking && _offhand.ActiveWindow is null)
        {
            _offhandWasActive = false;
            OffhandClient = null;
            OffhandStatus = "The extended client closed. Select a client to start another Offhand session.";
            OnPropertyChanged(nameof(CanChooseOffhand));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void RefreshOffhandDisplays()
    {
        var previousMain = OffhandMain?.Monitor;
        var previousWorkspace = OffhandWorkspace?.Monitor;
        OffhandDisplays = Monitors.Select(m => new OffhandDisplayChoice(m,
            $"Monitor {m.Index} · {ScreenSet.Describe([m], Monitors)}")).ToList();
        OnPropertyChanged(nameof(OffhandDisplays));
        OffhandMain = previousMain is null ? null : OffhandDisplays.FirstOrDefault(d => d.Monitor == previousMain);
        OffhandWorkspace = OffhandDisplays.FirstOrDefault(d => d.Monitor == previousWorkspace);
        OnPropertyChanged(nameof(OffhandPreview));
        if (_offhand.ActiveWindow is not null && (OffhandMain is null || OffhandWorkspace is null))
            OffhandStatus = "The display arrangement changed. Restore the client and choose its monitors again.";
    }

    private WindowInfo? FindOffhandClient()
    {
        if (OffhandClient is not { } selected) return null;
        return WindowFinder.FindGameWindows().FirstOrDefault(selected.Matches);
    }

    private bool TryOffhandBounds(out PixelRect bounds, out string error)
    {
        if (OffhandMain is { } main && OffhandWorkspace is { } workspace)
            return OffhandSpan.TryCreate(main.Monitor, workspace.Monitor, Monitors, OffhandPercent, out bounds, out error);
        bounds = default;
        error = "Choose your gameplay monitor and an adjacent workspace monitor.";
        return false;
    }

    private async Task ChangeOffhandAsync(bool restore)
    {
        if (Busy || _offhandWorking) return;
        _offhandWorking = true;
        Busy = true;
        OnPropertyChanged(nameof(CanChooseOffhand));
        await _liveLock.WaitAsync();
        try
        {
            if (restore)
            {
                await _offhand.RestoreAsync();
                OffhandStatus = "Window restored. Normal slot placement can resume.";
            }
            else
            {
                RefreshMonitors();
                if (FindOffhandClient() is not { } window) throw new InvalidOperationException("That WoW window has closed. Select a running client.");
                if (!TryOffhandBounds(out var bounds, out var error)) throw new InvalidOperationException(error);
                await _offhand.ExpandAsync(window, bounds);
                OffhandStatus = $"PID {window.Pid} is extended. In this client, run /offhand wizard and choose Configure Manually.";
            }
            Status.Ok(OffhandStatus);
        }
        catch (InvalidOperationException e)
        {
            OffhandStatus = e.Message;
            Status.Warn(e.Message);
        }
        finally
        {
            _liveLock.Release();
            _offhandWasActive = _offhand.ActiveWindow is not null;
            _offhandWorking = false;
            Busy = false;
            OnPropertyChanged(nameof(CanChooseOffhand));
            RefreshWindows();
        }
    }

    public void RestoreOffhandOnExit()
    {
        try { _offhand.RestoreAsync().GetAwaiter().GetResult(); }
        catch (InvalidOperationException e) { Status.Warn(e.Message); }
    }
}
