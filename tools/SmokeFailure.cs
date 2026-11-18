namespace WoWBoxer.Layouts.Tools;

internal static class SmokeFailure
{
    // The unauthenticated check-run API exposes annotations even when the full Actions log requires a login.
    public static void Report(string title, Exception error)
    {
        Console.Error.WriteLine(error);
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") return;
        string message = error.ToString().Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");
        Console.WriteLine($"::error title={title}::{message}");
    }
}
