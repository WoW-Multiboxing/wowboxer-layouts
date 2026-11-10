<#
  RUN IT, NEVER DOT-SOURCE IT:  powershell -ExecutionPolicy Bypass -File tools\resize-probe.ps1 -Game forever|era [-Force]
  How does a WoW client react when its window is moved/resized from outside? (#bug 2026-11-10: Forever clients ended
  up at other sizes than placed and swapping made them go back and forth.)
  Starts ONE client of the chosen install directly (it opens at the login screen – nobody logs in), waits until it has
  loaded, then resizes it like WoWBoxer does (SWP_NOACTIVATE|SWP_ASYNCWINDOWPOS|SWP_FRAMECHANGED, title bar kept, by
  its visible edges) through a list of sizes, and samples window/visible/client rects every 10 ms for a few seconds
  after each step: a timeline of what the CLIENT does by itself. Ends with swap-like sequences (big→small→big quickly).
  Refuses to run while a client of that install is open, or when the PC was used less than -MinIdle seconds ago.
  Config.wtf of the install is copied first and put back byte for byte at the end (a client rewrites it on exit).
  The client is ended with Stop-Process. Nothing is sent to the game: no keys, no clicks, no window messages.
  Writes a report next to -Out (default: %TEMP%\wowboxer-resize-probe-<game>.txt).
#>
param(
    [ValidateSet('forever', 'era')][string]$Game = 'forever',
    [string]$Root = 'C:\Program Files (x86)\World of Warcraft',
    [int]$MinIdle = 180,
    [int]$SettleMs = 3000,
    [switch]$Force,
    [string]$Out = (Join-Path $env:TEMP "wowboxer-resize-probe-$Game.txt"),
    [string]$Wbx = (Join-Path $PSScriptRoot '..\src\WoWBoxer.Layouts.Cli\bin\Release\net10.0-windows\wbx.exe')
)
$ErrorActionPreference = 'Stop'
$folder = if ($Game -eq 'forever') { '_forever_' } else { '_classic_era_' }
$exeName = if ($Game -eq 'forever') { 'Wow.exe' } else { 'WowClassic.exe' }
$exe = Join-Path $Root "$folder\$exeName"
if (-not (Test-Path $exe)) { throw "Not installed: $exe" }
Add-Type @'
using System; using System.Collections.Generic; using System.Diagnostics; using System.Runtime.InteropServices; using System.Text; using System.Threading;
public static class RP {
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Ri, B; }
  [StructLayout(LayoutKind.Sequential)] struct LI { public uint s; public uint t; }
  [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LI l);
  public static double Idle() { var l = new LI(); l.s = 8; GetLastInputInfo(ref l); return unchecked((uint)Environment.TickCount - l.t) / 1000.0; }
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out R r);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out R r, int n);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
  [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  public static IntPtr Find(int pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); var c = new StringBuilder(64); GetClassName(h, c, 64);
      if (p == pid && IsWindowVisible(h) && c.ToString() == "waApplication Window") { found = h; return false; } return true; }, IntPtr.Zero);
    return found;
  }
  public static string S(R r) { return r.L + "," + r.T + " " + (r.Ri - r.L) + "x" + (r.B - r.T); }
  public static R Win(IntPtr h) { R r; GetWindowRect(h, out r); return r; }
  public static R Vis(IntPtr h) { R r; if (DwmGetWindowAttribute(h, 9, out r, 16) != 0) GetWindowRect(h, out r); return r; }
  public static R Cli(IntPtr h) { R r; GetClientRect(h, out r); return r; }
  public static string State(IntPtr h) { var c = Cli(h); return "win " + S(Win(h)) + " | vis " + S(Vis(h)) + " | client " + c.Ri + "x" + c.B + (IsZoomed(h) ? " | MAXIMIZED" : ""); }
  const uint NOACT = 0x10, ASYNC = 0x4000, FRAME = 0x20, SHOW = 0x40, NOZ = 0x4;
  // like WindowPlacer.Place: the visible rect is what the slot means; the window rect gets the invisible border added
  public static void Place(IntPtr h, int x, int y, int w, int hh) {
    R win = Win(h), vis = Vis(h);
    int l = vis.L - win.L, t = vis.T - win.T, r = win.Ri - vis.Ri, b = win.B - vis.B;
    SetWindowPos(h, IntPtr.Zero, x - l, y - t, w + l + r, hh + t + b, NOACT | ASYNC | FRAME | SHOW | NOZ);
  }
  // samples every 10 ms; returns one line per change, with the time since the call
  public static List<string> Watch(IntPtr h, int ms) {
    var lines = new List<string>(); var sw = Stopwatch.StartNew(); string last = null;
    while (sw.ElapsedMilliseconds < ms) { var s = State(h); if (s != last) { lines.Add(string.Format("  {0,5} ms  {1}", sw.ElapsedMilliseconds, s)); last = s; } Thread.Sleep(10); }
    return lines;
  }
}
'@
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -ieq $exe })
if ($running.Count -gt 0) { throw "A client of $folder is running. Close it first." }
$idle = [RP]::Idle()
if (-not $Force -and $idle -lt $MinIdle) { throw "The PC was used $([int]$idle) s ago. Run when idle >= $MinIdle s (or -Force)." }

$config = Join-Path $Root "$folder\WTF\Config.wtf"
$backup = Join-Path $env:TEMP "wowboxer-resize-probe-$Game-Config.wtf"
$hash = $null
if (Test-Path $config) { Copy-Item $config $backup -Force; $hash = (Get-FileHash $config).Hash }
$report = [System.Collections.Generic.List[string]]::new()
function Say($s) { Write-Host $s; $report.Add($s) }
# PrintWindow picture of the client (read-only): shows whether it draws its UI for the new size or an old one, stretched
function Snap($name) {
    if (-not (Test-Path $Wbx)) { return }
    $dir = Join-Path (Split-Path $Out) "resize-probe-$Game-$name"
    & $Wbx snap --game $Game --out $dir 2>&1 | Select-String 'Window 1' | ForEach-Object { Say "  snap $name  $($_.Line.Trim())" }
}
Say "resize probe $Game  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  exe $folder\$exeName"
if (Test-Path $config) {
    Select-String -Path $config -Pattern '^SET (gx\w*|windowResizeLock|renderScale|resolution\w*|useUiScale|uiScale|maxFPS\w*)\s' | ForEach-Object { Say "  Config.wtf  $($_.Line)" }
}
$proc = $null
try {
    $proc = Start-Process $exe -PassThru
    $h = [IntPtr]::Zero
    for ($i = 0; $i -lt 120 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 500; $h = [RP]::Find($proc.Id) }
    if ($h -eq [IntPtr]::Zero) { throw 'The client window did not open.' }
    Say "window open: $([RP]::State($h))  style 0x$('{0:X8}' -f [RP]::GetWindowLong($h, -16))"
    Say 'loading (20 s), what the client does by itself:'
    [RP]::Watch($h, 20000) | ForEach-Object { Say $_ }

    $steps = @(
        @{ n = 'follower 870x696 (5:4, the owner''s Main + side)'; r = @(1690, 0, 870, 696) },
        @{ n = 'same again (a second placing, like the keeper)'; r = @(1690, 0, 870, 696) },
        @{ n = 'main 1690x1392 (1.21)'; r = @(0, 0, 1690, 1392) },
        @{ n = 'follower 928x696 (4:3)'; r = @(1632, 0, 928, 696) },
        @{ n = 'main 1632x1392 (1.17)'; r = @(0, 0, 1632, 1392) },
        @{ n = 'narrow 700x696 (1.01)'; r = @(1800, 0, 700, 696) },
        @{ n = 'wide 1200x600 (2.0)'; r = @(1300, 0, 1200, 600) },
        @{ n = 'small 640x480'; r = @(1900, 0, 640, 480) },
        @{ n = 'tiny 400x300'; r = @(2100, 0, 400, 300) },
        @{ n = 'follower 870x696 at the lower slot'; r = @(1690, 696, 870, 696) }
    )
    foreach ($s in $steps) {
        $r = $s.r
        Say ''
        Say "STEP $($s.n): visible wanted $($r[0]),$($r[1]) $($r[2])x$($r[3])"
        Say "  before   $([RP]::State($h))"
        [RP]::Place($h, $r[0], $r[1], $r[2], $r[3])
        [RP]::Watch($h, $SettleMs) | ForEach-Object { Say $_ }
        if ($s.n -like 'main 1690*') { Snap 'main-1690' }
    }

    Say ''
    Say 'SWAP-LIKE: follower 870x696 -> main 1690x1392, then the keeper places again every 250 ms for 3 s'
    [RP]::Place($h, 1690, 0, 870, 696); Start-Sleep -Milliseconds 1500
    Say "  before   $([RP]::State($h))"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    for ($k = 0; $k -lt 12; $k++) { [RP]::Place($h, 0, 0, 1690, 1392); [RP]::Watch($h, 250) | ForEach-Object { Say $_ } }
    Say '  then 3 s alone:'
    [RP]::Watch($h, 3000) | ForEach-Object { Say $_ }
    Snap 'after-swap-like'

    Say ''
    Say 'RAPID: main <-> follower every 300 ms, 6 times (swap hotkeys pressed quickly), then 4 s alone'
    for ($k = 0; $k -lt 6; $k++) {
        if ($k % 2 -eq 0) { [RP]::Place($h, 1690, 0, 870, 696) } else { [RP]::Place($h, 0, 0, 1690, 1392) }
        [RP]::Watch($h, 300) | ForEach-Object { Say $_ }
    }
    [RP]::Watch($h, 4000) | ForEach-Object { Say $_ }
    Say "end      $([RP]::State($h))"
    Snap 'after-rapid'
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force; $proc.WaitForExit(10000) | Out-Null }
    Start-Sleep -Seconds 2
    if ($hash) {
        Copy-Item $backup $config -Force
        $same = (Get-FileHash $config).Hash -eq $hash
        Say "Config.wtf put back: $(if ($same) { 'identical (SHA-256)' } else { 'NOT IDENTICAL - check it' })"
    }
    $report | Set-Content -Path $Out -Encoding utf8
    Write-Host "report: $Out"
}
