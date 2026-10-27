using System.Windows;
using WoWBoxer.Layouts.Win32;
using Forms = System.Windows.Forms;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Start-up: one instance only, optional "--tray" (start hidden in the tray) and "--start NAME" (start that layout's
/// squad right away, used by desktop shortcuts), first-run screen, tray icon.
/// </summary>
public partial class App : Application
{
    private const string InstanceName = "WoWBoxer.Layouts.SingleInstance";
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private Forms.NotifyIcon? _tray;
    private MainWindow? _main;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Dpi.EnsurePerMonitorV2();
        // English UI with the same number format on every PC ("1.25", not "1,25").
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        _mutex = new Mutex(true, InstanceName, out var first);
        if (!first)
        {
            // Already running: bring that window up instead.
            try
            {
                EventWaitHandle.OpenExisting(InstanceName + ".Show").Set();
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
            while (_showSignal.WaitOne()) Dispatcher.BeginInvoke(ShowMain);
        }) { IsBackground = true, Name = "single-instance" }.Start();

        var settings = AppSettings.Load();
        var startInTray = e.Args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        var startIndex = Array.FindIndex(e.Args, a => a.Equals("--start", StringComparison.OrdinalIgnoreCase));
        var startProfile = startIndex >= 0 && startIndex + 1 < e.Args.Length ? e.Args[startIndex + 1] : null;

        if (!settings.FirstRunDone)
        {
            FirstRunWindow.Show(null);
            settings.FirstRunDone = true;
            settings.Save();
            startInTray = false;
        }

        _main = new MainWindow(settings);
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
        if (settings.CheckForUpdates) _ = Updater.CheckAsync(_main.ViewModel.Status);

        if (startProfile is not null)
        {
            if (_main.ViewModel.SelectProfile(startProfile))
            {
                _tray!.ShowBalloonTip(4000, "WoWBoxer", $"Starting \"{startProfile}\". Press Play in Battle.net for each account.", Forms.ToolTipIcon.Info);
                _ = _main.ViewModel.StartSquadAsync();
            }
            else
            {
                ShowMain();
                _main.ViewModel.Status.Warn($"No layout called \"{startProfile}\".");
            }
        }
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMain());
        menu.Items.Add("Start squad", null, (_, _) => Run(_main!.ViewModel.StartSquadCommand));
        menu.Items.Add("Arrange now", null, (_, _) => Run(_main!.ViewModel.ArrangeCommand));
        menu.Items.Add("Identify windows", null, (_, _) => Run(_main!.ViewModel.IdentifyCommand));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Shutdown());

        using var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _tray = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream),
            Text = "WoWBoxer Window Layouts",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowMain();
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
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _showSignal?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
