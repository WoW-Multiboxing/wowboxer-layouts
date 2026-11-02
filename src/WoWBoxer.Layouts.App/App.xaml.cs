using System.Windows;
using WoWBoxer.Layouts.Win32;
using Forms = System.Windows.Forms;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Start-up: one instance only, optional "--tray" (start hidden in the tray), "--start NAME" (that layout, and open the
/// missing clients – used by desktop shortcuts), "--pause" / "--resume" (Stop / Start placing windows; also sent to a
/// running copy, e.g. from a script), first-run screen, tray icon.
/// </summary>
public partial class App : Application
{
    private static string InstanceName => AppFlavor.InstanceName;
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private Forms.NotifyIcon? _tray;
    private MainWindow? _main;
    private Forms.ToolStripMenuItem? _pauseItem, _hoverItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Dpi.EnsurePerMonitorV2();
        // English UI with the same number format on every PC ("1.25", not "1,25").
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        if (DemoArgs(e.Args) is { } demo)
        {
            StartDemo(demo.Shots);
            return;
        }

        _mutex = new Mutex(true, InstanceName, out var first);
        if (!first)
        {
            // Already running: hand our arguments (e.g. a shortcut's "--start NAME") to it and bring it up.
            try
            {
                System.IO.Directory.CreateDirectory(AppSettings.Folder);
                System.IO.File.WriteAllLines(PendingArgsFile, e.Args);
                EventWaitHandle.OpenExisting(InstanceName + ".Show").Set();
            }
            catch (System.IO.IOException)
            {
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
            Shutdown();
            return;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        new Thread(() =>
        {
            while (_showSignal.WaitOne()) Dispatcher.BeginInvoke(OnSecondInstance);
        }) { IsBackground = true, Name = "single-instance" }.Start();

        var settings = AppSettings.Load();
        var (startInTray, startProfile) = ParseArgs(e.Args);
        // Opens stopped: nothing is moved until Start (another game may be open). A layout shortcut, "--resume" or the
        // Settings switch start it right away.
        var paused = PauseArg(e.Args) ?? !(settings.StartPlacingOnOpen || startProfile is not null);

        if (!settings.FirstRunDone)
        {
            FirstRunWindow.Show(null);
            settings.FirstRunDone = true;
            settings.LastSeenVersion = ViewModels.MainViewModel.AppVersion;   // a fresh install has nothing "new"
            settings.Save();
            startInTray = false;
        }

        _main = new MainWindow(settings, paused);
        DispatcherUnhandledException += (_, args) =>
        {
            // Log and keep running: a placement tool must not vanish in the middle of a squad start.
            _main.ViewModel.Status.Report(StatusLevel.Error, "Unexpected error: " + args.Exception.Message);
            System.IO.File.AppendAllText(System.IO.Path.Combine(ViewModels.StatusLog.LogFolder, "app.log"), args.Exception + Environment.NewLine);
            args.Handled = true;
        };
        _main.StateChanged += (_, _) =>
        {
            if (_main.WindowState == WindowState.Minimized) _main.Hide();   // minimise = to the tray
        };
        _main.Closed += (_, _) => Shutdown();
        CreateTray();
        if (!startInTray) _main.Show();
        var version = ViewModels.MainViewModel.AppVersion;
        if (!startInTray && WhatsNewWindow.ShouldShow(settings, version))   // a tray start shows it next time
        {
            WhatsNewWindow.Show(_main, version);
            settings.LastSeenVersion = version;
            settings.Save();
        }
        _ = Community.RefreshAsync(settings.CheckForUpdates);   // community links, at most once a day
        if (settings.CheckForUpdates)
            _ = Updater.CheckAsync(_main.ViewModel.Status, settings.UpdateChannel, startInTray ? null : version => PromptWindow.Confirm(_main, "Update available",
                $"WoWBoxer {version} is ready (you have {ViewModels.MainViewModel.AppVersion}).\n\nUpdate now? The app closes and opens again in a few seconds. Later installs it when you quit.",
                "Update now", "Later"), e.Args);

        if (AppFlavor.OtherIsRunning())
            _main.ViewModel.Status.Warn(AppFlavor.IsDev
                ? "The normal WoWBoxer is open too. Both place the same WoW windows – stop or quit one of them before you press Start."
                : "WoWBoxer Dev is open too. Both place the same WoW windows – stop or quit one of them before you press Start.");

        if (startProfile is not null) StartProfile(startProfile);
    }

    /// <summary>"--demo" (made-up monitors and squad, nothing real touched) and "--screenshots FOLDER" (demo + render every screen, then quit).</summary>
    private static (bool Demo, string? Shots)? DemoArgs(string[] args)
    {
        var i = Array.FindIndex(args, a => a.Equals("--screenshots", StringComparison.OrdinalIgnoreCase));
        var shots = i >= 0 && i + 1 < args.Length ? System.IO.Path.GetFullPath(args[i + 1]) : null;
        return shots is not null || args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase)) ? (true, shots) : null;
    }

    /// <summary>Demo mode: its own temporary settings, no single-instance lock, tray, update check or network.</summary>
    private async void StartDemo(string? shots)
    {
        _main = new MainWindow(Demo.Start());
        _main.Closed += (_, _) => Shutdown();
        if (shots is null)
        {
            _main.Show();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            Console.Error.WriteLine(args.Exception);
            args.Handled = true;
            Shutdown(1);
        };
        try
        {
            await Screenshots.RunAsync(_main, shots, TrayMenu);
            Shutdown(0);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            System.IO.File.WriteAllText(System.IO.Path.Combine(shots, "error.txt"), error.ToString());
            Shutdown(1);
        }
    }

    private static string PendingArgsFile => System.IO.Path.Combine(AppSettings.Folder, "pending-args.txt");

    /// <summary>"--pause" (or "--paused") = true, "--resume" = false, neither = null.</summary>
    private static bool? PauseArg(string[] args) =>
        args.Any(a => a.Equals("--pause", StringComparison.OrdinalIgnoreCase) || a.Equals("--paused", StringComparison.OrdinalIgnoreCase)) ? true
        : args.Any(a => a.Equals("--resume", StringComparison.OrdinalIgnoreCase)) ? false
        : null;

    /// <summary>"--swap next|previous|home|1…10": a window-swap action for the running copy (scripts, tests). Null if absent or unknown.</summary>
    private static string? SwapArg(string[] args)
    {
        var i = Array.FindIndex(args, a => a.Equals("--swap", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= args.Length) return null;
        var v = args[i + 1].ToLowerInvariant();
        if (int.TryParse(v, out var n)) return n is >= 1 and <= Core.Presets.MaxClients ? Core.SwapActions.Client(n) : null;
        return v is Core.SwapActions.Next or Core.SwapActions.Previous or Core.SwapActions.Home ? v : null;
    }

    private static (bool Tray, string? Start) ParseArgs(string[] args)
    {
        var tray = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        var i = Array.FindIndex(args, a => a.Equals("--start", StringComparison.OrdinalIgnoreCase));
        return (tray, i >= 0 && i + 1 < args.Length ? args[i + 1] : null);
    }

    /// <summary>Another start of the app (shortcut, Start menu): follow its arguments, or just show the window.</summary>
    private void OnSecondInstance()
    {
        string[] args = [];
        try
        {
            if (System.IO.File.Exists(PendingArgsFile))
            {
                args = System.IO.File.ReadAllLines(PendingArgsFile);
                System.IO.File.Delete(PendingArgsFile);
            }
        }
        catch (System.IO.IOException)
        {
        }
        var (tray, start) = ParseArgs(args);
        if (SwapArg(args) is { } swap && _main is not null)
        {
            _main.ViewModel.RunSwapAction(swap);   // the same as the hotkey; the window stays as it is
            return;
        }
        if (PauseArg(args) is { } pause && _main is not null)
        {
            _main.ViewModel.Paused = pause;   // a pause/resume request only: the window stays as it is
            return;
        }
        if (!tray) ShowMain();
        if (start is not null) StartProfile(start);
    }

    private void StartProfile(string name)
    {
        if (_main is null) return;
        if (_main.ViewModel.SelectProfile(name))
        {
            _tray?.ShowBalloonTip(4000, AppFlavor.Name, $"\"{name}\": log in to each client – every window is placed as it opens.", Forms.ToolTipIcon.Info);
            _main.ViewModel.StartLayout(name);
        }
        else
        {
            ShowMain();
            _main.ViewModel.Status.Warn($"No layout called \"{name}\".");
        }
    }

    private void CreateTray()
    {
        var menu = TrayMenu();
        using var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _tray = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream),
            Text = AppFlavor.Name,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowMain();
    }

    /// <summary>The tray icon's menu (also drawn for the website's screenshots).</summary>
    private Forms.ContextMenuStrip TrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add($"Open {AppFlavor.Name}", null, (_, _) => ShowMain());
        menu.Items.Add("Open missing WoW clients", null, (_, _) => Run(_main!.ViewModel.OpenClientsCommand));
        menu.Items.Add("Re-arrange", null, (_, _) => Run(_main!.ViewModel.ArrangeCommand));
        menu.Items.Add("Identify windows", null, (_, _) => Run(_main!.ViewModel.IdentifyCommand));
        menu.Items.Add(new Forms.ToolStripSeparator());
        // Start / Stop placing windows, the same as the button on the main screen.
        _pauseItem = new Forms.ToolStripMenuItem("Start placing windows", null, (_, _) => Run(_main!.ViewModel.PauseCommand));
        menu.Items.Add(_pauseItem);
        // Windows' hover-to-activate (a Windows setting; the app sends no input). Ticked = on, as Windows has it now.
        _hoverItem = new Forms.ToolStripMenuItem("Hover to activate windows", null, (_, _) =>
        {
            var hover = _main!.ViewModel.Hover;
            hover.Enabled = !hover.Enabled;
        });
        menu.Items.Add(_hoverItem);
        // Window swap (Settings › Advanced): only shown while its hotkeys are on
        var homeItem = new Forms.ToolStripMenuItem("Back to home layout", null, (_, _) => _main!.ViewModel.RunSwapAction(Core.SwapActions.Home));
        var swapOffItem = new Forms.ToolStripMenuItem("Disable swap hotkeys", null, (_, _) => _main!.ViewModel.SwapHotkeysEnabled = false);
        menu.Items.Add(homeItem);
        menu.Items.Add(swapOffItem);
        void UpdateItems()
        {
            _pauseItem.Text = _main!.ViewModel.Paused ? "Start placing windows" : "Stop placing windows";
            _main.ViewModel.Hover.Refresh();
            _hoverItem.Checked = _main.ViewModel.Hover.Enabled;
            homeItem.Visible = swapOffItem.Visible = _main.ViewModel.SwapHotkeysEnabled;
            homeItem.Enabled = _main.ViewModel.IsSwapped;
        }
        menu.Opening += (_, _) => UpdateItems();
        if (_main is not null) UpdateItems();
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Report a problem…", null, (_, _) =>
        {
            ShowMain();
            _main!.ViewModel.SelectedTab = ViewModels.AppTab.Help;
            Run(_main.ViewModel.ReportProblemCommand);
        });
        menu.Items.Add("Quit", null, (_, _) => Shutdown());
        TrayMenuStyle.Apply(menu);
        return menu;
    }

    private static void Run(System.Windows.Input.ICommand command)
    {
        if (command.CanExecute(null)) command.Execute(null);
    }

    private void ShowMain()
    {
        if (_main is null) return;
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();   // our own window, opened by the user: fine to activate
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!Updater.RestartingForUpdate) _main?.ViewModel.Hover.OnAppExit();   // hover off (or the user's own values) on quit
        _main?.ViewModel.DisposeHotkeys();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _showSignal?.Dispose();
        _mutex?.Dispose();
        Demo.End();
        base.OnExit(e);
    }
}
