using System.IO;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// Normal app or "WoWBoxer Dev" – a local test build made by tools/dev-build.ps1 (MSBuild property WbxDev=true). The dev
/// build installs next to the normal app (own Velopack package id WoWBoxer.Dev, own install folder and shortcuts), keeps
/// its own settings, layouts and logs (%APPDATA%\WoWBoxer\Dev, %LOCALAPPDATA%\WoWBoxer\Dev), has its own single-instance
/// lock, a DEV badge in the title bar and a teal icon, and never checks for updates (a newer dev build is installed by
/// running the script again).
/// </summary>
internal static class AppFlavor
{
#if WBX_DEV
    public static readonly bool IsDev = true;
#else
    public static readonly bool IsDev = false;
#endif

    /// <summary>The name shown in the title, the tray and the Start menu.</summary>
    public static string Name => IsDev ? "WoWBoxer Dev" : "WoWBoxer";

    /// <summary>The folder under %APPDATA%\WoWBoxer and %LOCALAPPDATA%\WoWBoxer ("Layouts" is the normal app's, since 0.1).</summary>
    public static string DataFolder => IsDev ? "Dev" : "Layouts";

    private const string StableInstance = "WoWBoxer.Layouts.SingleInstance";
    private const string DevInstance = "WoWBoxer.Dev.SingleInstance";

    public static string InstanceName => IsDev ? DevInstance : StableInstance;

    /// <summary>Is the other flavour (normal app vs dev build) open right now? Both would place the same WoW windows.</summary>
    public static bool OtherIsRunning()
    {
        try
        {
            if (!Mutex.TryOpenExisting(IsDev ? StableInstance : DevInstance, out var other)) return false;
            other.Dispose();
            return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return true;   // exists, just not ours to open
        }
    }
}
