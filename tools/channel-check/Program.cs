using Velopack.Locators;
using WoWBoxer.Layouts.App;
using WoWBoxer.Layouts.Core;

// channel-check --installed 0.6.0 [--installed-from win|beta] [--setting Stable|Beta] [--expect 0.6.1-beta.1|none]
// Pretends WoWBoxer <installed> is installed (from the given Velopack channel, with the given Settings › Updates choice)
// and asks the real GitHub releases what the app would offer. With --expect it fails (exit 1) on any other answer.
string? Arg(string name)
{
    var i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var installed = Arg("--installed") ?? throw new ArgumentException("--installed VERSION is required");
var feed = Arg("--installed-from") ?? (UpdateChannels.IsPrerelease(installed) ? UpdateChannels.BetaFeed : UpdateChannels.StableFeed);
var setting = Arg("--setting");
var expect = Arg("--expect");
var channel = UpdateChannels.Effective(setting, feed);

var packages = Directory.CreateTempSubdirectory("wbx-channel-check-");
try
{
    var locator = new TestVelopackLocator("WoWBoxer.Layouts", installed, packages.FullName, null, null, null, feed);
    var found = await UpdateFinder.FindAsync(channel, locator);
    var offered = found?.Update.TargetFullRelease.Version.ToString() ?? "none";
    Console.WriteLine($"installed {installed} (from '{feed}'), setting {setting ?? "(none)"} -> channel {channel} -> offered: {offered}");
    if (expect is not null && !string.Equals(expect, offered, StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"  FAIL: expected {expect}");
        return 1;
    }
    return 0;
}
finally
{
    packages.Delete(recursive: true);
}
