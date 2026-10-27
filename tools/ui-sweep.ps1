<#
  UI sweep for WoWBoxer Window Layouts – drives ONLY the app's own window through UI Automation (no mouse, no keys,
  nothing reaches a game window) and checks the results in the saved layout files and the app log.
  Usage: start the app, then  powershell -ExecutionPolicy Bypass -File tools\ui-sweep.ps1
  Turns "Move WoW windows live" off for the sweep (so open game windows are not moved) and restores it at the end.
#>
param([switch]$KeepLive)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
# Our small dialogs (prompt, yes/no) do not appear in the UIA desktop list, so they are found by window handle.
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class WbxWin {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  public static List<IntPtr> Dialogs(uint pid, string mainTitle) { var r = new List<IntPtr>(); EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); var sb = new StringBuilder(256); GetWindowText(h, sb, 256); if (p == pid && IsWindowVisible(h) && sb.ToString() != mainTitle) r.Add(h); return true; }, IntPtr.Zero); return r; }
}
'@
$A = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
$settingsPath = Join-Path $env:APPDATA 'WoWBoxer\Layouts\settings.json'
$profiles = Join-Path $env:APPDATA 'WoWBoxer\Layouts\profiles'
$log = Join-Path $env:LOCALAPPDATA 'WoWBoxer\Layouts\logs\app.log'
$results = New-Object System.Collections.Generic.List[string]
$fails = 0

function Main { $w = @($A::RootElement.FindAll($Scope::Children, [System.Windows.Automation.Condition]::TrueCondition)) | Where-Object { $_.Current.Name -like 'WoWBoxer Window Layouts*' -and $_.Current.ClassName -notlike '*Wnd' } | Select-Object -First 1; if (-not $w) { throw 'App window not found' }; $w }   # title carries the version
function Find($name, $root = (Main)) { if ($env:WBX_TRACE) { Write-Host "  > $name" }; $root.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name))) }
function FindId($id, $root = (Main)) { $root.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::AutomationIdProperty, $id))) }
function Pattern($e, $p) { $o = $null; if ($e.TryGetCurrentPattern($p, [ref]$o)) { $o } else { $null } }
function FindAnywhere($name) {
  $e = Find $name; if ($e) { return $e }
  foreach ($h in [WbxWin]::Dialogs([uint32](Get-Process WoWBoxer.Layouts).Id, 'WoWBoxer Window Layouts')) {
    $e = Find $name ([System.Windows.Automation.AutomationElement]::FromHandle($h)); if ($e) { return $e }
  }
  $null
}
function Invoke($name) { $e = FindAnywhere $name; if (-not $e) { throw "not found: $name" }; (Pattern $e ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Milliseconds 400 }
function Pick($name) { $e = Find $name; if (-not $e) { throw "not found: $name" }; (Pattern $e ([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select(); Start-Sleep -Milliseconds 500 }
function ToggleState($name) { $e = Find $name; (Pattern $e ([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState.ToString() }
function SetToggle($name, $on) { $e = Find $name; $t = Pattern $e ([System.Windows.Automation.TogglePattern]::Pattern); if (($t.Current.ToggleState -eq 'On') -ne $on) { $t.Toggle(); Start-Sleep -Milliseconds 400 } }
function Settings { Get-Content $settingsPath -Raw | ConvertFrom-Json }
function Current { $n = (Settings).lastProfile; Get-Content (Join-Path $profiles ($n + '.json')) -Raw | ConvertFrom-Json }
function Check($what, [scriptblock]$test) {
  try { $ok = & $test } catch { $ok = $false; $what += " ($($_.Exception.Message))" }
  if ($ok) { $results.Add("PASS  $what") } else { $results.Add("FAIL  $what"); $script:fails++ }
}
function LogErrors { if (Test-Path $log) { @(Get-Content $log | Where-Object { $_ -match ' Error ' -and $_ -ge $script:startStamp }).Count } else { 0 } }
function Dialog {
  for ($i = 0; $i -lt 20; $i++) {
    $h = [WbxWin]::Dialogs([uint32](Get-Process WoWBoxer.Layouts).Id, 'WoWBoxer Window Layouts') | Select-Object -First 1
    if ($h) { return [System.Windows.Automation.AutomationElement]::FromHandle($h) }
    Start-Sleep -Milliseconds 150
  }
  throw 'dialog did not open'
}
function Prompt($text, $button = 'OK') {
  $d = Dialog
  $box = FindId 'PromptText' $d
  (Pattern $box ([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($text)
  (Pattern (Find $button $d) ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Milliseconds 600
}
function Answer($button) { $d = Dialog; (Pattern (Find $button $d) ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Milliseconds 600 }

$script:startStamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
$liveBefore = (Settings).liveApply
Pick 'SETUP'
if (-not $KeepLive) { SetToggle 'Move WoW windows live' $false }
$original = (Settings).lastProfile

# ---- layouts × client counts ----
$cards = 'Main + side', 'Overlays', 'Grid', '2 monitors', 'Free for all'
$kinds = @{ 'Main + side' = 'side'; 'Overlays' = 'overlay'; 'Grid' = 'grid'; '2 monitors' = 'dual'; 'Free for all' = 'free' }
Invoke 'Save as…'; Prompt 'UI sweep'
$sweepName = (Settings).lastProfile
Check 'Save as creates and selects a "UI sweep" layout' { $sweepName -like 'UI sweep*' -and (Test-Path (Join-Path $profiles ($sweepName + '.json'))) }
foreach ($n in 2..8) {
  Pick "$n"
  foreach ($c in $cards) {
    $card = Find $c
    if (-not $card.Current.IsEnabled) { $results.Add("SKIP  $n clients · $c (not available on this PC)"); continue }
    Invoke $c
    Check "$n clients · $c" { $p = Current; $p.clients -eq $n -and $p.preset -eq $kinds[$c] -and @($p.slots).Count -eq $n }
  }
}
Pick '3'; Invoke 'Main + side'

# ---- switches ----
SetToggle 'Full action bar' $true;  Check 'Full action bar on → min aspect 4:3' { [math]::Abs((Current).options.minAspect - 4/3) -lt 0.01 }
SetToggle 'Full action bar' $false; Check 'Full action bar off → min aspect 1.25' { [math]::Abs((Current).options.minAspect - 1.25) -lt 0.01 }
SetToggle 'Cover the taskbar area' $true;  Check 'Cover taskbar on' { (Current).options.useTaskbarArea -eq $true }
SetToggle 'Cover the taskbar area' $false; Check 'Cover taskbar off' { (Current).options.useTaskbarArea -eq $false }
SetToggle 'All monitors' $true; SetToggle 'All monitors' $false; Check 'All monitors toggles without error' { (LogErrors) -eq 0 }

# ---- undo / reset ----
Pick '4'; Invoke 'Undo'
Check 'Undo after 3→4 clients gives 3 again' { (Current).clients -eq 3 }
Invoke 'Reset to preset'; Check 'Reset to preset keeps 3 clients side' { $p = Current; $p.clients -eq 3 -and $p.preset -eq 'side' }

# ---- inspector ----
Invoke 'Shorten window instead'
Check 'Shorten window instead (window 1) gives a 4:3 game area' {
  $s = @((Current).slots)[0]; $b = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
  $w = [math]::Round($b.Width * $s.w / 100); $h = [math]::Round($b.Height * $s.h / 100)
  (($w - 2) / ($h - 32)) -ge (4/3 - 0.01)
}

# window picker, measured action bar, Fix action bar (must refuse while clients of the install run)
Check 'Your squad shows one row per window' { @((Main).FindAll($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))) | Where-Object { $_.Current.Name -match '^Window \d$' }).Count -eq 3 }
Pick 'Window 2'
Check 'Selecting squad row 2 selects window 2 (details open in the row)' { (Pattern (Find 'Window 2') ([System.Windows.Automation.SelectionItemPattern]::Pattern)).Current.IsSelected -and [bool](Find 'Always on top (floating overlay)') }
Check 'Auto-fit action bar switch is there and on by default' { (ToggleState 'Auto-fit action bar') -eq 'On' -and (Settings).autoFitActionBar -ne $false }
Check 'Action bar text says Now (measured) or Planned' { (FindId 'SlotBarText').Current.Name -match '^(Now|Planned): ' }
$eraCfg = 'C:\Program Files (x86)\World of Warcraft\_classic_era_\WTF\Config.wtf'
$wowRunning = @(Get-Process WowClassic -ErrorAction SilentlyContinue).Count
if ($wowRunning -gt 0 -and (Test-Path $eraCfg)) {
  $hash = (Get-FileHash $eraCfg).Hash
  (Pattern (FindId 'FixUiScale') ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Milliseconds 600
  Check 'Fix action bar with clients running writes nothing' { (Get-FileHash $eraCfg).Hash -eq $hash -and -not (Test-Path "$eraCfg.wowboxer-backup") }
} else { $results.Add('SKIP  Fix action bar refusal (no Classic Era client running)') }

# ---- version ----
Check 'Title bar shows the version' { (FindId 'TitleVersion').Current.Name -match '^v\d+\.\d+\.\d+$' -and (Main).Current.Name -match 'WoWBoxer Window Layouts \d+\.\d+\.\d+' }

# ---- share codes ----
Invoke 'More ▾'; Invoke 'Copy layout code (WBX1:)'
$code = [System.Windows.Forms.Clipboard]::GetText()
Check 'Copy code puts a WBX1: code on the clipboard' { $code.StartsWith('WBX1:') }
Invoke 'More ▾'; Invoke 'Paste a layout code…'; Prompt $code
Check 'Paste code imports a new layout' { (Settings).lastProfile -like 'UI sweep*' -and (Settings).lastProfile -ne $sweepName }
$imported = (Settings).lastProfile

# ---- rename / delete ----
Invoke 'Rename'; Prompt 'UI sweep renamed'
Check 'Rename moves the file' { (Test-Path (Join-Path $profiles 'UI sweep renamed.json')) -and -not (Test-Path (Join-Path $profiles ($imported + '.json'))) }
Check 'Rename kept the renamed layout selected' { (Settings).lastProfile -eq 'UI sweep renamed' }
if ((Settings).lastProfile -eq 'UI sweep renamed') { Invoke 'More ▾'; Invoke 'Delete this layout'; Answer 'Yes' }
Check 'Delete removes the layout' { -not (Test-Path (Join-Path $profiles 'UI sweep renamed.json')) }

# ---- other tabs ----
Check 'No Squad tab any more (merged into Setup)' { -not (Find 'SQUAD') }
Check 'Each squad row has Identify' { @((Main).FindAll($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, 'Identify')))).Count -ge 3 }
Pick 'Login screens'; Check 'Launch mode: login screens saved' { (Settings).launchLoginScreens -eq $true }
Pick 'Battle.net';    Check 'Launch mode: Battle.net saved' { (Settings).launchLoginScreens -eq $false }
Pick 'GUIDE'
Start-Sleep -Milliseconds 600
foreach ($ch in 'Prepare your WoW clients', 'Pick clients and a layout', 'Start your squad', 'Switch between windows', 'If something looks wrong', 'What WoWBoxer never does') {
  Check "Guide chapter: $ch" { Invoke $ch; $true }
}
Check 'Guide tab is the current tab' { (Settings).lastTab -eq 'Guide' }
Pick 'SETTINGS'
foreach ($t in 'Title WoW windows with the character names', 'Animations', 'Check for updates when the app starts') {
  $before = ToggleState $t; SetToggle $t ($before -ne 'On'); SetToggle $t ($before -eq 'On')
  Check "Setting toggles and restores: $t" { (ToggleState $t) -eq $before }
}
# game switch: each game has its own layouts and last layout
Pick 'Forever'; Check 'Game Forever: a Forever layout is selected' { (Settings).forever -eq $true -and [bool](Settings).lastProfileForever -and (Get-Content (Join-Path $profiles ((Settings).lastProfileForever + '.json')) -Raw | ConvertFrom-Json).game -eq 'forever' }
Pick 'Classic Era'; Check 'Game Classic Era: back to the Classic layout' { (Settings).forever -eq $false -and (Settings).lastProfile -like 'UI sweep*' }
Pick 'ABOUT'; Check 'About shows the version' { [bool](Find 'Version') -or [bool](Find 'WINDOW LAYOUTS') }

# ---- clean up ----
# Never delete here: after a delete the app selects a neighbouring layout, which could be one of the user's.
# The caller removes 'UI sweep.json' with the app closed and restores lastProfile = $original.
Pick 'SETUP'
if (-not $KeepLive -and $liveBefore) { SetToggle 'Move WoW windows live' $true }
Check 'No errors in the app log during the sweep' { (LogErrors) -eq 0 }

$results
"`n$($results.Count) checks, $fails failed"
"original layout: $original"
exit $fails
