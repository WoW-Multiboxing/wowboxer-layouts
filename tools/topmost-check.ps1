# Always-on-top check on the REAL WoW windows, driven only through the app's own window (UI Automation).
# For each slot: picks it in the inspector, toggles "Always on top" on and off several times and reads WS_EX_TOPMOST of
# the WoW window that sits in that slot. Nothing is sent to a game window; WoW windows are only read.
# Needs: the app running with "Move my WoW windows live" on, and one open WoW window per slot of the current layout.
param([int]$Rounds = 3)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
if (-not ('TopCheck' -as [type])) {
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class TopCheck {
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  public static bool IsTop(IntPtr h) { return (GetWindowLong(h, -20) & 8) != 0; }
}
"@
}
$A = [System.Windows.Automation.AutomationElement]; $Scope = [System.Windows.Automation.TreeScope]
function Main { @($A::RootElement.FindAll($Scope::Children, [System.Windows.Automation.Condition]::TrueCondition)) | Where-Object { $_.Current.Name -like 'WoWBoxer Window Layouts*' } | Select-Object -First 1 }
function ById($id) { (Main).FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::AutomationIdProperty, $id))) }
function ByName($name) { (Main).FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name))) }
function Pattern($e, $p) { $o = $null; if ($e.TryGetCurrentPattern($p, [ref]$o)) { $o } else { $null } }

$settings = Get-Content "$env:APPDATA\WoWBoxer\Layouts\settings.json" -Raw | ConvertFrom-Json
if (-not $settings.liveApply) { throw 'Turn "Move my WoW windows live" on first.' }
$profilePath = Join-Path "$env:APPDATA\WoWBoxer\Layouts\profiles" ($settings.lastProfile + '.json')

# Which WoW window is in which slot: the app's Squad rows order = slot order; here: match by the planned rectangles via wbx info is overkill,
# so we use the order the app reports in its Identify-free way: windows sorted like the layout (left-to-right, top-to-bottom of their slot).
# Setup tab, then "Apply to open windows" so every window sits in its slot (the app moves them; never input to WoW).
$tab = ByName 'SETUP'; (Pattern $tab ([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select(); Start-Sleep -Milliseconds 600
$apply = ByName 'APPLY TO OPEN WINDOWS'; if ($apply) { (Pattern $apply ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Seconds 5 }
$wbx = Join-Path $PSScriptRoot '..\publish\wbx\wbx.exe'
$info = & $wbx info --profile $profilePath 2>&1 | Out-String
$slotRects = [regex]::Matches($info, '(\d+)\. .{1,12}-> (-?\d+),(-?\d+) (\d+)x(\d+)') | ForEach-Object { [pscustomobject]@{ N = [int]$_.Groups[1].Value; X = [int]$_.Groups[2].Value; Y = [int]$_.Groups[3].Value } }
$wins = [regex]::Matches($info, 'pid (\d+) .*?visible (-?\d+),(-?\d+) ') | ForEach-Object { [pscustomobject]@{ Pid = [int]$_.Groups[1].Value; X = [int]$_.Groups[2].Value; Y = [int]$_.Groups[3].Value } }

$picker = ById 'SlotPicker'
$box = ByName 'Always on top (floating overlay)'
$failures = 0; $checks = 0
foreach ($slot in $slotRects) {
  $w = $wins | Where-Object { $_.X -eq $slot.X -and $_.Y -eq $slot.Y } | Select-Object -First 1
  if (-not $w) { Write-Host "slot $($slot.N): no WoW window at its place - skipped"; continue }
  $h = (Get-Process -Id $w.Pid).MainWindowHandle
  (Pattern $picker ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand(); Start-Sleep -Milliseconds 200
  $item = $picker.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, "Window $($slot.N)")))
  (Pattern $item ([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
  (Pattern $picker ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Collapse(); Start-Sleep -Milliseconds 400
  $toggle = Pattern $box ([System.Windows.Automation.TogglePattern]::Pattern)
  $original = $toggle.Current.ToggleState
  for ($i = 1; $i -le $Rounds * 2; $i++) {
    $toggle.Toggle(); Start-Sleep -Milliseconds 900
    $want = $toggle.Current.ToggleState -eq 'On'
    $saved = (@((Get-Content $profilePath -Raw | ConvertFrom-Json).slots)[$slot.N - 1].z -eq 'top')
    $is = [TopCheck]::IsTop($h)
    $checks++
    $ok = ($is -eq $want) -and ($saved -eq $want)
    if (-not $ok) { $failures++ }
    Write-Host ("slot {0} (pid {1}) toggle {2}: checkbox={3} saved={4} WS_EX_TOPMOST={5} {6}" -f $slot.N, $w.Pid, $i, $want, $saved, $is, $(if ($ok) { 'PASS' } else { 'FAIL' }))
  }
  if ($toggle.Current.ToggleState -ne $original) { $toggle.Toggle(); Start-Sleep -Milliseconds 600 }
}
Write-Host "$($checks - $failures)/$checks passed"
exit $failures
