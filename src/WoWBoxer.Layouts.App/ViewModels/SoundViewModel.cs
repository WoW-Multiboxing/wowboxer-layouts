using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WoWBoxer.Layouts.App.Infrastructure;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App.ViewModels;

/// <summary>The squad as sound sees it: client process → exe, which client is in the main slot, and the client numbers.</summary>
public sealed record SoundSquad(IReadOnlyDictionary<uint, string> Clients, uint? MainPid, IReadOnlyDictionary<uint, int> Numbers, string? Exe);

/// <summary>
/// "Sound follows the main window" (Settings › Sound and the speaker on the Setup screen; off by default, owner 2026-11-10):
/// the WoW client in the main slot plays, the other clients are muted or turned down – through Windows' volume mixer
/// only, nothing is sent to the game. Follows every swap, Re-arrange, new and closed window at once; optionally the
/// active window instead. Each client's own mixer level is kept and put back when it is turned off, when the app quits
/// and on uninstall.
/// </summary>
public sealed class SoundViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly StatusLog _status;
    private readonly Func<SoundSquad> _squad;
    private readonly SoundFollower _follower;
    private readonly DispatcherTimer _activeTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private IAudioMixer? _mixer;
    private uint? _loud;
    private uint? _lastActive;
    private int _percent;
    private string _statusText = "";
    private WowSoundSetting? _wow;
    private string? _wowExe;
    private DateTime _wowReadAt;
    private bool _wowClosed;

    public SoundViewModel(AppSettings settings, StatusLog status, Func<SoundSquad> squad, Action openGuide)
    {
        _settings = settings;
        _status = status;
        _squad = squad;
        _percent = Math.Clamp(settings.SoundFollowerPercent, 0, 100);
        _follower = new SoundFollower(settings.SoundOriginals, settings.Save);
        _activeTimer.Tick += (_, _) => FollowActiveWindow();
        OpenGuideCommand = new RelayCommand(openGuide);
        CommitPercentCommand = new RelayCommand(CommitPercent);
        FixBackgroundCommand = new RelayCommand(FixBackground, () => CanFixBackground);
        UpdateTimer();
    }

    public ICommand OpenGuideCommand { get; }
    /// <summary>The follower slider was let go: keep the new level in the settings.</summary>
    public ICommand CommitPercentCommand { get; }
    /// <summary>Turns WoW's "Sound in Background" on in Config.wtf – only while every client of that install is closed.</summary>
    public ICommand FixBackgroundCommand { get; }

    public const string Note = "Changes only Windows' volume mixer for the WoW clients – nothing is sent to the game, other programs keep their sound.";

    public bool Enabled
    {
        get => _settings.SoundFollows;
        set
        {
            if (value == _settings.SoundFollows) return;
            _settings.SoundFollows = value;
            _settings.Save();
            if (value)
            {
                _loud = null;
                Now();
                _status.Ok($"Sound follows the {(FollowActive ? "active" : "main")} window: {StatusText}");
            }
            else
            {
                var n = RestoreAll();
                _status.Ok(n > 0 ? "Sound follows the main window is off: every WoW client has its own volume back." : "Sound follows the main window is off.");
                SetStatus("");
            }
            UpdateTimer();
            RaiseAll();
        }
    }

    /// <summary>Follow the window that has the keyboard instead of the main slot.</summary>
    public bool FollowActive
    {
        get => _settings.SoundMode == SoundFollowMode.ActiveWindow;
        set
        {
            var mode = value ? SoundFollowMode.ActiveWindow : SoundFollowMode.MainSlot;
            if (mode == _settings.SoundMode) return;
            _settings.SoundMode = mode;
            _settings.Save();
            UpdateTimer();
            Now();
            RaiseAll();
        }
    }

    /// <summary>The followers' level in % of their normal level (0 = muted). Applies while the slider moves.</summary>
    public double FollowerPercent
    {
        get => _percent;
        set
        {
            var p = Math.Clamp((int)Math.Round(value / 5) * 5, 0, 100);
            if (p == _percent) return;
            _percent = p;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FollowerText));
            if (Enabled) Now();
        }
    }

    public string FollowerText => _percent == 0 ? "Muted" : $"{_percent} %";

    /// <summary>"Client 2 plays · clients 1, 3 muted" (or why nothing plays yet).</summary>
    public string StatusText => _statusText;

    /// <summary>What to change in WoW so the main window keeps playing, or "".</summary>
    public string HintText => Enabled && _wow is { } w ? w.Hint(_settings.SoundMode) : "";

    public bool HasHint => HintText.Length > 0;

    /// <summary>The hint is about Sound in Background and all clients of that install are closed: the app may set it.</summary>
    public bool CanFixBackground => HasHint && _wow is { AllSound: true, InBackground: false } && _wowClosed && _wowExe is not null;

    public string FixBackgroundTip => CanFixBackground
        ? "Sets Sound in Background in this install's Config.wtf (a backup is kept). Every client of the install is closed now."
        : "Possible only while every client of this install is closed (WoW rewrites Config.wtf when it closes). Or set it in game: Options › Audio.";

    private void CommitPercent()
    {
        if (_settings.SoundFollowerPercent == _percent) return;
        _settings.SoundFollowerPercent = _percent;
        _settings.Save();
        if (Enabled) _status.Info(_percent == 0 ? "Sound: the other clients are muted." : $"Sound: the other clients play at {_percent} % of their level.");
    }

    /// <summary>Every second (from the app's tick): new sessions, closed clients, the Sound in Background hint.</summary>
    public void Tick()
    {
        if (Enabled) Now();
        else if (_settings.SoundOriginals.Count > 0 && Mixer() is { } m && _follower.RestorePending(m, ProcessInfo.ImagePath) > 0)
            _status.Info("Sound: a WoW client got its own volume back (it was closed while WoWBoxer had turned it down).");
        if (DateTime.UtcNow - _wowReadAt > TimeSpan.FromSeconds(10)) ReadWow(_squad().Exe);
    }

    /// <summary>At once: after a swap, Re-arrange, a canvas swap, a new or closed window.</summary>
    public void Now()
    {
        if (!Enabled || Mixer() is not { } mixer) return;
        var squad = _squad();
        var loud = Loud(squad);
        _follower.Update(mixer, loud, squad.Clients, _percent);
        if (loud != _loud || _statusText.Length == 0 || _wowExe != squad.Exe)
        {
            _loud = loud;
            SetStatus(Describe(squad, loud, mixer));
            if (_wowExe != squad.Exe) ReadWow(squad.Exe);
        }
    }

    /// <summary>On quit (also before an update restarts the app): every client gets its own level back.</summary>
    public void OnAppExit()
    {
        if (_mixer is null && _settings.SoundOriginals.Count == 0) return;
        RestoreAll();
        (_mixer as IDisposable)?.Dispose();
        _mixer = null;
    }

    /// <summary>Velopack's uninstall hook: the app is going away, so its changes to the volume mixer go too.</summary>
    public static void RestoreOnUninstall()
    {
        var settings = AppSettings.Load();
        if (settings.SoundOriginals.Count == 0) return;
        var mixer = AudioMixer.Create();
        try
        {
            new SoundFollower(settings.SoundOriginals, settings.Save).Restore(mixer, ProcessInfo.ImagePath);
        }
        finally
        {
            (mixer as IDisposable)?.Dispose();
        }
    }

    private int RestoreAll() => Mixer() is { } m ? _follower.Restore(m, ProcessInfo.ImagePath) : 0;

    /// <summary>The client that plays: the main slot's (or, with no main window or "active window" on, the last WoW window that had the keyboard).</summary>
    private uint? Loud(SoundSquad squad)
    {
        if (squad.Clients.Count == 0) return null;
        if (!FollowActive && squad.MainPid is { } main && squad.Clients.ContainsKey(main)) return main;
        var active = ActivePid();
        if (active is { } a && squad.Clients.ContainsKey(a)) _lastActive = a;
        if (_lastActive is { } last && squad.Clients.ContainsKey(last)) return last;
        return squad.MainPid is { } m && squad.Clients.ContainsKey(m) ? m : squad.Clients.Keys.Min();
    }

    private static uint? ActivePid()
    {
        var h = GlobalHotkeys.ForegroundWindow();
        var pid = h == 0 ? 0 : WindowFinder.PidOf(h);
        return pid == 0 ? null : pid;
    }

    /// <summary>"Active window" mode: a 200 ms look at which window has the keyboard (no hooks, nothing sent).</summary>
    private void FollowActiveWindow()
    {
        var active = ActivePid();
        if (active is null || active == _loud) return;
        var squad = _squad();
        if (squad.Clients.ContainsKey(active.Value)) Now();
    }

    private void UpdateTimer()
    {
        if (Enabled && FollowActive) _activeTimer.Start();
        else _activeTimer.Stop();
    }

    private string Describe(SoundSquad squad, uint? loud, IAudioMixer mixer)
    {
        if (loud is null) return "No WoW client is open.";
        string Name(uint pid) => squad.Numbers.TryGetValue(pid, out var n) ? n.ToString() : "?";
        var withSound = mixer.Sessions().Select(s => s.Pid).ToHashSet();
        var others = squad.Clients.Keys.Where(p => p != loud).OrderBy(p => squad.Numbers.GetValueOrDefault(p)).ToList();
        var text = $"client {Name(loud.Value)} plays";
        if (others.Count > 0) text += $" · client{(others.Count == 1 ? "" : "s")} {string.Join(", ", others.Select(Name))} {(_percent == 0 ? "muted" : $"at {_percent} %")}";
        if (!FollowActive && squad.MainPid is null) text += " · this layout has no main window, so sound follows the active window";
        var silent = squad.Clients.Keys.Where(p => !withSound.Contains(p)).ToList();
        if (silent.Count > 0) text += $" · client{(silent.Count == 1 ? "" : "s")} {string.Join(", ", silent.Select(Name))} not in the volume mixer yet (no sound played so far)";
        return char.ToUpperInvariant(text[0]) + text[1..] + ".";
    }

    private IAudioMixer? Mixer()
    {
        if (_mixer is not null) return _mixer;
        try
        {
            _mixer = AudioMixer.Create();
            if (_mixer is AudioMixer real) real.Changed += () => Application.Current?.Dispatcher.InvokeAsync(Now);
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidCastException)
        {
            _status.Warn($"Sound: Windows' volume mixer is not available ({e.Message}).");
            _mixer = null;
        }
        return _mixer;
    }

    private void ReadWow(string? exe)
    {
        _wowReadAt = DateTime.UtcNow;
        _wowExe = exe;
        if (exe is null || exe.Length == 0)
        {
            _wow = null;
            _wowClosed = false;
        }
        else
        {
            _wow = Sandbox.Active ? new WowSoundSetting(true, true) : WowSound.ReadFile(WowConfig.ConfigPathFor(exe));
            _wowClosed = UiScaleFix.RunningClients(Path.GetDirectoryName(exe) ?? "") == 0;
        }
        OnPropertyChanged(nameof(HintText));
        OnPropertyChanged(nameof(HasHint));
        OnPropertyChanged(nameof(CanFixBackground));
        OnPropertyChanged(nameof(FixBackgroundTip));
        CommandManager.InvalidateRequerySuggested();
    }

    private void FixBackground()
    {
        if (_wowExe is not { } exe) return;
        var error = UiScaleFix.EditConfig(exe, WowSound.WithBackgroundSound);
        if (error is null) _status.Ok("Sound in Background is on for this WoW install (Config.wtf; a backup is kept). It applies when the clients start.");
        else _status.Warn($"Sound in Background: {error}");
        ReadWow(exe);
    }

    private void SetStatus(string text)
    {
        if (text == _statusText) return;
        _statusText = text;
        OnPropertyChanged(nameof(StatusText));
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(FollowActive));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HintText));
        OnPropertyChanged(nameof(HasHint));
        OnPropertyChanged(nameof(CanFixBackground));
        OnPropertyChanged(nameof(FixBackgroundTip));
    }
}
