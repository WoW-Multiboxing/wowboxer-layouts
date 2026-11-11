using System.Runtime.InteropServices;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.Win32;

/// <summary>
/// Windows' per-process volume mixer through Core Audio: every active output device → IAudioSessionManager2 → its audio
/// sessions → ISimpleAudioVolume. Reads and sets a session's level and mute – the same thing the Volume mixer in Windows
/// does. Nothing is sent to any program. <see cref="Changed"/> fires (on a Windows audio thread) when a session appears,
/// so a client that starts playing later is handled at once, not only at the next tick.
/// </summary>
public sealed class AudioMixer : IAudioMixer, IDisposable
{
    /// <summary>Marks our own changes (Windows passes it to every listener of the session).</summary>
    public static readonly Guid EventContext = new("6b0c3c1e-6a0e-4e55-9a2b-5f0d1b0a7c11");

    private readonly IMMDeviceEnumerator? _devices;
    private readonly Dictionary<string, Device> _open = [];
    private Dictionary<string, IAudioSessionControl2> _controls = [];
    private readonly SessionCreated _created;

    /// <summary>A new audio session appeared on one of the devices (raised on a Windows audio thread).</summary>
    public event Action? Changed;

    /// <summary>The mixer, or a silent stand-in while the test/demo sandbox runs or Core Audio is not there.</summary>
    public static IAudioMixer Create() => Sandbox.Active ? Sandbox.Audio : new AudioMixer();

    private AudioMixer()
    {
        _created = new SessionCreated(() => Changed?.Invoke());
        try
        {
            _devices = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        }
        catch (COMException)
        {
            _devices = null;
        }
    }

    public IReadOnlyList<AudioSession> Sessions()
    {
        var result = new List<AudioSession>();
        var controls = new Dictionary<string, IAudioSessionControl2>();
        if (_devices is not null)
        {
            foreach (var device in OpenDevices())
            {
                IAudioSessionEnumerator? list = null;
                try
                {
                    if (device.Manager.GetSessionEnumerator(out list) != 0 || list is null) continue;
                    list.GetCount(out var count);
                    for (var i = 0; i < count; i++)
                    {
                        if (list.GetSession(i, out var control) != 0 || control is null) continue;
                        if (control is not IAudioSessionControl2 c2)
                        {
                            Marshal.ReleaseComObject(control);
                            continue;
                        }
                        if (c2.GetState(out var state) != 0 || state == 2 /* expired */ || c2.GetProcessId(out var pid) != 0
                            || c2.GetSessionInstanceIdentifier(out var instance) != 0 || c2 is not ISimpleAudioVolume volume
                            || volume.GetMasterVolume(out var level) != 0 || volume.GetMute(out var muted) != 0)
                        {
                            Marshal.ReleaseComObject(c2);
                            continue;
                        }
                        var id = device.Id + "|" + instance;
                        if (!controls.TryAdd(id, c2))
                        {
                            Marshal.ReleaseComObject(c2);
                            continue;
                        }
                        result.Add(new AudioSession(id, pid, new AudioState(level, muted)));
                    }
                }
                catch (COMException)
                {
                    // the device went away while we read it – the next call sees the new list
                }
                catch (InvalidCastException)
                {
                }
                finally
                {
                    if (list is not null) Marshal.ReleaseComObject(list);
                }
            }
        }
        var old = _controls;
        _controls = controls;
        foreach (var c in old.Values) Marshal.ReleaseComObject(c);
        return result;
    }

    public bool Set(string id, AudioState state)
    {
        if (!_controls.TryGetValue(id, out var control) || control is not ISimpleAudioVolume volume) return false;
        try
        {
            var context = EventContext;
            var ok = volume.SetMasterVolume(Math.Clamp(state.Volume, 0f, 1f), ref context) == 0;
            context = EventContext;
            ok &= volume.SetMute(state.Muted, ref context) == 0;
            return ok;
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// <summary>The active output devices, opened once each (and dropped when they go away).</summary>
    private List<Device> OpenDevices()
    {
        var now = new List<Device>();
        IMMDeviceCollection? collection = null;
        try
        {
            if (_devices!.EnumAudioEndpoints(0 /* eRender */, 1 /* DEVICE_STATE_ACTIVE */, out collection) != 0 || collection is null) return now;
            collection.GetCount(out var count);
            var seen = new HashSet<string>();
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) != 0 || device is null) continue;
                try
                {
                    if (device.GetId(out var id) != 0 || id is null) continue;
                    seen.Add(id);
                    if (!_open.TryGetValue(id, out var open))
                    {
                        var iid = typeof(IAudioSessionManager2).GUID;
                        if (device.Activate(ref iid, 0x17 /* CLSCTX_ALL */, IntPtr.Zero, out var obj) != 0 || obj is not IAudioSessionManager2 manager) continue;
                        open = new Device(id, manager);
                        // notifications only start after the first GetSessionEnumerator (done right after, in Sessions)
                        try
                        {
                            manager.RegisterSessionNotification(_created);
                        }
                        catch (COMException)
                        {
                        }
                        _open[id] = open;
                    }
                    now.Add(open);
                }
                finally
                {
                    Marshal.ReleaseComObject(device);
                }
            }
            foreach (var gone in _open.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                Close(_open[gone]);
                _open.Remove(gone);
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            if (collection is not null) Marshal.ReleaseComObject(collection);
        }
        return now;
    }

    private void Close(Device d)
    {
        try
        {
            d.Manager.UnregisterSessionNotification(_created);
        }
        catch (COMException)
        {
        }
        Marshal.ReleaseComObject(d.Manager);
    }

    public void Dispose()
    {
        foreach (var c in _controls.Values) Marshal.ReleaseComObject(c);
        _controls.Clear();
        foreach (var d in _open.Values) Close(d);
        _open.Clear();
        if (_devices is not null) Marshal.ReleaseComObject(_devices);
    }

    private sealed record Device(string Id, IAudioSessionManager2 Manager);

    /// <summary>Called by Windows when a session appears (public: Windows calls it through COM).</summary>
    [ComVisible(true)]
    public sealed class SessionCreated(Action onCreated) : IAudioSessionNotification
    {
        public int OnSessionCreated(IAudioSessionControl newSession)
        {
            onCreated();
            return 0;
        }
    }

    // ---------- Core Audio (mmdeviceapi.h, audiopolicy.h) ----------

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorCom;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        // IAudioSessionManager
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
        // IAudioSessionManager2
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        [PreserveSig] int RegisterSessionNotification(IAudioSessionNotification notification);
        [PreserveSig] int UnregisterSessionNotification(IAudioSessionNotification notification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionControl
    {
    }

    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid context);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
        // IAudioSessionControl2
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference(bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionNotification
    {
        [PreserveSig] int OnSessionCreated(IAudioSessionControl newSession);
    }
}
