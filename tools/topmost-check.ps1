# Always-on-top check on the REAL WoW windows, driven only through the app's own window (UI Automation).
#  Part 1: for each slot, select its squad row, toggle "Always on top" on and off and read WS_EX_TOPMOST of the WoW
#          window in that slot (live mode is switched OFF for this part: on-top must apply without it).
#  Part 2: disturbances, many rounds: another program clears/sets "on top" of a WoW window, a WoW window is moved,
#          and the foreground switches between our own helper windows (Notepad we start, and the app). The app's
#          guard must put every window back to its slot's state within a second.
# Nothing is sent to a game window (no keys, no clicks, no activation); WoW windows are only read, moved and restacked
# the way the app itself does it. Needs: the app running, one open WoW window per slot of the current layout.
param([int]$Rounds = 3, [int]$Disturbances = 20)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
if (-not ('TopCheck' -as [type])) {
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class TopCheck {
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Ri, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  public static bool IsTop(IntPtr h) { return (GetWindowLong(h, -20) & 8) != 0; }
}
"@
}
$A = [System.Windows.Automation.AutomationElement]; $Scope = [System.Windows.Automation.TreeScope]
function Main { @($A::RootElement.FindAll($Scope::Children, [System.Windows.Automation.Condition]::TrueCondition)) | Where-Object { $_.Current.Name -like 'WoWBoxer Window Layouts*' } | Select-Object -First 1 }
function ByName($name) { (Main).FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name))) }
function Pattern($e, $p) { $o = $null; if ($e.TryGetCurrentPattern($p, [ref]$o)) { $o } else { $null } }
function Select-Row($n) { (Pattern (ByName "Window $n") ([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select(); Start-Sleep -Milliseconds 500 }
function SetSwitch($name, $on) { $t = Pattern (ByName $name) ([System.Windows.Automation.TogglePattern]::Pattern); if (($t.Current.ToggleState -eq 'On') -ne $on) { $t.Toggle(); Start-Sleep -Milliseconds 500 } }

$settingsPath = "$env:APPDATA\WoWBoxer\Layouts\settings.json"
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$profilePath = Join-Path "$env:APPDATA\WoWBoxer\Layouts\profiles" ($settings.lastProfile + '.json')
function Wanted($n) { @((Get-Content $profilePath -Raw | ConvertFrom-Json).slots)[$n - 1].z -eq 'top' }

(Pattern (ByName 'SETUP') ([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select(); Start-Sleep -Milliseconds 600
$apply = ByName 'APPLY TO OPEN WINDOWS'; (Pattern $apply ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Seconds 5

# slot -> WoW window: the window sitting at the slot's planned place
$wbx = Join-Path $PSScriptRoot '..\publish\wbx\wbx.exe'
$info = & $wbx info --profile $profilePath 2>&1 | Out-String
$slotRects = [regex]::Matches($info, '(\d+)\. .{1,12}-> (-?\d+),(-?\d+) (\d+)x(\d+)') | ForEach-Object { [pscustomobject]@{ N = [int]$_.Groups[1].Value; X = [int]$_.Groups[2].Value; Y = [int]$_.Groups[3].Value } }
$wins = [regex]::Matches($info, 'pid (\d+) .*?visible (-?\d+),(-?\d+) ') | ForEach-Object { [pscustomobject]@{ Pid = [int]$_.Groups[1].Value; X = [int]$_.Groups[2].Value; Y = [int]$_.Groups[3].Value } }
$map = @(foreach ($slot in $slotRects) {
  $w = $wins | Where-Object { $_.X -eq $slot.X -and $_.Y -eq $slot.Y } | Select-Object -First 1
  if ($w) { [pscustomobject]@{ N = $slot.N; Pid = $w.Pid; H = (Get-Process -Id $w.Pid).MainWindowHandle } } else { Write-Host "slot $($slot.N): no WoW window at its place - skipped" }
})

$script:failures = 0; $script:checks = 0
function Check($label, $ok) { $script:checks++; if (-not $ok) { $script:failures++ }; Write-Host ("{0} {1}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $label) }

# ---- part 1: toggling, live mode OFF ----
$liveBefore = $settings.liveApply
SetSwitch 'Move WoW windows live' $false
foreach ($m in $map) {
  Select-Row $m.N
  $toggle = Pattern (ByName 'Always on top (floating overlay)') ([System.Windows.Automation.TogglePattern]::Pattern)
  $original = $toggle.Current.ToggleState
  for ($i = 1; $i -le $Rounds * 2; $i++) {
    $toggle.Toggle(); Start-Sleep -Milliseconds 400
    $want = $toggle.Current.ToggleState -eq 'On'
    $is = [TopCheck]::IsTop($m.H); $saved = Wanted $m.N
    Check ("slot {0} toggle {1} (live off): checkbox={2} saved={3} WS_EX_TOPMOST={4}" -f $m.N, $i, $want, $saved, $is) (($is -eq $want) -and ($saved -eq $want))
  }
  if ($toggle.Current.ToggleState -ne $original) { $toggle.Toggle(); Start-Sleep -Milliseconds 400 }
}
if ($liveBefore) { SetSwitch 'Move WoW windows live' $true }

# ---- part 2: disturbances ----
$notepad = Start-Process notepad -PassThru; Start-Sleep -Seconds 1
$appWin = Main
$flags = 0x0001 -bor 0x0002 -bor 0x0010 -bor 0x0200   # NOSIZE | NOMOVE | NOACTIVATE | NOOWNERZORDER
for ($d = 1; $d -le $Disturbances; $d++) {
  $m = $map[($d - 1) % $map.Count]
  switch (($d - 1) % 4) {
    0 { [void][TopCheck]::SetWindowPos($m.H, [IntPtr](-2), 0, 0, 0, 0, $flags); $what = 'other program cleared on-top' }
    1 { [void][TopCheck]::SetWindowPos($m.H, [IntPtr](-1), 0, 0, 0, 0, $flags); $what = 'other program set on-top' }
    2 { $r = New-Object TopCheck+R; [void][TopCheck]::GetWindowRect($m.H, [ref]$r)
        [void][TopCheck]::SetWindowPos($m.H, [IntPtr]::Zero, $r.L + 3, $r.T, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010)   # moved 3 px
        [void][TopCheck]::SetWindowPos($m.H, [IntPtr](-2), 0, 0, 0, 0, $flags); $what = 'moved + on-top cleared' }
    3 { [void][TopCheck]::SetWindowPos($m.H, [IntPtr](-2), 0, 0, 0, 0, $flags)
        if ($d % 8 -eq 4) { [void](New-Object -ComObject WScript.Shell).AppActivate($notepad.Id) } else { try { $appWin.SetFocus() } catch {} }
        $what = 'foreground switched (Notepad / app) + on-top cleared' }
  }
  Start-Sleep -Milliseconds 1000
  $want = Wanted $m.N; $is = [TopCheck]::IsTop($m.H)
  Check ("disturbance {0} slot {1}: {2} -> WS_EX_TOPMOST={3}, wanted {4}" -f $d, $m.N, $what, $is, $want) ($is -eq $want)
}
Stop-Process -Id $notepad.Id -ErrorAction SilentlyContinue
(Pattern $apply ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Seconds 3   # moved windows back exactly

Write-Host "$($script:checks - $script:failures)/$($script:checks) passed"
exit $script:failures
