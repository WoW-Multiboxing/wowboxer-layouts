using System.Windows.Input;
using WoWBoxer.Layouts.App.Infrastructure;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App.ViewModels;

/// <summary>
/// The built-in switch for Windows' "Activate a window by hovering over it with the mouse" (Setup, Settings, tray, Guide).
/// Changes only that Windows setting and only when the user asks; the first change saves the user's own values so they
/// can be put back (Settings, on quit if wanted, on uninstall). Changes made outside the app show up within a second.
/// </summary>
public sealed class HoverViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly StatusLog _status;
    private HoverSettings _now;
    private int _delayPreview;

    public HoverViewModel(AppSettings settings, StatusLog status, Action openGuide, Action<string> openUrl)
    {
        _settings = settings;
        _status = status;
        _now = HoverActivation.Read();
        _delayPreview = _now.DelayMs;
        RestoreCommand = new RelayCommand(RestoreOriginal, () => HoverSettings.NeedsRestore(_settings.HoverOriginal, _now));
        OpenGuideCommand = new RelayCommand(openGuide);
        OpenRulesCommand = new RelayCommand(() => openUrl(RulesUrl));
        CommitDelayCommand = new RelayCommand(CommitDelay);
        ResetDelayCommand = new RelayCommand(ResetDelay, () => _now.DelayMs != DefaultDelay || _delayPreview != DefaultDelay);
    }

    public const string RulesUrl = MainViewModel.WebsiteUrl + "/rules/#hover-to-activate-a-windows-setting";
    private const int DefaultDelay = HoverSettings.DefaultDelayMs;

    public ICommand RestoreCommand { get; }
    public ICommand OpenGuideCommand { get; }
    public ICommand OpenRulesCommand { get; }
    /// <summary>The delay slider was let go (mouse up or a key): write the new delay to Windows.</summary>
    public ICommand CommitDelayCommand { get; }
    /// <summary>"Reset to default": the delay back to 30 ms, at once. Not the same as Restore (the user's own values).</summary>
    public ICommand ResetDelayCommand { get; }

    public bool Enabled
    {
        get => _now.Enabled;
        set
        {
            // On always starts at the default delay (written together with on); the slider jumps there with it.
            if (value) Apply(s => s.TurnedOn(), $"Hover to activate is on (delay {DefaultDelay} ms): the window under the mouse becomes active.");
            else Apply(s => s with { Enabled = false }, "Hover to activate is off.");
        }
    }

    public bool BringToFront
    {
        get => _now.BringToFront;
        set => Apply(s => s with { BringToFront = value }, value ? "Hover to activate brings the window to the front." : "Hover to activate leaves the window where it is in the stack.");
    }

    /// <summary>The slider's value while it moves (label only, steps of 5 ms); <see cref="CommitDelay"/> writes it when it is let go.</summary>
    public double DelayPreview
    {
        get => _delayPreview;
        set => SetDelayPreview((int)Math.Round(value / 5) * 5);
    }

    public string DelayText => _delayPreview == DefaultDelay ? $"{_delayPreview} ms · default" : $"{_delayPreview} ms";

    public string DefaultDelayText => $"Default: {DefaultDelay} ms";

    private void SetDelayPreview(int ms)
    {
        if (ms == _delayPreview) return;
        _delayPreview = ms;
        OnPropertyChanged(nameof(DelayPreview));
        OnPropertyChanged(nameof(DelayText));
    }

    public void CommitDelay()
    {
        var ms = HoverSettings.ClampDelay(_delayPreview);
        if (ms != _now.DelayMs) Apply(s => s with { DelayMs = ms }, $"Hover to activate: delay {ms} ms.");
    }

    private void ResetDelay()
    {
        SetDelayPreview(DefaultDelay);
        Apply(s => s with { DelayMs = DefaultDelay }, $"Hover to activate: delay back to the default, {DefaultDelay} ms.");
    }

    public bool RestoreOnExit
    {
        get => _settings.RestoreHoverOnExit;
        set
        {
            _settings.RestoreHoverOnExit = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public bool TurnOffOnExit
    {
        get => _settings.TurnHoverOffOnExit;
        set
        {
            _settings.TurnHoverOffOnExit = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string OriginalText => _settings.HoverOriginal is { } o
        ? $"Your Windows setting before WoWBoxer changed it: {o.Describe()}."
        : "WoWBoxer has not changed this Windows setting yet.";

    /// <summary>Picks up changes made outside the app (Windows settings, another tool). Cheap: three system calls.</summary>
    public void Refresh()
    {
        var now = HoverActivation.Read();
        if (now == _now) return;
        var delayChanged = now.DelayMs != _now.DelayMs;
        _now = now;
        if (delayChanged) SetDelayPreview(now.DelayMs);
        RaiseAll();
    }

    /// <summary>On quit: the user's own values back if they asked for that, else hover off (on by default).</summary>
    public void OnAppExit()
    {
        var now = HoverActivation.Read();
        if (_settings.RestoreHoverOnExit && HoverSettings.NeedsRestore(_settings.HoverOriginal, now))
            HoverActivation.Write(_settings.HoverOriginal!);
        else if (!_settings.RestoreHoverOnExit && _settings.TurnHoverOffOnExit && now.Enabled)
            HoverActivation.Write(now with { Enabled = false });
    }

    /// <summary>Velopack's uninstall hook: the app is going away, so its change to Windows goes too.</summary>
    public static void RestoreOnUninstall()
    {
        var original = AppSettings.Load().HoverOriginal;
        if (HoverSettings.NeedsRestore(original, HoverActivation.Read())) HoverActivation.Write(original!);
    }

    private void RestoreOriginal()
    {
        if (_settings.HoverOriginal is not { } original) return;
        Write(original, $"Your original Windows setting is back: hover to activate {original.Describe()}.");
    }

    /// <summary>One change on top of what Windows has now (not the cached value, which may be up to a second old).</summary>
    private void Apply(Func<HoverSettings, HoverSettings> change, string message)
    {
        var now = HoverActivation.Read();
        var wanted = change(now);
        if (wanted == now)
        {
            Refresh();
            return;
        }
        if (_settings.HoverOriginal is null)
        {
            // The first change ever: keep what the user had, so it can always be put back. Never replaced later.
            _settings.HoverOriginal = now;
            _settings.Save();
        }
        Write(wanted, message);
    }

    private void Write(HoverSettings wanted, string message)
    {
        if (HoverActivation.Write(wanted)) _status.Ok(message);
        else _status.Warn("Windows did not accept the hover-to-activate change.");
        _now = HoverActivation.Read();
        SetDelayPreview(_now.DelayMs);
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(BringToFront));
        OnPropertyChanged(nameof(OriginalText));
        CommandManager.InvalidateRequerySuggested();
    }
}
