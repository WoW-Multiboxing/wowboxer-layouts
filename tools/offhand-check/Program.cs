using System.Diagnostics;
using WoWBoxer.Layouts.Tools;
using WoWBoxer.Layouts.Core;
using WoWBoxer.Layouts.Win32;

// Run on a Windows desktop: dotnet run --project tools/offhand-check -c Release.
// Two stand-ins have the same exe and title. Only the second should span and restore.
try
{
    await RunAsync();
    return 0;
}
catch (Exception e)
{
    SmokeFailure.Report("Offhand window check", e);
    return 1;
}

static async Task RunAsync()
{
    Dpi.EnsurePerMonitorV2();
    var area = Monitors.Primary(MonitorReader.GetMonitors()).WorkArea;
    if (WindowFinder.FindGameWindows().Count != 0)
        throw new InvalidOperationException("Close the existing WoW clients before running this check.");

    var temp = Directory.CreateTempSubdirectory("wowboxer-offhand-");
    var children = new List<Process>();
    bool verified = false;
    try
    {
        var install = temp.CreateSubdirectory("_forever_");
        var exe = Path.Combine(install.FullName, "Wow.exe");
        var compiler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
        var compile = new ProcessStartInfo(compiler) { UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var arg in new[] { "/nologo", "/target:winexe", "/reference:System.Drawing.dll", "/reference:System.Windows.Forms.dll",
            "/out:" + exe, Path.Combine(AppContext.BaseDirectory, "FakeWow.cs") }) compile.ArgumentList.Add(arg);
        using var build = Process.Start(compile) ?? throw new InvalidOperationException("Could not start the .NET Framework compiler.");
        var diagnostics = await build.StandardOutput.ReadToEndAsync();
        await build.WaitForExitAsync();
        if (build.ExitCode != 0) throw new InvalidOperationException($"FakeWow compilation failed ({build.ExitCode}): {diagnostics.Trim()}");

        var first = LaunchClient(20);
        var untouched = await WaitForClient(first);
        var second = LaunchClient(120);
        var selected = await WaitForClient(second);

        Console.WriteLine($"Discovered both fake windows, PIDs {first.Id} and {second.Id}.");
        var firstState = ReadState(untouched.Handle);
        var secondState = ReadState(selected.Handle);
        var service = new OffhandWindow();
        // A single test monitor suffices to verify native placement. The layout tests cover monitor selection.
        try
        {
            await service.ExpandAsync(selected, area);
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidOperationException($"Expand failed for PID {selected.Pid}: original {secondState}, requested {area}, " +
                $"after attempt {ReadState(selected.Handle)}, client size {WindowFinder.ClientSizeOf(selected.Handle)}.", error);
        }
        Check(service.ActiveWindow?.Handle == selected.Handle, "the selected handle owns the span");
        Check(WindowFinder.RectOf(selected.Handle) == area, "the selected window fills the target rectangle");
        Check(WindowFinder.ClientSizeOf(selected.Handle) == (area.Width, area.Height), "the game area fills the borderless window");
        Check(ReadState(untouched.Handle) == firstState, "the first window keeps its bounds, style and topmost state");

        await service.RestoreAsync();
        Check(service.ActiveWindow == null, "the span ends");
        Check(ReadState(selected.Handle) == secondState, "the second window regains its original bounds, style and topmost state");
        Check(ReadState(untouched.Handle) == firstState, "restoring leaves the first window alone");
        verified = true;

        Process LaunchClient(int offset)
        {
            var p = Start(exe, (area.X + offset).ToString(), (area.Y + 40).ToString(), "460", "320", "WoW Forever test");
            children.Add(p);
            return p;
        }
    }
    finally
    {
        try
        {
            await CleanUpAsync(children, temp);
        }
        catch (Exception e) when (!verified)
        {
            // Preserve the placement failure already on its way to the caller.
            SmokeFailure.Report("Stand-in cleanup also failed", e);
        }
    }
}

static async Task CleanUpAsync(List<Process> children, DirectoryInfo temp)
{
    List<Exception> failures = [];
    foreach (var child in children)
    {
        using (child)
        {
            try
            {
                if (!child.HasExited && child.CloseMainWindow())
                    child.WaitForExit(2000);
                if (!child.HasExited) child.Kill();
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception e)
            {
                failures.Add(e);
            }
        }
    }

    // Windows can briefly hold the executable's image section after the process signals exit.
    var timer = Stopwatch.StartNew();
    while (true)
    {
        try
        {
            temp.Delete(recursive: true);
            break;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (timer.Elapsed >= TimeSpan.FromSeconds(10))
            {
                failures.Add(e);
                break;
            }
            await Task.Delay(250);
        }
    }
    if (failures.Count > 0)
        throw new AggregateException($"Could not fully clean up the stand-ins under {temp.FullName}.", failures);
}

static Process Start(string file, params string[] arguments)
{
    var info = new ProcessStartInfo(file) { UseShellExecute = false };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    return Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
}

static async Task<WindowInfo> WaitForClient(Process process)
{
    var timer = Stopwatch.StartNew();
    while (timer.Elapsed < TimeSpan.FromSeconds(10) && !process.HasExited)
    {
        var client = WindowFinder.FindGameWindows(WowGame.Forever).SingleOrDefault(w => w.Pid == process.Id);
        if (client != null) return client;
        await Task.Delay(100);
    }
    throw new InvalidOperationException(process.HasExited
        ? $"Stand-in PID {process.Id} exited with code {process.ExitCode} before its window could be found."
        : $"PID {process.Id} did not produce a visible Forever window within ten seconds.");
}

static (PixelRect Rect, int Style, bool Topmost) ReadState(nint handle) =>
    (WindowFinder.RectOf(handle), WindowFinder.Describe(handle).Style, WindowPlacer.IsTopmost(handle));

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"PASS  {description}");
}
