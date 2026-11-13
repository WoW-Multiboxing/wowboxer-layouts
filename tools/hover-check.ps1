# Hover-to-activate check (#help 2026-11-08, Warfen: after Re-arrange, hovering main -> follower stopped activating the
# follower; follower -> main still worked). Two FAKE WoW windows (tools\fakewow) overlap like Overlays / Main + side;
# the cursor is moved between them and the active window is read. Then the same Win32 calls Re-arrange makes are done
# (move/resize with SWP_NOACTIVATE|SWP_ASYNCWINDOWPOS|SWP_FRAMECHANGED, borderless restyle, "on top" set again) and the
# hover is checked again. Variants: follower on top on/off, Windows' "bring window to front" on/off.
# Moves the mouse cursor: run only when the PC is idle (default: refuses unless idle >= 180 s) and no WoW client runs.
# Restores the cursor position and the hover settings (Windows' ACTIVEWINDOWTRACKING / -TIMEOUT / -ZORDER) at the end.
# Sends no keys and no clicks to any window.
param([int]$Rounds = 6, [int]$MinIdle = 180, [switch]$Force)
$ErrorActionPreference = 'Stop'
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class HoverCheck {
  [StructLayout(LayoutKind.Sequential)] public struct L { public uint s; public uint t; }
  [StructLayout(LayoutKind.Sequential)] public struct P { public int X, Y; }
  [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref L l);
  public static double Idle() { L l = new L(); l.s = 8; GetLastInputInfo(ref l); return (Environment.TickCount - (int)l.t) / 1000.0; }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out P p);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
  [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint p, ref int v, uint w);
  [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")] public static extern bool SpiSet(uint a, uint p, IntPtr v, uint w);
  public static int Get(uint a) { int v = 0; SystemParametersInfo(a, 0, ref v, 0); return v; }
  public static void Set(uint a, int v) { SpiSet(a, 0, (IntPtr)v, 2); }
}
"@
$SPI_GETAWT = 0x1000; $SPI_SETAWT = 0x1001; $SPI_GETZ = 0x100C; $SPI_SETZ = 0x100D; $SPI_GETTO = 0x2002; $SPI_SETTO = 0x2003
$NOACT_ASYNC = 0x0010 -bor 0x4000; $NOSIZE = 1; $NOMOVE = 2; $NOZORDER = 4; $FRAME = 0x20; $SHOW = 0x40
$HWND_TOPMOST = [IntPtr](-1); $HWND_NOTOPMOST = [IntPtr](-2)

if (@(Get-Process Wow* -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike 'WoWBoxer*' -and $_.Name -notlike '*Voice*' }).Count -gt 0) { throw 'A WoW client is running. Close it first (this check moves the mouse).' }
$idle = [HoverCheck]::Idle()
if (-not $Force -and $idle -lt $MinIdle) { throw "The PC was used $([int]$idle) s ago. Run when idle >= $MinIdle s (or -Force)." }

$fakeExe = Join-Path $env:TEMP 'wowboxer-hovercheck\FakeWow.exe'
New-Item -ItemType Directory -Force (Split-Path $fakeExe) | Out-Null
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe "/out:$fakeExe" "$PSScriptRoot\fakewow\FakeWow.cs" | Out-Null

$saved = @{ awt = [HoverCheck]::Get($SPI_GETAWT); z = [HoverCheck]::Get($SPI_GETZ); to = [HoverCheck]::Get($SPI_GETTO) }
$cursor = New-Object HoverCheck+P
[HoverCheck]::GetCursorPos([ref]$cursor) | Out-Null
$procs = @()
$results = @()
try {
    [HoverCheck]::Set($SPI_SETAWT, 1); [HoverCheck]::Set($SPI_SETTO, 30)
    $procs += Start-Process $fakeExe -ArgumentList '100 100 1400 900 HoverMain' -PassThru
    $procs += Start-Process $fakeExe -ArgumentList '1100 600 800 500 HoverFollower' -PassThru
    Start-Sleep 2
    $main = $procs[0].MainWindowHandle; $fol = $procs[1].MainWindowHandle
    foreach ($p in $procs) { $p.Refresh() }
    $main = $procs[0].MainWindowHandle; $fol = $procs[1].MainWindowHandle
    if ($main -eq 0 -or $fol -eq 0) { throw 'Fake windows did not open.' }

    function Hover($x, $y, $want) {
        [HoverCheck]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 25
        [HoverCheck]::SetCursorPos($x + 3, $y + 2) | Out-Null   # a real move inside the window
        Start-Sleep -Milliseconds 400
        return [HoverCheck]::GetForegroundWindow() -eq $want
    }
    function Pass($label) {
        $toF = 0; $toM = 0
        for ($i = 0; $i -lt $Rounds; $i++) {
            if (Hover 400 400 $main) { $toM++ }          # main only
            if (Hover 1500 850 $fol) { $toF++ }          # follower (over the main where they overlap)
        }
        $line = "{0,-46} main->follower {1}/{2}   follower->main {3}/{2}" -f $label, $toF, $Rounds, $toM
        Write-Host $line
        return [pscustomobject]@{ Case = $label; ToFollower = $toF; ToMain = $toM; Rounds = $Rounds }
    }
    function Rearrange([bool]$borderless, [bool]$folTop) {
        foreach ($h in @($main, $fol)) {
            $style = [HoverCheck]::GetWindowLong($h, -16)
            if ($borderless) { $style = ($style -band (-bnot 0x00CF0000)) -bor 0x80000000 -bor 0x10000000 -bor 0x00080000 }
            [HoverCheck]::SetWindowLong($h, -16, $style) | Out-Null
        }
        [HoverCheck]::SetWindowPos($main, 0, 0, 0, 1500, 1000, $NOACT_ASYNC -bor $FRAME -bor $SHOW -bor $NOZORDER) | Out-Null
        [HoverCheck]::SetWindowPos($fol, 0, 1100, 600, 800, 500, $NOACT_ASYNC -bor $FRAME -bor $SHOW -bor $NOZORDER) | Out-Null
        Start-Sleep -Milliseconds 300
        [HoverCheck]::SetWindowPos($fol, $(if ($folTop) { $HWND_TOPMOST } else { $HWND_NOTOPMOST }), 0, 0, 0, 0, $NOMOVE -bor $NOSIZE -bor $NOACT_ASYNC) | Out-Null
        Start-Sleep -Milliseconds 300
    }

    foreach ($z in 0, 1) {
        [HoverCheck]::Set($SPI_SETZ, $z)
        foreach ($top in $true, $false) {
            [HoverCheck]::SetWindowPos($fol, $(if ($top) { $HWND_TOPMOST } else { $HWND_NOTOPMOST }), 0, 0, 0, 0, $NOMOVE -bor $NOSIZE -bor $NOACT_ASYNC) | Out-Null
            Start-Sleep -Milliseconds 300
            $tag = "front=$z top=$top"
            $results += Pass "$tag  before"
            Rearrange $false $top; $results += Pass "$tag  after re-arrange"
            Rearrange $true $top;  $results += Pass "$tag  after borderless re-arrange"
        }
    }
}
finally {
    foreach ($p in $procs) { if (-not $p.HasExited) { $p.Kill() } }
    [HoverCheck]::Set($SPI_SETAWT, $saved.awt); [HoverCheck]::Set($SPI_SETZ, $saved.z); [HoverCheck]::Set($SPI_SETTO, $saved.to)
    [HoverCheck]::SetCursorPos($cursor.X, $cursor.Y) | Out-Null
}
$bad = @($results | Where-Object { $_.ToFollower -lt $_.Rounds -or $_.ToMain -lt $_.Rounds })
if ($bad.Count -eq 0) { Write-Host "PASS: hover activated both ways in every case." } else { Write-Host "FAIL: $($bad.Count) case(s) lost hover activation."; exit 1 }
