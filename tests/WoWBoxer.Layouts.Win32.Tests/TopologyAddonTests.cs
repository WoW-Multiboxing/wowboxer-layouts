using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32.Tests;

/// <summary>The WoWBoxerTopology data add-on in a pretend "World of Warcraft" folder (never the real one).</summary>
[Collection("TopologyAddon")]
public sealed class TopologyAddonTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wbx-topology-" + Guid.NewGuid().ToString("N"), "World of Warcraft");

    public TopologyAddonTests()
    {
        foreach (var (folder, exe) in new[] { ("_classic_era_", "WowClassic.exe"), ("_forever_", "Wow.exe") })
        {
            Directory.CreateDirectory(Path.Combine(_root, folder));
            File.WriteAllText(Path.Combine(_root, folder, exe), "");
        }
        Directory.CreateDirectory(Path.Combine(_root, "_retail_"));   // no game exe: not an installed client
        TopologyAddon.Reset();
    }

    public void Dispose()
    {
        TopologyAddon.Reset();
        try { Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true); } catch (IOException) { }
    }

    private static readonly Topology Sample = new(4480, 1440, new PixelRect(2560, 0, 1920, 1080));

    [Fact]
    public void Writes_into_every_installed_client_then_removes_its_own()
    {
        var r = TopologyAddon.Sync(Sample, roots: [_root]);
        Assert.Equal(2, r.Written.Count);
        var lua = Path.Combine(TopologyAddon.FolderIn(Path.Combine(_root, "_forever_")), "WoWBoxerTopology.lua");
        Assert.Equal(Sample.LuaText(), File.ReadAllText(lua));
        Assert.False(Directory.Exists(TopologyAddon.FolderIn(Path.Combine(_root, "_retail_"))));

        Assert.False(TopologyAddon.Sync(Sample, roots: [_root]).Changed);   // same state: nothing done

        var removed = TopologyAddon.Sync(null, roots: [_root]);
        Assert.Equal(2, removed.Removed.Count);
        Assert.False(File.Exists(lua));
    }

    [Fact]
    public void Never_touches_a_folder_it_did_not_write()
    {
        var foreign = TopologyAddon.FolderIn(Path.Combine(_root, "_forever_"));
        Directory.CreateDirectory(foreign);
        File.WriteAllText(Path.Combine(foreign, "WoWBoxerTopology.toc"), "## Title: someone else's\r\n");

        var r = TopologyAddon.Sync(Sample, roots: [_root]);
        Assert.Single(r.Written);   // only Classic Era
        Assert.Equal("## Title: someone else's\r\n", File.ReadAllText(Path.Combine(foreign, "WoWBoxerTopology.toc")));
        TopologyAddon.Sync(null, roots: [_root]);
        Assert.True(Directory.Exists(foreign));
    }

    [Fact]
    public void Rewrites_when_the_numbers_change()
    {
        TopologyAddon.Sync(Sample, roots: [_root]);
        var moved = Sample with { Team = new PixelRect(-1920, 360, 1920, 1080) };
        Assert.Equal(2, TopologyAddon.Sync(moved, roots: [_root]).Written.Count);
        Assert.Contains("x = -1920", File.ReadAllText(Path.Combine(TopologyAddon.FolderIn(Path.Combine(_root, "_classic_era_")), "WoWBoxerTopology.lua")));
    }
}
