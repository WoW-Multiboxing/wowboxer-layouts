<#
  RUN IT, NEVER DOT-SOURCE IT:  powershell -ExecutionPolicy Bypass -File tools\swap-real-check.ps1 [-IdleMinutes 3]
  Window swap main slot on REAL WoW clients (Classic Era, 3 open ones) with WoWBoxer Dev (this branch's dev publish,
  artifacts\dev\publish - its own settings; the normal app's are never touched). The 2026-11-04 beta bug:
  A) the owner's "Trio" as it was after a drag-swap (Custom, large window in slot 2, no crown): "--swap 1" must move a
     window into the large place (auto-picked main slot), no "click the crown" warning, IN MAIN SLOT shown;
  B) Main + side card, then the Custom card: the main slot is carried over, "--swap 3" works;
  C) Grid (no main slot): three presses move nothing, the warning is logged once and shown in the status bar.
  The active window must never change during the swaps. Swaps go through "--swap" (the hotkey path without keys).
  Waits until nobody has touched the PC for -IdleMinutes. If the normal WoWBoxer is open and placing, it is paused
  ("--pause") for the test and resumed afterwards. Saves every WoW window's exact place, style and "on top" first and
  puts them back at the end (also on failure). Only window positions change; nothing is sent to the game.
#>
param(
    [double]$IdleMinutes = 3,
    [double]$MaxWaitMinutes = 240,
    [string]$Exe = (Join-Path $PSScriptRoot '..\artifacts\dev\publish\WoWBoxer.exe'),
    [string]$Wbx = (Join-Path $PSScriptRoot '..\src\WoWBoxer.Layouts.Cli\bin\Release\net10.0-windows\wbx.exe')
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { throw "WoWBoxer Dev not built ($Exe): tools\dev-build.ps1" }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class SR {
  public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] struct LII { public uint cb; public uint t; }
  [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LII l);
  public static double IdleSeconds() { var l = new LII(); l.cb = 8; GetLastInputInfo(ref l); return unchecked((uint)Environment.TickCount - l.t) / 1000.0; }
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

# ---------- wait until nobody uses the PC ----------
$deadline = (Get-Date).AddMinutes($MaxWaitMinutes)
while ([SR]::IdleSeconds() -lt $IdleMinutes * 60) {
    if ((Get-Date) -gt $deadline) { Write-Host "Not idle for $IdleMinutes min within $MaxWaitMinutes min - nothing was changed."; exit 99 }
    Start-Sleep 15
}
Write-Host ("Idle for {0:0} s - starting." -f [SR]::IdleSeconds())

$wow = @(Get-Process WowClassic -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Sort-Object StartTime)
if ($wow.Count -ne 3) { throw "Needs exactly 3 open Classic Era clients (found $($wow.Count))." }
$h = @($wow | ForEach-Object { $_.MainWindowHandle })
$saved = @($h | ForEach-Object { @{ H = $_; Rect = [SR]::Win($_); Style = [SR]::GetWindowLong($_, -16); Ex = [SR]::GetWindowLong($_, -20) } })

# the normal WoWBoxer: paused for the test if it is placing windows (it would fight the Dev build over them)
$devFolder = "$env:LOCALAPPDATA\WoWBoxer.Dev"
$normal = Get-Process WoWBoxer -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -notlike "$devFolder\*" -and $_.Path -ne (Resolve-Path $Exe).Path } | Select-Object -First 1
$normalExe = $null; $resumeNormal = $false
if ($normal) {
    $normalExe = $normal.Path
    $last = Get-Content "$env:LOCALAPPDATA\WoWBoxer\Layouts\logs\app.log" -Tail 400 -ErrorAction SilentlyContinue | Where-Object { $_ -match ' (Started|Stopped): ' } | Select-Object -Last 1
    $resumeNormal = $last -notmatch ' Stopped: '
    if ($resumeNormal) { Start-Process $normalExe -ArgumentList '--pause'; Start-Sleep 3 }
    $results.Add("INFO  normal WoWBoxer open ($($normal.MainWindowTitle)); $(if ($resumeNormal) { 'paused for the test' } else { 'already stopped' })")
}
Get-Process WoWBoxer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$devFolder\*" -or $_.Path -eq (Resolve-Path $Exe).Path } | Stop-Process -Force

$dev = Join-Path $env:APPDATA 'WoWBoxer\Dev'
$devLog = "$env:LOCALAPPDATA\WoWBoxer\Dev\logs\app.log"
$backup = Join-Path $env:TEMP "wowboxer-devsettings-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
if (Test-Path $dev) { Copy-Item $dev $backup -Recurse }
$proc = $null
try {
    Remove-Item $dev -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$dev\profiles" | Out-Null
    # the bug layout: Main + side after dropping window 1 on window 2 (Custom, the large place is slot 2's, no crown)
    $o = & $Wbx layout --preset side --clients 3 --json | Out-String | ConvertFrom-Json
    $o.name = 'Swap real check'
    $o.preset = 'custom'
    $s0 = $o.slots[0]; $s1 = $o.slots[1]
    foreach ($k in 'x', 'y', 'w', 'h', 'anchor') { $t = $s0.$k; $s0.$k = $s1.$k; $s1.$k = $t }
    if ($o.PSObject.Properties['swapMainSlots']) { $o.PSObject.Properties.Remove('swapMainSlots') }
    $o | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 "$dev\profiles\Swap real check.json"
    $version = ((Get-Item $Exe).VersionInfo.ProductVersion -split '\+')[0]
    @{ firstRunDone = $true; lastSeenVersion = $version; lastProfile = 'Swap real check'; checkForUpdates = $false; startPlacingOnOpen = $true
       renameWindows = $false; autoFitActionBar = $false; gameExe = $wow[0].Path; turnHoverOffOnExit = $false; swapHotkeys = $true; swapRemember = $false } |
        ConvertTo-Json | Set-Content -Encoding utf8 "$dev\settings.json"
    $logStart = if (Test-Path $devLog) { @(Get-Content $devLog).Count } else { 0 }
    function NewLog { if (Test-Path $devLog) { @(Get-Content $devLog | Select-Object -Skip $script:logStart) } else { @() } }
    function CrownWarnings { @(NewLog | Where-Object { $_ -match 'click the crown' }).Count }

    $proc = Start-Process $Exe -ArgumentList '--resume' -PassThru
    $main = $null
    for ($i = 0; $i -lt 80 -and -not $main; $i++) { Start-Sleep -Milliseconds 250; $w = [SR]::Windows([uint32]$proc.Id) | Where-Object { [SR]::Title($_) -like 'WoWBoxer Dev*' } | Select-Object -First 1; if ($w) { $main = $A::FromHandle($w) } }
    if (-not $main) { throw 'WoWBoxer Dev did not open' }
    Start-Sleep 8   # the windows go to their slots
    function FindId($id) { $main.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::AutomationIdProperty, $id))) }
    function Card($name) {
        $cond = New-Object System.Windows.Automation.AndCondition (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name)), (New-Object System.Windows.Automation.PropertyCondition ($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button))
        $e = $main.FindFirst($Scope::Descendants, $cond); $p = $null
        if (-not $e -or -not $e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$p)) { throw "no card $name" }
        $p.Invoke(); Start-Sleep 4
    }
    function Swap($action) { Start-Process $Exe -ArgumentList '--swap', $action -Wait; Start-Sleep 3 }
    function Texts { @($main.FindAll($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text))) | ForEach-Object { $_.Current.Name }) }
    # which WoW window is in the large place, and every window's place
    function Places { foreach ($w in $h) { $v = [SR]::Vis($w); [pscustomobject]@{ H = $w; Area = [long]$v[2] * $v[3]; S = "$($v[0]),$($v[1]) $($v[2])x$($v[3])" } } }
    function Large { (Places | Sort-Object Area -Descending | Select-Object -First 1) }
    function PlaceSet { (@(Places | ForEach-Object { $_.S }) | Sort-Object) -join ' | ' }
    $layoutPlaces = PlaceSet
    $fg = [SR]::GetForegroundWindow()

    # ---------- A: the bug layout ----------
    Check "A: the bug layout is placed ($layoutPlaces)" ((@(Places | ForEach-Object { $_.S }) | Select-Object -Unique).Count -eq 3)
    Check 'A: no "click the crown" hint on the map (main slot auto-picked)' (-not (FindId 'MainSlotHint'))
    Check 'A: IN MAIN SLOT shows in the squad list before any swap' ([bool](@(Texts) | Where-Object { $_ -eq 'IN MAIN SLOT' }))
    $before = Large
    Swap '1'
    $after1 = Large
    if ($after1.H -eq $before.H) { Swap '3'; $after1 = Large }   # client 1 was in the large place already
    Check "A: --swap moved another window into the large place (large: $($after1.S))" ($after1.H -ne $before.H)
    Check 'A: the layout places are unchanged (only who sits where)' ((PlaceSet) -eq $layoutPlaces)
    $old = @(Places | Where-Object { $_.H -eq $before.H })[0]
    Check 'A: the window that was in the main slot took the newcomer''s old place' ($old.Area -lt $before.Area)
    Check 'A: no "click the crown" warning logged' ((CrownWarnings) -eq 0)
    Swap 'home'
    Check 'A: home puts the large window back' ((Large).H -eq $before.H)

    # ---------- B: Main + side, then Custom: carried over ----------
    Card 'Main + side'
    Card 'Custom'
    $prof = Get-Content "$dev\profiles\Swap real check.json" -Raw | ConvertFrom-Json
    Check "B: Custom from Main + side stores the main slot (swapMainSlots = $($prof.swapMainSlots -join ','))" ($prof.preset -eq 'custom' -and (@($prof.swapMainSlots) -join ',') -eq '1')
    $bBefore = Large
    Swap '3'
    Check 'B: --swap 3 moves a window into the large place' ((Large).H -ne $bBefore.H)
    Check 'B: no "click the crown" warning logged' ((CrownWarnings) -eq 0)
    Swap 'home'

    # ---------- C: Grid has no main slot: visible, logged once ----------
    Card 'Grid'
    $gridPlaces = @(Places | ForEach-Object { "$($_.H)=$($_.S)" }) -join ' | '
    Swap '2'; Swap '2'; Swap '2'
    Check 'C: without a main slot nothing moves' ((@(Places | ForEach-Object { "$($_.H)=$($_.S)" }) -join ' | ') -eq $gridPlaces)
    Check "C: the warning is logged once for three presses (logged: $(CrownWarnings))" ((CrownWarnings) -eq 1)
    Check 'C: the status bar shows the notice' ([bool](@(Texts) | Where-Object { $_ -match 'click the crown' }))
    Check 'C: the hint shows on the map' ([bool](FindId 'MainSlotHint'))

    Check 'the swaps never changed the active window' ([SR]::GetForegroundWindow() -eq $fg)
} catch {
    $results.Add("FAIL  stopped: $($_.Exception.Message)"); $script:fails++
} finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process $proc -Force }
    Start-Sleep -Milliseconds 500
    foreach ($x in $saved) {
        [void][SR]::SetWindowLong($x.H, -16, $x.Style)
        [void][SR]::SetWindowLong($x.H, -20, $x.Ex)
        $top = if (($x.Ex -band 8) -ne 0) { [IntPtr](-1) } else { [IntPtr](-2) }
        [void][SR]::SetWindowPos($x.H, $top, $x.Rect[0], $x.Rect[1], $x.Rect[2], $x.Rect[3], 0x0010 -bor 0x0020 -bor 0x0200)
    }
    Remove-Item $dev -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $backup) { Copy-Item $backup $dev -Recurse; Remove-Item $backup -Recurse -Force }
    Start-Sleep 1
    $off = @($saved | Where-Object { ([SR]::Win($_.H) -join ',') -ne ($_.Rect -join ',') })
    $results.Add($(if ($off.Count -eq 0) { 'OK    every WoW window back exactly as it was' } else { "WARN  $($off.Count) window(s) not exactly back" }))
    if ($resumeNormal) { Start-Process $normalExe -ArgumentList '--resume'; $results.Add('INFO  normal WoWBoxer resumed') }
}
$results | ForEach-Object { Write-Host $_ }
Write-Host ("{0} checks failed" -f $script:fails)
exit $script:fails
