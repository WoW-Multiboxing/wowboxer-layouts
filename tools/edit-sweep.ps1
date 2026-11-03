<#
  RUN IT, NEVER DOT-SOURCE IT:  powershell -ExecutionPolicy Bypass -File tools\edit-sweep.ps1 [-Exe path\WoWBoxer.exe]
  UI Automation sweep of Advanced editing, Custom and the "Paste a layout code" dialog – in DEMO mode
  (WoWBoxer.exe --demo: made-up monitors and windows, its own temporary settings; nothing real is read or moved,
  and it runs next to an open WoWBoxer). Drives only the demo window through UI Automation (no mouse, no keys).
  Checks the results in the demo's own layout and draft files.
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\WoWBoxer.Layouts.App\bin\Release\net10.0-windows\WoWBoxer.exe'),
    [string]$Wbx = (Join-Path $PSScriptRoot '..\src\WoWBoxer.Layouts.Cli\bin\Release\net10.0-windows\wbx.exe')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class EsWin {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  public static List<IntPtr> Windows(uint pid) { var r = new List<IntPtr>(); EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); var sb = new StringBuilder(256); GetWindowText(h, sb, 256); if (p == pid && IsWindowVisible(h) && sb.Length > 0) r.Add(h); return true; }, IntPtr.Zero); return r; }
  public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
}
'@
$A = [System.Windows.Automation.AutomationElement]; $Scope = [System.Windows.Automation.TreeScope]
$results = New-Object System.Collections.Generic.List[string]; $script:fails = 0
function Check($what, [scriptblock]$test) {
    try { $ok = & $test } catch { $ok = $false; $what += " ($($_.Exception.Message))" }
    if ($ok) { $results.Add("PASS  $what") } else { $results.Add("FAIL  $what"); $script:fails++ }
}

$proc = Start-Process $Exe -ArgumentList '--demo' -PassThru
$root = Join-Path $env:TEMP "WoWBoxer-demo-$($proc.Id)\roaming"
function MainWin {
    for ($i = 0; $i -lt 60; $i++) {
        $h = [EsWin]::Windows([uint32]$proc.Id) | Where-Object { [EsWin]::Title($_) -like 'WoWBoxer*[0-9]*' } | Select-Object -First 1
        if ($h) { return $A::FromHandle($h) }
        Start-Sleep -Milliseconds 250
    }
    throw 'demo window not found'
}
function Dialog($title) {
    for ($i = 0; $i -lt 30; $i++) {
        $h = [EsWin]::Windows([uint32]$proc.Id) | Where-Object { [EsWin]::Title($_) -eq $title } | Select-Object -First 1
        if ($h) { return $A::FromHandle($h) }
        Start-Sleep -Milliseconds 200
    }
    $null
}
function Find($name, $in = (MainWin)) { $in.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name))) }
function FindId($id, $in = (MainWin)) { $in.FindFirst($Scope::Descendants, (New-Object System.Windows.Automation.PropertyCondition ($A::AutomationIdProperty, $id))) }
function Pat($e, $p) { $o = $null; if ($e -and $e.TryGetCurrentPattern($p, [ref]$o)) { $o } else { throw "no pattern on $($e.Current.Name)" } }
function Invoke($e) { (Pat $e ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Milliseconds 500 }
function Press($name) { $e = Find $name; if (-not $e) { throw "not found: $name" }; Invoke $e }
function Toggle($name, $on) { $e = Find $name; $t = Pat $e ([System.Windows.Automation.TogglePattern]::Pattern); if (($t.Current.ToggleState -eq 'On') -ne $on) { $t.Toggle(); Start-Sleep -Milliseconds 500 } }
function Card($name) {
    $cond = New-Object System.Windows.Automation.AndCondition (New-Object System.Windows.Automation.PropertyCondition ($A::NameProperty, $name)), (New-Object System.Windows.Automation.PropertyCondition ($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button))
    $e = (MainWin).FindFirst($Scope::Descendants, $cond)
    if (-not $e) { throw "no card $name" }
    (Pat $e ([System.Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep -Milliseconds 900
}
function Text($id) { (FindId $id).Current.Name }
function Layout { $n = (Get-Content "$root\settings.json" -Raw | ConvertFrom-Json).lastProfile; Get-Content "$root\profiles\$n.json" -Raw | ConvertFrom-Json }
function Draft { $n = (Get-Content "$root\settings.json" -Raw | ConvertFrom-Json).lastProfile; $f = "$root\drafts\$n.json"; if (Test-Path $f) { Get-Content $f -Raw | ConvertFrom-Json } else { $null } }
function Inside($inner, $outer) { $i = $inner.Current.BoundingRectangle; $o = $outer.Current.BoundingRectangle; $i.Left -ge $o.Left -and $i.Right -le $o.Right -and $i.Top -ge $o.Top -and $i.Bottom -le $o.Bottom -and $i.Width -gt 0 }

try {
    $null = MainWin
    Start-Sleep 3

    # ---- Advanced editing toolbar ----
    Check 'the ribbon is hidden while Advanced editing is off' { (FindId 'EditRibbon') -eq $null -or (FindId 'EditRibbon').Current.IsOffscreen }
    Toggle 'Advanced editing' $true
    Check 'Advanced editing opens the ribbon' { -not (FindId 'EditRibbon').Current.IsOffscreen }
    $buttons = 'Select all', 'Clear selection', 'Same width', 'Same height', 'Same size', 'Align left', 'Align centre', 'Align right', 'Align top', 'Align middle', 'Align bottom',
        'Distribute horizontally', 'Distribute vertically', 'Fill row', 'Fill column', 'Fit 4:3', 'Swap when dropped on another window', 'Snap', 'Show grid', 'Lock aspect ratio', 'Undo', 'Redo',
        'Fill gap', 'Selection X', 'Selection Y', 'Selection width', 'Selection height'
    $missing = @($buttons | Where-Object { -not (Find $_) })
    Check "every toolbar control is there ($($buttons.Count))$(if ($missing) { ': missing ' + ($missing -join ', ') })" { $missing.Count -eq 0 }
    $noTip = @($buttons | Where-Object { $e = Find $_; $e -and -not $e.Current.HelpText })
    Check "every toolbar control has a tooltip$(if ($noTip) { ': ' + ($noTip -join ', ') })" { $noTip.Count -eq 0 }
    Check 'Swap when dropped is off in Advanced editing' { (Pat (Find 'Swap when dropped on another window') ([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -eq 'Off' }
    Check 'Same size needs two windows: disabled with nothing selected' { -not (Find 'Same size').Current.IsEnabled }

    Press 'Select all'
    Check 'Select all: 3 windows, a reference' { (Text 'SelectionText') -like '3 windows*reference*' }
    $before = Layout
    Check 'the layout starts as Main + side' { $before.preset -eq 'side' }
    Press 'Fill row'
    $after = Layout
    Check 'Fill row: one row, same height, edge to edge' { @($after.slots | ForEach-Object { $_.y } | Select-Object -Unique).Count -eq 1 -and [math]::Abs((($after.slots | Measure-Object w -Sum).Sum) - 100) -lt 0.05 }
    Check 'editing a preset switched it to Custom' { $after.preset -eq 'custom' }
    Check 'the Custom draft is saved (unsaved changes)' { (Draft).unsaved -eq $true }
    Check 'the inspector shows the shared Y and H, blank X' { (Pat (Find 'Selection Y') ([System.Windows.Automation.ValuePattern]::Pattern)).Current.Value -ne '' -and (Pat (Find 'Selection X') ([System.Windows.Automation.ValuePattern]::Pattern)).Current.Value -eq '' }
    Press 'Undo'
    Check 'Undo: back to Main + side' { (Layout).preset -eq 'side' }
    Press 'Redo'
    Check 'Redo: the filled row again' { (Layout).preset -eq 'custom' -and @((Layout).slots | ForEach-Object { $_.y } | Select-Object -Unique).Count -eq 1 }
    $custom = Layout

    # ---- leaving Custom asks once ----
    Card 'Main + side'
    $d = Dialog 'Save your custom layout?'
    Check 'leaving Custom with unsaved changes asks "Save your custom layout?"' { $d -ne $null }
    if ($d) {
        Check 'the question offers Save as…, Keep as draft, Discard' { (Find 'Save as…' $d) -and (Find 'Keep as draft' $d) -and (Find 'Discard' $d) }
        Invoke (Find 'Keep as draft' $d)
    }
    Check 'Keep as draft: Main + side now' { (Layout).preset -eq 'side' }
    Check 'Keep as draft: the draft is still there' { (Draft) -ne $null -and (Draft).unsaved -eq $false }
    Card 'Custom'
    Check 'the Custom card brings the draft back (no dialog, nothing jumps)' { (Layout).preset -eq 'custom' -and ((Layout).slots[1].x -eq $custom.slots[1].x) -and -not (Dialog 'Save your custom layout?') }
    Card 'Grid'
    Check 'a kept draft does not ask again' { -not (Dialog 'Save your custom layout?') -and (Layout).preset -eq 'grid' }
    Card 'Custom'
    Press 'Select all'; Press 'Align left'   # the draft is a row: align left really changes it
    Card 'Overlays'
    $d = Dialog 'Save your custom layout?'
    Check 'a new change asks again when leaving' { $d -ne $null }
    if ($d) { Invoke (Find 'Discard' $d) }
    Check 'Discard: Overlays, no draft left' { (Layout).preset -eq 'overlay' -and (Draft) -eq $null }

    # ---- Paste a layout code ----
    $code = (& $Wbx layout --preset side --clients 10 | Where-Object { $_ -match '^\s*WBX1:' } | Select-Object -First 1).Trim()
    Press 'More layout actions'
    Press 'Paste a layout code…'
    $d = Dialog 'Paste a layout code'
    Check 'the import dialog opens' { $d -ne $null }
    if ($d) {
        $box = FindId 'ShareCodeText' $d
        $ok = FindId 'ShareCodeOk' $d
        Check 'OK is disabled until a valid code is pasted' { -not $ok.Current.IsEnabled }
        (Pat $box ([System.Windows.Automation.ValuePattern]::Pattern)).SetValue('not a code'); Start-Sleep -Milliseconds 300
        Check 'a clear error for a wrong code' { (FindId 'ShareCodeCheck' $d).Current.Name -like '*starts with*WBX1*' -and -not $ok.Current.IsEnabled }
        (Pat $box ([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($code); Start-Sleep -Milliseconds 300
        Check "a long code ($($code.Length) characters) is checked live" { (FindId 'ShareCodeCheck' $d).Current.Name -eq 'Valid WBX1 code · 10 clients · Main + side' -and $ok.Current.IsEnabled }
        Check 'the box, OK and Cancel stay inside the dialog' { (Inside $box $d) -and (Inside $ok $d) -and (Inside (Find 'Cancel' $d) $d) -and (Inside (FindId 'ShareCodePaste' $d) $d) }
        Check 'the dialog keeps its width' { $d.Current.BoundingRectangle.Width -lt 700 }
        Invoke $ok
        Start-Sleep -Milliseconds 600
        Check 'OK imports the layout (10 clients)' { (Layout).clients -eq 10 }
    }
    Check 'no error in the demo log' { -not (Test-Path (Join-Path $env:TEMP "WoWBoxer-demo-$($proc.Id)\logs\app.log")) -or -not (Select-String -Path (Join-Path $env:TEMP "WoWBoxer-demo-$($proc.Id)\logs\app.log") -Pattern ' Error ' -Quiet) }
} catch {
    $results.Add("FAIL  stopped: $($_.Exception.Message)"); $script:fails++
} finally {
    if (-not $proc.HasExited) { Stop-Process $proc -Force }
    Start-Sleep -Milliseconds 500
    Remove-Item (Join-Path $env:TEMP "WoWBoxer-demo-$($proc.Id)") -Recurse -Force -ErrorAction SilentlyContinue
}
$results | ForEach-Object { Write-Host $_ }
Write-Host ("{0} checks failed" -f $script:fails)
exit $script:fails
