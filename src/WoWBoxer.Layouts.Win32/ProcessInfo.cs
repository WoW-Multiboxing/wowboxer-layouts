using System.Diagnostics;
using static WoWBoxer.Layouts.Win32.NativeMethods;

namespace WoWBoxer.Layouts.Win32;

public enum Elevation { Normal, Elevated, Unknown }

/// <summary>Process facts that work even for elevated processes (PROCESS_QUERY_LIMITED_INFORMATION).</summary>
public static unsafe class ProcessInfo
{
    public static string Name(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return "?";
        }
    }

    public static string ImagePath(uint pid) => WithProcess(pid, "", h =>
    {
        var buffer = stackalloc char[1024];
        var size = 1024;
        return QueryFullProcessImageName(h, 0, buffer, ref size) ? new string(buffer, 0, size) : "";
    });

    /// <summary>Process start time (UTC), used for "oldest window first". Null if Windows does not tell us.</summary>
    public static DateTime? StartTime(uint pid) => WithProcess<DateTime?>(pid, null, h =>
        GetProcessTimes(h, out var created, out _, out _, out _) ? DateTime.FromFileTimeUtc(created) : null);

    /// <summary>Unknown usually means the process runs at a higher integrity level than we do (administrator).</summary>
    public static Elevation ElevationOf(uint pid) => WithProcess(pid, Elevation.Unknown, TokenElevationOf);

    public static Elevation CurrentElevation() => TokenElevationOf(GetCurrentProcess());

    private static Elevation TokenElevationOf(nint process)
    {
        if (!OpenProcessToken(process, TOKEN_QUERY, out var token)) return Elevation.Unknown;
        try
        {
            if (!GetTokenInformation(token, TokenElevation, out var elevated, sizeof(int), out _)) return Elevation.Unknown;
            return elevated != 0 ? Elevation.Elevated : Elevation.Normal;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static T WithProcess<T>(uint pid, T fallback, Func<nint, T> action)
    {
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == 0) return fallback;
        try
        {
            return action(h);
        }
        finally
        {
            CloseHandle(h);
        }
    }
}
