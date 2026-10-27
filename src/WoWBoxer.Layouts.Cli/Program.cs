using System.Globalization;
using WoWBoxer.Layouts.Cli;
using WoWBoxer.Layouts.Win32;

// Same number format on every PC, so reports read the same ("1.25", not "1,25").
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

CliOptions options;
try
{
    options = CliOptions.Parse(args);
}
catch (UsageException e)
{
    Console.Error.WriteLine("wbx: " + e.Message);
    Console.Error.WriteLine("Run \"wbx help\" for the commands and options.");
    return 2;
}

if (options.Command is "help" or "-h" or "--help")
{
    Console.WriteLine(CliOptions.Usage);
    return 0;
}
if (options.Command is "version" or "--version")
{
    Console.WriteLine(Commands.Version);
    return 0;
}

// Physical pixels on every monitor, before any window or monitor call.
var dpiAwareness = Dpi.EnsurePerMonitorV2();
var isAdmin = ProcessInfo.CurrentElevation() == Elevation.Elevated;
var logName = options.Command == "diagnose" ? $"diagnose-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
    : isAdmin ? "last-run-admin.log" : "last-run.log";

using var log = new Log(logName);
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;   // let "start" finish its summary
    cancel.Cancel();
};

if (!options.Json)   // --json output stays clean for piping into a file
{
    log.Line();
    log.Line($"WoWBoxer Window Layouts {Commands.Version} - {options.Command}", ConsoleColor.Yellow);
    log.Line("-----------------------------------------", ConsoleColor.DarkYellow);
}

try
{
    return options.Command switch
    {
        "arrange" => await Commands.ArrangeAsync(options, log),
        "start" => await Commands.StartAsync(options, log, cancel.Token),
        "diagnose" => await Commands.DiagnoseAsync(options, log, dpiAwareness),
        "info" => Commands.Info(options, log),
        "layout" => Commands.Layout(options, log),
        _ => throw new UsageException($"Unknown command \"{options.Command}\"."),
    };
}
catch (UsageException e)
{
    log.Error(e.Message);
    log.Info("Run \"wbx help\" for the commands and options.");
    return 2;
}
catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
{
    log.Error(e.Message);
    return 1;
}
catch (Exception e)
{
    log.Error("ERROR: " + e.Message);
    log.Detail(e.ToString());
    return 1;
}
