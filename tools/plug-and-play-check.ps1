# RUN IT, NEVER DOT-SOURCE IT (it starts right away):  powershell -ExecutionPolicy Bypass -File tools\plug-and-play-check.ps1
# Always watching, end to end, with 5 FAKE WoW clients (a scratch "World of Warcraft\_classic_era_" folder in %TEMP%,
# tools\fakewow\FakeWow.cs) on MONITOR 2 only. The real clients (up to 4) are never moved: while the 5 fakes are open
# they are the bigger install, so the squad follows them and the real windows are "ignored"; at the end the fakes close
# and the squad follows the real clients again. Every step that moves windows or opens a client first checks that the
# squad really is the fake install, and stops (paused) if not. UI Automation on our own app only; fakes never take focus.
$ErrorActionPreference = 'Stop'
$S = Join-Path $env:TEMP 'wowboxer-fakewow'   # fake install and backups
$fakeExe = "$S\World of Warcraft\_classic_era_\WowClassic.exe"
if (-not (Test-Path $fakeExe)) {
  New-Item -ItemType Directory -Force (Split-Path $fakeExe) | Out-Null
  & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe "/out:$fakeExe" "$PSScriptRoot\fakewow\FakeWow.cs"
}
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class T {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
  public struct RECT { public int L, T, R, B; }
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int n);
  public static string Raw(IntPtr h) { RECT r; GetWindowRect(h, out r); return r.L + "," + r.T + " " + (r.R - r.L) + "x" + (r.B - r.T); }
  public static string Rect(IntPtr h) { RECT r; if (DwmGetWindowAttribute(h, 9, out r, 16) != 0) GetWindowRect(h, out r); return r.L + "," + r.T + " " + (r.R - r.L) + "x" + (r.B - r.T); }
  public static bool Top(IntPtr h) { return (GetWindowLong(h, -20) & 8) != 0; }
}
'@
$A = [System.Windows.Automation.AutomationElement]; $Scope = [System.Windows.Automation.TreeScope]
$exeApp = (Resolve-Path (Join-Path $PSScriptRoot '..\publish\app\WoWBoxer.Layouts.exe')).Path
$profiles = "$env:APPDATA\WoWBoxer\Layouts\profiles"; $settingsPath = "$env:APPDATA\WoWBoxer\Layouts\settings.json"
$results = New-Object System.Collections.Generic.List[string]; $fails = 0
function Check($what, [scriptblock]$t) { try { $ok = & $t } catch { $ok = $false; $what += " ($($_.Exception.Message))" }; if ($ok) { $results.Add("PASS  $what") } else { $results.Add("FAIL  $what"); $script:fails++ } }
function Main { @($A::RootElement.FindAll($Scope::Children, [System.Windows.Automation.Condition]::TrueCondition)) | Where-Object { $_.Current.Name -like 'WoWBoxer Window Layouts*' } | Select-Object -First 1 }
function Find($name, $root = (Main)) { $root.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name))) }
function FindId($id, $root = (Main)) { $root.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::AutomationIdProperty, $id))) }
function Invoke($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 500 }
function Tab($n) { (Find $n).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500 }
function Header { (FindId 'HeaderStatus').Current.Name }
function Primary { (FindId 'PrimaryButton').Current.Name }
function States { @((Main).FindAll($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::AutomationIdProperty, 'RowState'))) | ForEach-Object { $_.Current.Name }) -join ',' }
function Fakes { @(Get-Process WowClassic -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $fakeExe } | Sort-Object StartTime) }
function Real { @(Get-Process WowClassic, Wow -ErrorAction SilentlyContinue | Where-Object { $_.Path -notlike "$S*" }) }
function Hwnd($p) { $p.Refresh(); $p.MainWindowHandle }
function StartFake($i) { $p = Start-Process $fakeExe -ArgumentList (2600 + 50 * $i), (60 + 50 * $i), 640, 480, "`"Fake $i`"" -PassThru; Start-Sleep -Milliseconds 700; $p }
function SendArg($arg) { Start-Process $exeApp -ArgumentList $arg; Start-Sleep 2 }
function PickLayout($name) {
  $combo = Find 'Saved layout'; $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 400
  $cond = New-Object System.Windows.Automation.AndCondition (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name)), (New-Object System.Windows.Automation.PropertyCondition ($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))
  $item = $combo.FindFirst($Scope::Descendants, $cond); $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 300
  $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse(); Start-Sleep -Milliseconds 500
}
function Row($n) { Find "Window $n" }
# Safety: anything that moves windows or opens a client runs only while the squad is the FAKE install.
function Guard($step) {
  $exe = (Get-Content $settingsPath -Raw | ConvertFrom-Json).gameExe
  if ($exe -notlike "$S*") { Start-Process $exeApp -ArgumentList '--pause'; Start-Sleep 2; throw "ABORTED before '$step': the squad follows $exe, not the fake install" }
}

# ---- set up: backups, test layouts on monitor 2, app restarted paused ----
$stamp = Get-Date -Format 'HHmmss'; $bak = "$S\backup-$stamp"; New-Item -ItemType Directory $bak | Out-Null
Copy-Item $settingsPath $bak; Copy-Item $profiles "$bak\profiles" -Recurse
$ownerLayout = (Get-Content $settingsPath -Raw | ConvertFrom-Json).lastProfile
$realBefore = Real | ForEach-Object { "$($_.Id) $([T]::Raw((Hwnd $_)))" }
function Slot($n, $role, $x, $y, $w, $h) { [ordered]@{ n = $n; role = $role; monitor = '2'; x = $x; y = $y; w = $w; h = $h; z = $(if ($role -eq 'main') { 'bottom' } else { 'normal' }); anchor = 'top' } }
function Layout($name, $slots) { [ordered]@{ format = 'wowboxer-layout/1'; name = $name; clients = $slots.Count; slots = $slots; options = [ordered]@{ borderless = $false; minAspect = 0; fit = 'shrink-anchor'; useTaskbarArea = $false; resizeNudge = $false } } | ConvertTo-Json -Depth 5 | Set-Content "$profiles\$name.json" -Encoding utf8 }
Layout 'WBX test 3 (monitor 2)' @((Slot 1 'main' 0 0 60 100), (Slot 2 'follower' 60 0 40 50), (Slot 3 'follower' 60 50 40 50))
Layout 'WBX test 5 (monitor 2)' @((Slot 1 'main' 0 0 40 100), (Slot 2 'follower' 40 0 60 25), (Slot 3 'follower' 40 25 60 25), (Slot 4 'follower' 40 50 60 25), (Slot 5 'follower' 40 75 60 25))
if (@(Real).Count -ge 5) { throw "too many real clients for this test" }
$app = Get-Process WoWBoxer.Layouts -ErrorAction SilentlyContinue; if ($app) { [void]$app.CloseMainWindow(); [void]$app.WaitForExit(10000) }
Start-Process $exeApp -ArgumentList '--pause' -WindowStyle Normal; Start-Sleep 7
Check "app starts paused: '$(Header)'" { (Header) -like 'Paused*' }

try {
  # ---- 4 fakes: the bigger install becomes the squad; real windows ignored ----
  $f = 1..5 | ForEach-Object { StartFake $_ }
  Start-Sleep 2
  Guard 'pick test layout'
  PickLayout 'WBX test 3 (monitor 2)'
  Check "test layout selected while paused (no moves): $((Get-Content $settingsPath -Raw | ConvertFrom-Json).lastProfile)" { (Get-Content $settingsPath -Raw | ConvertFrom-Json).lastProfile -eq 'WBX test 3 (monitor 2)' }
  Check "real windows reported as ignored: '$((Find 'Include').Current.Name)'" { [bool](Find 'Include') }
  $fakeRects = $f | ForEach-Object { [T]::Raw((Hwnd $_)) }
  Check "paused: no fake moved ($($fakeRects -join ' | '))" { $fakeRects[0] -eq '2650,110 640x480' -and $fakeRects[1] -eq '2700,160 640x480' }
  Guard 'resume'
  SendArg '--resume'; Start-Sleep 3
  $r = $f | ForEach-Object { [T]::Rect((Hwnd $_)) }
  Check "resume: fakes 1-3 placed on monitor 2, fakes 4+5 extra ($($r -join ' | ')); header '$(Header)'; rows $(States)" { (Header) -eq 'All 3 connected' -and $r[0] -like '2560,0 *' -and [T]::Raw((Hwnd $f[3])) -eq '2800,260 640x480' -and (States) -eq 'Connected,Connected,Connected' }
  Check "extra window notice" { [bool](Find '2 extra Classic Era windows left as they are – more windows than slots') }
  Check "primary button is RE-ARRANGE: '$(Primary)'" { (Primary) -eq 'RE-ARRANGE' }

  # ---- a closed window frees its slot; the waiting extra window takes it ----
  $slot2 = $r[1]; Stop-Process -Id $f[1].Id -Force; Start-Sleep 4
  Guard 'after closing fake 2'
  Check "fake 2 closed: fake 4 moves into slot 2 ($([T]::Rect((Hwnd $f[3]))) vs $slot2); header '$(Header)'" { [T]::Rect((Hwnd $f[3])) -eq $slot2 -and (Header) -eq 'All 3 connected' }

  # ---- Open N WoW clients: a 4-slot layout with 3 windows → OPEN 1 WOW CLIENT opens one login screen of the squad's install ----
  PickLayout 'WBX test 5 (monitor 2)'; Start-Sleep 3
  Check "5 slots, 4 windows: header '$(Header)', button '$(Primary)', rows $(States)" { (Header) -eq 'Awaiting clients · 4 of 5 connected' -and (Primary) -eq 'OPEN 1 WOW CLIENT' -and (States) -like '*Awaiting…' }
  Guard 'Open 1 WoW client'
  Invoke (FindId 'PrimaryButton'); Start-Sleep 5
  $opened = Fakes | Where-Object { $f.Id -notcontains $_.Id }
  Check "OPEN 1 WOW CLIENT started exactly one client of the fake install ($(@($opened).Count)) and it was placed: '$(Header)'" { @($opened).Count -eq 1 -and (Header) -eq 'All 5 connected' }
  # pause: a window moved by hand stays; resume puts it back
  SendArg '--pause'
  $h1 = Hwnd $f[0]; [void][T]::SetWindowPos($h1, [IntPtr]::Zero, 2600, 500, 500, 400, 0x14)   # NOZORDER|NOACTIVATE
  Start-Sleep 3
  Check "paused: a moved window stays moved ($([T]::Raw($h1))), row 1 '$((States).Split(',')[0])'" { [T]::Raw($h1) -eq '2600,500 500x400' -and (States).StartsWith('Moved') }
  Guard 'resume after pause'
  SendArg '--resume'; Start-Sleep 3
  Check "resume: it is back in slot 1 ($([T]::Rect($h1))), header '$(Header)'" { [T]::Rect($h1) -like '2560,0 *' -and (Header) -eq 'All 5 connected' }
  # always on top on a slot, both ways
  Invoke (Row 2).FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, 'Details')))
  $cb = Find 'Always on top'; $cb.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep 1
  $inSlot2 = (Fakes | Where-Object { [T]::Rect((Hwnd $_)) -like '3328,0 *' } | Select-Object -First 1)
  Check "Always on top on: the window in slot 2 is topmost" { $inSlot2 -and [T]::Top((Hwnd $inSlot2)) }
  $cb.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep 1
  Check "Always on top off: cleared" { -not [T]::Top((Hwnd $inSlot2)) }
  # close the login screen we opened
  Tab 'SETTINGS'
  Invoke (Find 'Close clients I started'); Start-Sleep 3
  Tab 'SETUP'
  Check "Close clients I started: only ours closed ($(@(Fakes).Count) fakes left), header '$(Header)'" { @(Fakes).Count -eq 4 -and (Header) -eq 'Awaiting clients · 4 of 5 connected' }
}
finally {
  # ---- back to the owner's setup: pause, his layout, fakes closed, files restored, resume ----
  SendArg '--pause'
  try { PickLayout $ownerLayout } catch { $results.Add("WARN  could not pick $($ownerLayout): $_") }
  Fakes | Stop-Process -Force; Start-Sleep 3
  Remove-Item "$profiles\WBX test 3 (monitor 2).json", "$profiles\WBX test 5 (monitor 2).json" -ErrorAction SilentlyContinue
  $realAfter = Real | ForEach-Object { "$($_.Id) $([T]::Raw((Hwnd $_)))" }
  Check "owner's real windows never moved: $($realBefore -join ' | ')" { ($realBefore -join '|') -eq ($realAfter -join '|') }
  Check "squad follows the real clients again: '$(Header)'" { (Header) -like 'Paused*' -and -not (Find 'Include') }
  SendArg '--resume'; Start-Sleep 3
  Check "resumed on the owner's layout: '$(Header)'" { (Header) -match '^(All \d connected|Awaiting clients)' }
  $realEnd = Real | ForEach-Object { "$($_.Id) $([T]::Raw((Hwnd $_)))" }
  Check "owner's windows still where they were after resume" { ($realBefore -join '|') -eq ($realEnd -join '|') }
  $results
  "`n$($results.Count) checks, $fails failed (backup in $bak)"
}

