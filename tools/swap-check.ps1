# RUN IT, NEVER DOT-SOURCE IT:  powershell -ExecutionPolicy Bypass -File tools\swap-check.ps1 [-Keys] [-Rounds 60]
# Window swap, end to end, with the WoWBoxer Dev build (its own settings – the normal app's are never touched) and three
# FAKE WoW clients (tools\fakewow, a scratch "World of Warcraft\_classic_era_" folder in %TEMP%).
#
#  1. Main + side, then Overlays: many random swaps in a row ("--swap next|previous|home|N", the same code path as the
#     hotkeys). After each one every window must sit exactly in the slot the model says, "always on top" must belong to
#     the slot, and the active (foreground) window must not change.
#  2. The same with Windows' hover-to-activate on and off (the user's own values are put back at the end).
#  3. -Keys: the real hotkeys. A fake window is made the active window and Ctrl+Alt+Shift+Right / +3 / +Backspace are
#     pressed (SendInput, by this test script only): the swaps must happen and the fake's key log must not contain the
#     main key (the modifier keys themselves reach the active window, as with any key combination).
#     Only while nobody uses the PC (no input for 2 minutes), because it presses keys.
#
# Safety: stops if a real WoW client or the normal WoWBoxer is running (either would mix in). Restores the Dev
# settings and layouts and the hover setting afterwards, also on failure.
param(
    [string]$Exe = (Join-Path $env:LOCALAPPDATA 'WoWBoxer.Dev\current\WoWBoxer.exe'),
    [string]$Wbx = (Join-Path $PSScriptRoot '..\src\WoWBoxer.Layouts.Cli\bin\Release\net10.0-windows\wbx.exe'),
    [int]$Rounds = 60,
    [switch]$Keys
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { throw "WoWBoxer Dev not found ($Exe). Install it: tools\dev-build.ps1 -Install" }
if (-not (Test-Path $Wbx)) { throw "wbx not found ($Wbx). Build first: dotnet build -c Release" }

$S = Join-Path $env:TEMP 'wowboxer-swapcheck'
$fakeExe = "$S\World of Warcraft\_classic_era_\WowClassic.exe"
New-Item -ItemType Directory -Force (Split-Path $fakeExe) | Out-Null
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe "/out:$fakeExe" "$PSScriptRoot\fakewow\FakeWow.cs"
if ($LASTEXITCODE -ne 0) { throw 'Could not build the fake WoW client.' }

Add-Type @'
using System; using System.Runtime.InteropServices;
public static class SW {
  public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int n);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LII l);
  [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint p, ref int v, uint f);
  [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint p, IntPtr v, uint f);
  [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] i, int size);
  [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
  struct LII { public uint cb; public uint t; }
  [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public KEYBDINPUT k; [FieldOffset(0)] public MOUSEINPUT m; }
  [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION u; }
  public static string Rect(IntPtr h) { RECT r; if (DwmGetWindowAttribute(h, 9, out r, 16) != 0) GetWindowRect(h, out r); return r.L + "," + r.T + " " + (r.R - r.L) + "x" + (r.B - r.T); }
  public static bool Top(IntPtr h) { return (GetWindowLong(h, -20) & 8) != 0; }
  public static uint IdleSeconds() { var l = new LII(); l.cb = (uint)Marshal.SizeOf(l); GetLastInputInfo(ref l); return ((uint)Environment.TickCount - l.t) / 1000; }
  // hover to activate: SPI_GET/SETACTIVEWINDOWTRACKING 0x1000/0x1001, TIMEOUT 0x2002/0x2003, ZORDER 0x100C/0x100D
  public static int[] Hover() { int a = 0, t = 0, z = 0; SystemParametersInfo(0x1000, 0, ref a, 0); SystemParametersInfo(0x2002, 0, ref t, 0); SystemParametersInfo(0x100C, 0, ref z, 0); return new[] { a, t, z }; }
  public static void SetHover(int[] v) { SystemParametersInfo(0x1001, 0, (IntPtr)v[0], 3); SystemParametersInfo(0x2003, 0, (IntPtr)v[1], 3); SystemParametersInfo(0x100D, 0, (IntPtr)v[2], 3); }
  // the test presses a key combination like a user would (the app under test never does this)
  public static void Press(ushort[] mods, ushort key) {
    var list = new System.Collections.Generic.List<INPUT>();
    foreach (var m in mods) list.Add(K(m, 0));
    list.Add(K(key, 0)); list.Add(K(key, 2));
    for (int i = mods.Length - 1; i >= 0; i--) list.Add(K(mods[i], 2));
    SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(INPUT)));
  }
  static INPUT K(ushort vk, uint flags) { var i = new INPUT(); i.type = 1; i.u.k.vk = vk; i.u.k.flags = flags | ((vk >= 0x21 && vk <= 0x2E) ? 1u : 0u); return i; }
}
'@

$results = New-Object System.Collections.Generic.List[string]; $script:fails = 0
function Check($what, [bool]$ok) { if ($ok) { $results.Add("PASS  $what") } else { $results.Add("FAIL  $what"); $script:fails++ } }

# ---- safety ----
$realWow = @(Get-Process Wow, WowClassic, WowT, WowB -ErrorAction SilentlyContinue | Where-Object { $_.Path -notlike "$S*" })
if ($realWow.Count -gt 0) { throw "A real WoW client is running ($($realWow[0].Path)). Close it first – the test must not touch it." }
$normal = @(Get-Process WoWBoxer -ErrorAction SilentlyContinue | Where-Object { $_.Path -notlike "$env:LOCALAPPDATA\WoWBoxer.Dev\*" })
if ($normal.Count -gt 0) { throw 'The normal WoWBoxer is open. It would place the fake windows too – quit it (tray › Quit) first.' }

$devRoaming = Join-Path $env:APPDATA 'WoWBoxer\Dev'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = "$S\dev-backup-$stamp"
$hoverBefore = [SW]::Hover()
$fakes = @()
function DevApp { @(Get-Process WoWBoxer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$env:LOCALAPPDATA\WoWBoxer.Dev\*" -or $_.Path -eq $Exe }) }
function StopDev { DevApp | ForEach-Object { Stop-Process $_ -Force }; Start-Sleep -Milliseconds 800 }
function Send($a) { Start-Process $Exe -ArgumentList $a -Wait; Start-Sleep -Milliseconds 900 }

try {
    StopDev
    if (Test-Path $devRoaming) { Copy-Item $devRoaming $backup -Recurse }
    Remove-Item $devRoaming -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$devRoaming\profiles" | Out-Null

    # two test layouts (3 clients on the primary monitor) made by wbx, the same presets the app uses
    foreach ($p in @(@{ Name = 'Swap test side'; Preset = 'side' }, @{ Name = 'Swap test overlay'; Preset = 'overlay' })) {
        $json = & $Wbx layout --preset $p.Preset --clients 3 --json | Out-String
        $o = $json | ConvertFrom-Json
        $o.name = $p.Name
        $o | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 "$devRoaming\profiles\$($p.Name).json"
    }
    $version = ((Get-Item $Exe).VersionInfo.ProductVersion -split '\+')[0]
    @{
        firstRunDone = $true; lastSeenVersion = $version; lastProfile = 'Swap test side'; checkForUpdates = $false
        startPlacingOnOpen = $true; renameWindows = $false; autoFitActionBar = $false; gameExe = $fakeExe
        turnHoverOffOnExit = $false; restoreHoverOnExit = $false; swapHotkeys = $true
    } | ConvertTo-Json | Set-Content -Encoding utf8 "$devRoaming\settings.json"

    # three fake clients, oldest first = clients 1, 2, 3
    for ($i = 1; $i -le 3; $i++) {
        Remove-Item "$S\keys-$i.log" -ErrorAction SilentlyContinue
        $fakes += Start-Process $fakeExe -ArgumentList (200 + 60 * $i), (150 + 40 * $i), 700, 500, "`"Fake $i`"", "`"$S\keys-$i.log`"" -PassThru
        Start-Sleep -Milliseconds 700
    }
    $h = @($fakes | ForEach-Object { $_.Refresh(); $_.MainWindowHandle })
    Start-Process $Exe -ArgumentList '--tray', '--resume' | Out-Null
    Start-Sleep 7

    function RunLayout($name, $hover) {
        $script:homeRect = @($h | ForEach-Object { [SW]::Rect($_) })
        $script:homeTop = @($h | ForEach-Object { [SW]::Top($_) })
        Check "$name / hover $hover`: three different home places ($($homeRect -join ' | '))" (($homeRect | Select-Object -Unique).Count -eq 3)
        $pos = @(0, 1, 2)
        $fg = [SW]::GetForegroundWindow()
        $rnd = New-Object System.Random 11
        $actions = @('next', 'previous', '1', '2', '3', 'home')
        $bad = 0; $topBad = 0; $fgBad = 0
        for ($r = 0; $r -lt $Rounds; $r++) {
            $a = $actions[$rnd.Next($actions.Count)]
            $inMain = [array]::IndexOf($pos, 0)
            $c = switch ($a) { 'next' { ($inMain + 1) % 3 } 'previous' { ($inMain + 2) % 3 } 'home' { -1 } default { [int]$a - 1 } }
            if ($c -lt 0) { $pos = @(0, 1, 2) } elseif ($c -ne $inMain) { $pos[$inMain] = $pos[$c]; $pos[$c] = 0 }
            Send @('--swap', $a)
            for ($k = 0; $k -lt 3; $k++) {
                if ([SW]::Rect($h[$k]) -ne $homeRect[$pos[$k]]) { $bad++; $results.Add("      round $r ($a): client $($k + 1) at $([SW]::Rect($h[$k])), expected $($homeRect[$pos[$k]])") }
                if ([SW]::Top($h[$k]) -ne $homeTop[$pos[$k]]) { $topBad++ }
            }
            if ([SW]::GetForegroundWindow() -ne $fg) { $fgBad++ }
        }
        Check "$name / hover $hover`: $Rounds swaps in a row, every window exactly in its slot" ($bad -eq 0)
        Check "$name / hover $hover`: 'always on top' follows the slot every time" ($topBad -eq 0)
        Check "$name / hover $hover`: the active window never changed" ($fgBad -eq 0)
        Send @('--swap', 'home')
        Check "$name / hover $hover`: back to home layout" ((0..2 | Where-Object { [SW]::Rect($h[$_]) -ne $homeRect[$_] }).Count -eq 0)
    }

    foreach ($hover in @('off', 'on')) {
        [SW]::SetHover($(if ($hover -eq 'on') { @(1, 30, 0) } else { @(0, $hoverBefore[1], $hoverBefore[2]) }))
        RunLayout 'Main + side' $hover
    }
    Send @('--start', '"Swap test overlay"')
    Start-Sleep 4
    foreach ($hover in @('off', 'on')) {
        [SW]::SetHover($(if ($hover -eq 'on') { @(1, 30, 0) } else { @(0, $hoverBefore[1], $hoverBefore[2]) }))
        RunLayout 'Overlays' $hover
    }
    [SW]::SetHover($hoverBefore)

    # ---- the real hotkeys ----
    if ($Keys) {
        $idle = [SW]::IdleSeconds()
        if ($idle -lt 120) {
            $results.Add("SKIP  real hotkeys: the PC was used $idle s ago (needs 2 minutes without input)")
        } else {
            $before = [SW]::GetForegroundWindow()
            [void][SW]::SetForegroundWindow($h[1])   # fake 2 plays the active game window
            Start-Sleep -Milliseconds 400
            $active = [SW]::GetForegroundWindow() -eq $h[1]
            Check 'hotkeys: fake 2 is the active window for the key test' $active
            $homeRect = @($h | ForEach-Object { [SW]::Rect($_) })
            $mods = [uint16[]](0x11, 0x12, 0x10)   # Ctrl, Alt, Shift
            [SW]::Press($mods, 0x27); Start-Sleep -Milliseconds 900   # Right: next (2 into main)
            Check 'hotkeys: Ctrl+Alt+Shift+Right moved client 2 into the main slot' ([SW]::Rect($h[1]) -eq $homeRect[0])
            [SW]::Press($mods, 0x33); Start-Sleep -Milliseconds 900   # 3: client 3 into main
            Check 'hotkeys: Ctrl+Alt+Shift+3 moved client 3 into the main slot' ([SW]::Rect($h[2]) -eq $homeRect[0])
            [SW]::Press($mods, 0x08); Start-Sleep -Milliseconds 900   # Backspace: home
            Check 'hotkeys: Ctrl+Alt+Shift+Backspace put everyone home' ((0..2 | Where-Object { [SW]::Rect($h[$_]) -ne $homeRect[$_] }).Count -eq 0)
            Check 'hotkeys: the active window stayed fake 2' ([SW]::GetForegroundWindow() -eq $h[1])
            $log = if (Test-Path "$S\keys-2.log") { @(Get-Content "$S\keys-2.log") } else { @() }
            $main = @($log | Where-Object { $_ -match ' (27|33|08)$' })
            Check "hotkeys: the active fake got none of the main keys (its log: $(@($log | Sort-Object -Unique) -join ', '))" ($main.Count -eq 0)
            if ($before -ne [IntPtr]::Zero) { [void][SW]::SetForegroundWindow($before) }
        }
    }
} finally {
    [SW]::SetHover($hoverBefore)
    StopDev
    $fakes | ForEach-Object { if (-not $_.HasExited) { Stop-Process $_ -Force -ErrorAction SilentlyContinue } }
    Remove-Item $devRoaming -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $backup) { Copy-Item $backup $devRoaming -Recurse; Remove-Item $backup -Recurse -Force }
    $after = [SW]::Hover()
    $results.Add($(if (($after -join ',') -eq ($hoverBefore -join ',')) { "OK    hover setting back as it was ($($after -join ','))" } else { "WARN  hover setting differs: before $($hoverBefore -join ','), now $($after -join ',')" }))
}
$results | ForEach-Object { Write-Host $_ }
Write-Host ("{0} checks failed" -f $script:fails)
exit $script:fails
