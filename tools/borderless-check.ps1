# RUN IT, NEVER DOT-SOURCE IT:  powershell -ExecutionPolicy Bypass -File tools\borderless-check.ps1 [-Out FOLDER]
# "Remove title bars" on REAL WoW clients that are open now (Classic Era by default), with this branch's wbx:
#   1. saves every WoW window's exact place, style, ex-style and "on top";
#   2. wbx snap (framed), then wbx arrange --borderless: checks popup chrome (no caption, client area = window area =
#      visible bounds) and that the follower column has no gaps, then wbx snap (borderless);
#   3. arrange framed again (title bars back), then wbx restore ("Give title bars back"): checks the frame is back;
#   4. puts every window back exactly as it was (place, styles, "on top").
# Look at the two snap folders: action bar and minimap drawn at the right size in both. Only window positions and
# styles change; nothing is sent to the game. Run only while nobody is playing (it moves the windows for ~30 s).
param(
    [string]$Wbx = (Join-Path $PSScriptRoot '..\src\WoWBoxer.Layouts.Cli\bin\Release\net10.0-windows\wbx.exe'),
    [string]$Out = (Join-Path $env:TEMP "wowboxer-borderless-$(Get-Date -Format 'yyyyMMdd-HHmmss')"),
    [string]$Game = 'classic'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Wbx)) { throw "wbx not found ($Wbx). Build first: dotnet build -c Release" }
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class BC {
  public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int n);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  public static int[] Win(IntPtr h) { RECT r; GetWindowRect(h, out r); return new[] { r.L, r.T, r.R - r.L, r.B - r.T }; }
  public static int[] Client(IntPtr h) { RECT r; GetClientRect(h, out r); return new[] { r.R, r.B }; }
  public static int[] Visible(IntPtr h) { RECT r; if (DwmGetWindowAttribute(h, 9, out r, 16) != 0) GetWindowRect(h, out r); return new[] { r.L, r.T, r.R - r.L, r.B - r.T }; }
}
'@
$results = New-Object System.Collections.Generic.List[string]; $script:fails = 0
function Check($what, [bool]$ok) { if ($ok) { $results.Add("PASS  $what") } else { $results.Add("FAIL  $what"); $script:fails++ } }

$wow = @(Get-Process Wow, WowClassic -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Sort-Object StartTime)
if ($wow.Count -eq 0) { throw 'No WoW client is open.' }
$h = @($wow | ForEach-Object { $_.MainWindowHandle })
$saved = @($h | ForEach-Object { @{ H = $_; Rect = [BC]::Win($_); Style = [BC]::GetWindowLong($_, -16); Ex = [BC]::GetWindowLong($_, -20) } })
$fg = [BC]::GetForegroundWindow()
New-Item -ItemType Directory -Force $Out | Out-Null
$n = $h.Count
$CAPTION = 0x00C00000; $POPUP = [int]0x80000000

try {
    & $Wbx snap --game $Game --out "$Out\1-framed-before" | Out-Null
    & $Wbx arrange --game $Game --preset side --clients $n --borderless | Out-Null
    Start-Sleep 3
    & $Wbx arrange --game $Game --preset side --clients $n --borderless | Out-Null   # WoW settles after a frame change
    Start-Sleep 2
    foreach ($w in $h) {
        $s = [BC]::GetWindowLong($w, -16); $win = [BC]::Win($w); $cl = [BC]::Client($w); $vis = [BC]::Visible($w)
        Check "borderless $w`: popup, no caption (style 0x$('{0:X8}' -f $s))" ((($s -band $CAPTION) -eq 0) -and (($s -band $POPUP) -ne 0))
        Check "borderless $w`: client area = window = visible ($($cl -join 'x') / $($win[2])x$($win[3]) / $($vis[2])x$($vis[3]))" (($cl[0] -eq $win[2]) -and ($cl[1] -eq $win[3]) -and ($vis[2] -eq $win[2]) -and ($vis[3] -eq $win[3]))
    }
    if ($n -ge 3) {
        # the followers (every window but the biggest) stacked in one column, top to bottom, without gaps
        # (objects, not nested arrays: PowerShell flattens arrays of arrays in a pipeline)
        $rects = New-Object System.Collections.Generic.List[object]
        foreach ($w in $h) { $v = [BC]::Visible($w); $rects.Add([pscustomobject]@{ X = $v[0]; Y = $v[1]; W = $v[2]; H = $v[3] }) }
        $main = $rects | Sort-Object { $_.W * $_.H } -Descending | Select-Object -First 1
        $f = @($rects | Where-Object { $_ -ne $main } | Sort-Object Y)
        $gaps = 0
        for ($i = 1; $i -lt $f.Count; $i++) { if ($f[$i].Y -ne $f[$i - 1].Y + $f[$i - 1].H) { $gaps++ } }
        Check "borderless: follower column has no gaps ($(@($f | ForEach-Object { "y $($_.Y) h $($_.H)" }) -join ', '))" ($gaps -eq 0)
    }
    & $Wbx snap --game $Game --out "$Out\2-borderless" | Out-Null

    & $Wbx arrange --game $Game --preset side --clients $n --no-borderless | Out-Null
    Start-Sleep 3
    foreach ($w in $h) {
        $s = [BC]::GetWindowLong($w, -16)
        Check "toggled back $w`: title bar again, no popup" ((($s -band $CAPTION) -eq $CAPTION) -and (($s -band $POPUP) -eq 0))
    }
    & $Wbx snap --game $Game --out "$Out\3-framed-after" | Out-Null
    & $Wbx arrange --game $Game --preset side --clients $n --borderless | Out-Null
    Start-Sleep 2
    & $Wbx restore --game $Game | Out-Null   # "Give title bars back"
    Start-Sleep 2
    foreach ($w in $h) {
        $s = [BC]::GetWindowLong($w, -16)
        Check "Give title bars back $w`: title bar again, no popup" ((($s -band $CAPTION) -eq $CAPTION) -and (($s -band $POPUP) -eq 0))
    }
    Check 'the active window never changed' ([BC]::GetForegroundWindow() -eq $fg)
} finally {
    # exactly as before: styles first, then place and size ("on top" is part of the ex-style; re-applied by z-order)
    foreach ($x in $saved) {
        [void][BC]::SetWindowLong($x.H, -16, $x.Style)
        [void][BC]::SetWindowLong($x.H, -20, $x.Ex)
        $top = if (($x.Ex -band 8) -ne 0) { [IntPtr](-1) } else { [IntPtr](-2) }
        [void][BC]::SetWindowPos($x.H, $top, $x.Rect[0], $x.Rect[1], $x.Rect[2], $x.Rect[3], 0x0010 -bor 0x0020 -bor 0x0200)
    }
    Start-Sleep 2
    $back = @($saved | Where-Object { ([BC]::Win($_.H) -join ',') -ne ($_.Rect -join ',') -or [BC]::GetWindowLong($_.H, -16) -ne $_.Style })
    $results.Add($(if ($back.Count -eq 0) { "OK    every window back exactly as it was" } else { "WARN  $($back.Count) window(s) not exactly back" }))
}
$results | ForEach-Object { Write-Host $_ }
Write-Host "Snaps: $Out"
Write-Host ("{0} checks failed" -f $script:fails)
exit $script:fails
