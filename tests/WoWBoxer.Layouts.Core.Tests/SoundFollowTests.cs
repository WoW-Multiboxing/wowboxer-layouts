namespace WoWBoxer.Layouts.Core.Tests;

/// <summary>"Sound follows the main window" (owner, 2026-11-10): the volume mapping, following swaps, and putting the user's own levels back.</summary>
public class SoundFollowTests
{
    private const string Exe = @"C:\Program Files (x86)\World of Warcraft\_forever_\Wow.exe";
    private static readonly Dictionary<uint, string> Clients = new() { [11] = Exe, [12] = Exe, [13] = Exe };

    private static (Mixer Mixer, SoundFollower Follower, Dictionary<string, AudioState> Originals) Squad(float level = 0.8f)
    {
        var mixer = new Mixer();
        foreach (var pid in Clients.Keys) mixer.Add(pid, new AudioState(level, false));
        mixer.Add(99, new AudioState(0.5f, false));   // Discord: never touched
        var originals = new Dictionary<string, AudioState>();
        return (mixer, new SoundFollower(originals), originals);
    }

    [Theory]
    [InlineData(true, 0, 0.8f, false)]
    [InlineData(false, 0, 0.8f, true)]      // muted, the level stays for later
    [InlineData(false, 25, 0.2f, false)]    // a quarter of its own level
    [InlineData(false, 100, 0.8f, false)]
    [InlineData(false, 150, 0.8f, false)]   // clamped
    public void Target_maps_the_main_and_the_followers(bool loud, int percent, float volume, bool muted)
    {
        var t = SoundFollower.Target(new AudioState(0.8f, false), loud, percent);
        Assert.Equal(volume, t.Volume, 3);
        Assert.Equal(muted, t.Muted);
    }

    [Fact]
    public void The_main_unmutes_even_when_the_user_had_muted_the_client()
    {
        Assert.False(SoundFollower.Target(new AudioState(0.6f, true), loud: true, 0).Muted);
    }

    [Fact]
    public void Only_the_main_client_plays_and_other_programs_keep_their_sound()
    {
        var (mixer, follower, originals) = Squad();

        follower.Update(mixer, 11, Clients, 0);

        Assert.Equal(new AudioState(0.8f, false), mixer.Of(11));
        Assert.True(mixer.Of(12).Muted);
        Assert.True(mixer.Of(13).Muted);
        Assert.Equal(new AudioState(0.5f, false), mixer.Of(99));
        Assert.Equal(new AudioState(0.8f, false), originals[SoundFollower.Key(Exe)]);
    }

    [Fact]
    public void Ten_swaps_always_leave_exactly_the_main_one_playing()
    {
        var (mixer, follower, _) = Squad();
        uint[] order = [12, 13, 11, 13, 12, 11, 12, 12, 13, 11];
        foreach (var main in order)
        {
            follower.Update(mixer, main, Clients, 0);
            Assert.Equal([main], Clients.Keys.Where(p => !mixer.Of(p).Muted));
            Assert.Equal(0.8f, mixer.Of(main).Volume, 3);
        }
    }

    [Fact]
    public void Nothing_is_written_again_while_nothing_changed_so_a_follower_unmuted_by_hand_stays_unmuted()
    {
        var (mixer, follower, _) = Squad();
        follower.Update(mixer, 11, Clients, 0);
        var writes = mixer.Writes;
        mixer.Put(12, new AudioState(0.8f, false));   // the user unmutes client 2 in Windows' mixer

        follower.Update(mixer, 11, Clients, 0);

        Assert.Equal(writes, mixer.Writes);
        Assert.False(mixer.Of(12).Muted);
    }

    [Fact]
    public void A_client_that_starts_playing_later_is_muted_at_once()
    {
        var (mixer, follower, _) = Squad();
        mixer.Remove(13);   // no sound played yet: not in the mixer
        follower.Update(mixer, 11, Clients, 0);

        mixer.Add(13, new AudioState(0.8f, false));
        follower.Update(mixer, 11, Clients, 0);

        Assert.True(mixer.Of(13).Muted);
    }

    [Fact]
    public void A_level_the_user_sets_on_the_playing_client_becomes_its_normal_level()
    {
        var (mixer, follower, originals) = Squad();
        follower.Update(mixer, 11, Clients, 0);
        mixer.Put(11, new AudioState(0.5f, false));

        follower.Update(mixer, 11, Clients, 0);
        follower.Update(mixer, 12, Clients, 0);   // swap

        Assert.Equal(0.5f, mixer.Of(12).Volume, 3);
        Assert.Equal(0.5f, originals[SoundFollower.Key(Exe)].Volume, 3);
    }

    [Fact]
    public void Restore_puts_every_client_back_and_forgets_the_original()
    {
        var (mixer, follower, originals) = Squad(0.7f);
        follower.Update(mixer, 11, Clients, 30);
        Assert.Equal(0.21f, mixer.Of(12).Volume, 3);

        var written = follower.Restore(mixer, pid => Clients.GetValueOrDefault(pid, @"C:\Discord\Discord.exe"));

        Assert.Equal(2, written);
        Assert.All(Clients.Keys, p => Assert.Equal(new AudioState(0.7f, false), mixer.Of(p)));
        Assert.Equal(new AudioState(0.5f, false), mixer.Of(99));
        Assert.Empty(originals);
    }

    [Fact]
    public void A_client_closed_while_muted_gets_its_level_back_when_it_plays_again()
    {
        // Windows remembers a program's mixer level per exe: the next client of that exe would start muted
        var (mixer, follower, originals) = Squad();
        follower.Update(mixer, 11, Clients, 0);
        foreach (var pid in Clients.Keys) mixer.Remove(pid);   // all clients closed, the app quits
        follower.Restore(mixer, _ => "");
        Assert.Single(originals);                            // nothing live to restore: kept for later

        var next = new SoundFollower(originals);              // next start, feature off
        mixer.Add(21, new AudioState(0.8f, true));
        var written = next.RestorePending(mixer, pid => pid == 21 ? Exe : "");

        Assert.Equal(1, written);
        Assert.Equal(new AudioState(0.8f, false), mixer.Of(21));
        Assert.Empty(originals);
    }

    [Fact]
    public void After_a_crash_the_saved_original_wins_over_the_muted_state_the_client_starts_with()
    {
        var originals = new Dictionary<string, AudioState> { [SoundFollower.Key(Exe)] = new(0.9f, false) };
        var follower = new SoundFollower(originals);
        var mixer = new Mixer();
        mixer.Add(11, new AudioState(0.9f, true));
        mixer.Add(12, new AudioState(0.9f, true));

        follower.Update(mixer, 12, new Dictionary<uint, string> { [11] = Exe, [12] = Exe }, 0);

        Assert.Equal(new AudioState(0.9f, false), mixer.Of(12));
        Assert.True(mixer.Of(11).Muted);
    }

    [Fact]
    public void The_settings_are_saved_when_a_new_original_is_kept()
    {
        var saves = 0;
        var follower = new SoundFollower([], () => saves++);
        var (mixer, _, _) = Squad();
        follower.Update(mixer, 11, Clients, 0);
        follower.Update(mixer, 12, Clients, 0);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void The_main_client_follows_swaps_and_canvas_swaps()
    {
        var monitors = new[] { new MonitorInfo(1, @"\\.\DISPLAY1", true, new PixelRect(0, 0, 2560, 1392), new PixelRect(0, 0, 2560, 1392)) };
        var home = Presets.Create(PresetKind.Side, 3, monitors);

        Assert.Equal(0, SoundFollower.MainClient(home, null));
        Assert.Equal(2, SoundFollower.MainClient(home, WindowSwap.ToMain(WindowSwap.Home(3), 2, WindowSwap.MainSlots(home)[0])));

        // canvas drag-swap: client 1 and 2 trade places in the home layout itself
        LayoutSlot a = home.Slots[0], b = home.Slots[1];
        var slots = home.Slots.ToList();
        slots[0] = a with { Monitor = b.Monitor, X = b.X, Y = b.Y, W = b.W, H = b.H };
        slots[1] = b with { Monitor = a.Monitor, X = a.X, Y = a.Y, W = a.W, H = a.H };
        Assert.Equal(1, SoundFollower.MainClient(home with { Slots = slots }, null));
    }

    [Fact]
    public void A_grid_has_no_main_client_unless_one_is_crowned()
    {
        var monitors = new[] { new MonitorInfo(1, @"\\.\DISPLAY1", true, new PixelRect(0, 0, 2560, 1392), new PixelRect(0, 0, 2560, 1392)) };
        var grid = Presets.Create(PresetKind.Grid, 4, monitors);

        Assert.Equal(-1, SoundFollower.MainClient(grid, null));
        Assert.Equal(3, SoundFollower.MainClient(grid with { SwapMainSlots = [4] }, null));
    }

    [Theory]
    [InlineData("", true, false)]
    [InlineData("SET Sound_EnableSoundWhenGameIsInBG \"1\"\n", true, true)]
    [InlineData("SET Sound_EnableSoundWhenGameIsInBg \"0\"\nSET Sound_EnableAllSound \"0\"\n", false, false)]
    public void Reads_sound_in_background_from_config_wtf(string config, bool all, bool background)
    {
        Assert.Equal(new WowSoundSetting(all, background), WowSound.Read(config));
    }

    [Fact]
    public void The_hint_says_what_to_turn_on_in_game()
    {
        Assert.Contains("Sound in Background", new WowSoundSetting(true, false).Hint(SoundFollowMode.MainSlot));
        Assert.Equal("", new WowSoundSetting(true, true).Hint(SoundFollowMode.MainSlot));
        Assert.Equal("", new WowSoundSetting(true, false).Hint(SoundFollowMode.ActiveWindow));
        Assert.Contains("Enable Sound", new WowSoundSetting(false, true).Hint(SoundFollowMode.MainSlot));
    }

    [Fact]
    public void Turning_sound_in_background_on_changes_only_that_line()
    {
        const string config = "SET gxWindowedResolution \"928x696\"\r\nSET Sound_EnableSoundWhenGameIsInBG \"0\"\r\nSET uiScale \"0.74\"\r\n";
        Assert.Equal(config.Replace("InBG \"0\"", "InBG \"1\""), WowSound.WithBackgroundSound(config));
        Assert.Equal("SET a \"1\"\r\nSET Sound_EnableSoundWhenGameIsInBG \"1\"\r\n", WowSound.WithBackgroundSound("SET a \"1\"\r\n"));
    }

    /// <summary>Windows' volume mixer in memory.</summary>
    private sealed class Mixer : IAudioMixer
    {
        private readonly List<AudioSession> _list = [];
        public int Writes { get; private set; }

        public void Add(uint pid, AudioState s) => _list.Add(new AudioSession($"dev|{pid}|{Guid.NewGuid()}", pid, s));
        public void Remove(uint pid) => _list.RemoveAll(s => s.Pid == pid);
        public void Put(uint pid, AudioState s) => _list[_list.FindIndex(x => x.Pid == pid)] = _list.First(x => x.Pid == pid) with { State = s };
        public AudioState Of(uint pid) => _list.First(s => s.Pid == pid).State;

        public IReadOnlyList<AudioSession> Sessions() => [.. _list];

        public bool Set(string id, AudioState state)
        {
            var i = _list.FindIndex(s => s.Id == id);
            if (i < 0) return false;
            _list[i] = _list[i] with { State = state };
            Writes++;
            return true;
        }
    }
}
