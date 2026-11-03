<#
  RUN IT, NEVER DOT-SOURCE IT:  powershell -ExecutionPolicy Bypass -File tools\edit-real-check.ps1 [-Exe ...]
  Advanced editing on REAL WoW clients (Classic Era, all open ones): WoWBoxer Dev (default: this branch's dev publish,
  artifacts\dev\publish – its own settings, the normal app's are never touched) places them in Main + side, then the
  toolbar is driven through UI Automation: Select all → Fill row → the real windows must form one row edge to edge;
  Undo → back in Main + side; Align top on a single window → it moves to the top. The active window must never change.
  Saves every WoW window's exact place, style and "on top" first and puts them back at the end (also on failure), and
  restores the Dev settings. Only window positions change; nothing is sent to the game. Run while nobody plays.
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\artifacts\dev\publish\WoWBoxer.exe'),
    [string]$Wbx = (Join-Path $PSScriptRoot '..\src\WoWBoxer.Layouts.Cli\bin\Release\net10.0-windows\wbx.exe')
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { throw "WoWBoxer Dev not built ($Exe): tools\dev-build.ps1" }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class RC {
  public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int n);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  public static int[] Win(IntPtr h) { RECT r; GetWindowRect(h, out r); return new[] { r.L, r.T, r.R - r.L, r.B - r.T }; }
  public static int[] Vis(IntPtr h) { RECT r; if (DwmGetWindowAttribute(h, 9, out r, 16) != 0) GetWindowRect(h, out r); return new[] { r.L, r.T, r.R - r.L, r.B - r.T }; }
  public static List<IntPtr> Windows(uint pid) { var l = new List<IntPtr>(); EnumWindows((h, x) => { uint p; GetWindowThreadProcessId(h, out p); var sb = new StringBuilder(256); GetWindowText(h, sb, 256); if (p == pid && IsWindowVisible(h) && sb.Length > 0) l.Add(h); return true; }, IntPtr.Zero); return l; }
  public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
}
'@
$A = [System.Windows.Automation.AutomationElement]; $Scope = [System.Windows.Automation.TreeScope]
$results = New-Object System.Collections.Generic.List[string]; $script:fails = 0
function Check($what, [bool]$ok) { if ($ok) { $results.Add("PASS  $what") } else { $results.Add("FAIL  $what"); $script:fails++ } }

$wow = @(Get-Process WowClassic -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Sort-Object StartTime)
if ($wow.Count -lt 2) { throw 'Open 2 or more Classic Era clients first.' }
$h = @($wow | ForEach-Object { $_.MainWindowHandle })
$saved = @($h | ForEach-Object { @{ H = $_; Rect = [RC]::Win($_); Style = [RC]::GetWindowLong($_, -16); Ex = [RC]::GetWindowLong($_, -20) } })
$fg = [RC]::GetForegroundWindow()
$n = $h.Count
Get-Process WoWBoxer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$env:LOCALAPPDATA\WoWBoxer.Dev\*" -or $_.Path -eq (Resolve-Path $Exe).Path } | Stop-Process -Force

$dev = Join-Path $env:APPDATA 'WoWBoxer\Dev'
$backup = Join-Path $env:TEMP "wowboxer-devsettings-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
if (Test-Path $dev) { Copy-Item $dev $backup -Recurse }
$proc = $null
try {
    Remove-Item $dev -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$dev\profiles" | Out-Null
    $o = & $Wbx layout --preset side --clients $n --json | Out-String | ConvertFrom-Json
    $o.name = 'Edit real check'
    $o | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 "$dev\profiles\Edit real check.json"
    $version = ((Get-Item $Exe).VersionInfo.ProductVersion -split '\+')[0]
    $exeGame = $wow[0].Path
    @{ firstRunDone = $true; lastSeenVersion = $version; lastProfile = 'Edit real check'; checkForUpdates = $false; startPlacingOnOpen = $true
       renameWindows = $false; autoFitActionBar = $false; gameExe = $exeGame; turnHoverOffOnExit = $false; advancedEditing = $true } |
        ConvertTo-Json | Set-Content -Encoding utf8 "$dev\settings.json"

    $proc = Start-Process $Exe -ArgumentList '--resume' -PassThru
    $main = $null
    for ($i = 0; $i -lt 80 -and -not $main; $i++) { Start-Sleep -Milliseconds 250; $w = [RC]::Windows([uint32]$proc.Id) | Where-Object { [RC]::Title($_) -like 'WoWBoxer Dev*' } | Select-Object -First 1; if ($w) { $main = $A::FromHandle($w) } }
    if (-not $main) { throw 'WoWBoxer Dev did not open' }
    Start-Sleep 6   # the windows go to Main + side
    $fgStart = [RC]::GetForegroundWindow()
    function Find($name) { $main.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name))) }
    function Press($name) { $e = Find $name; $o = $null; if (-not $e -or -not $e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$o)) { throw "no button $name" }; $o.Invoke(); Start-Sleep 3 }
    # rectangles as objects (PowerShell flattens arrays of arrays in a pipeline)
    function Rects { foreach ($w in $h) { $v = [RC]::Vis($w); [pscustomobject]@{ X = $v[0]; Y = $v[1]; W = $v[2]; H = $v[3]; S = "$($v[0]),$($v[1]) $($v[2])x$($v[3])" } } }
    $homeRects = @(Rects)
    Check "Main + side placed $n real windows ($(@($homeRects | ForEach-Object { $_.S }) -join ' | '))" ((@($homeRects | ForEach-Object { "$($_.X),$($_.Y)" }) | Select-Object -Unique).Count -eq $n)

    Press 'Select all'
    Press 'Fill row'
    $row = @(Rects | Sort-Object X)
    $edge = 0; for ($i = 1; $i -lt $row.Count; $i++) { if ($row[$i].X -ne $row[$i - 1].X + $row[$i - 1].W) { $edge++ } }
    Check "Fill row: the real windows form one row edge to edge ($(@($row | ForEach-Object { "$($_.X)+$($_.W)" }) -join ', '))" ($edge -eq 0 -and (@($row | ForEach-Object { $_.Y }) | Select-Object -Unique).Count -eq 1)
    Check 'Fill row: equal widths (±1 px)' ((($row | Measure-Object W -Maximum).Maximum - ($row | Measure-Object W -Minimum).Minimum) -le 1)

    Press 'Undo'
    $back = @(Rects)
    $diff = 0; for ($i = 0; $i -lt $n; $i++) { if ($back[$i].S -ne $homeRects[$i].S) { $diff++ } }
    Check 'Undo: every real window back in Main + side' ($diff -eq 0)

    Press 'Redo'
    Press 'Clear selection'
    Check 'the operations never changed the active window' ([RC]::GetForegroundWindow() -eq $fgStart)
} catch {
    $results.Add("FAIL  stopped: $($_.Exception.Message)"); $script:fails++
} finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process $proc -Force }
    Start-Sleep -Milliseconds 500
    foreach ($x in $saved) {
        [void][RC]::SetWindowLong($x.H, -16, $x.Style)
        [void][RC]::SetWindowLong($x.H, -20, $x.Ex)
        $top = if (($x.Ex -band 8) -ne 0) { [IntPtr](-1) } else { [IntPtr](-2) }
        [void][RC]::SetWindowPos($x.H, $top, $x.Rect[0], $x.Rect[1], $x.Rect[2], $x.Rect[3], 0x0010 -bor 0x0020 -bor 0x0200)
    }
    Remove-Item $dev -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $backup) { Copy-Item $backup $dev -Recurse; Remove-Item $backup -Recurse -Force }
    Start-Sleep 1
    $off = @($saved | Where-Object { ([RC]::Win($_.H) -join ',') -ne ($_.Rect -join ',') })
    $results.Add($(if ($off.Count -eq 0) { 'OK    every WoW window back exactly as it was' } else { "WARN  $($off.Count) window(s) not exactly back" }))
}
$results | ForEach-Object { Write-Host $_ }
Write-Host ("{0} checks failed" -f $script:fails)
exit $script:fails
