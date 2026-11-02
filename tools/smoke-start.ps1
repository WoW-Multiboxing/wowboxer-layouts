# Starts the app (stopped, so no window is touched) and fails if it does not stay open with its main window.
# Catches start-up crashes that unit tests miss, e.g. a XAML resource only one view defines (v0.3.2).
# Used by CI (build and release); safe to run locally.
# -DataFolder Dev for the WoWBoxer Dev build (its settings live in %APPDATA%\WoWBoxer\Dev).
param([Parameter(Mandatory)][string]$Exe, [int]$Seconds = 30, [string]$DataFolder = 'Layouts')
$ErrorActionPreference = 'Stop'

# Skip the first-run screen on a clean machine (CI); an existing settings file is left alone.
$settings = Join-Path $env:APPDATA "WoWBoxer\$DataFolder\settings.json"
if (-not (Test-Path $settings)) {
    New-Item -ItemType Directory -Force (Split-Path $settings) | Out-Null
    '{ "firstRunDone": true, "checkForUpdates": false }' | Set-Content -Encoding utf8 $settings
}

# Every visible top-level window title of a process (Process.MainWindowTitle shows only one of them, and while the
# What's new dialog is open it may pick that one - the flaky v0.6.1-beta.1 release run).
Add-Type @"
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class SmokeWindows {
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  public static List<string> Titles(int pid) {
    var list = new List<string>();
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); if (sb.Length > 0) list.Add(sb.ToString()); }
      return true; }, IntPtr.Zero);
    return list;
  }
}
"@

$p = Start-Process $Exe -ArgumentList '--pause' -PassThru
$deadline = (Get-Date).AddSeconds($Seconds)
$title = $null
while ((Get-Date) -lt $deadline -and -not $p.HasExited) {
    Start-Sleep -Milliseconds 500
    $p.Refresh()
    if (-not $title) { $title = [SmokeWindows]::Titles($p.Id) | Where-Object { $_ -match '^WoWBoxer( Dev)? \d' } | Select-Object -First 1 }
}
if ($p.HasExited) { throw "The app exited during start-up (exit code $($p.ExitCode)). See the Application event log." }
$all = [SmokeWindows]::Titles($p.Id) -join "', '"
if (-not $title) { Stop-Process $p -Force; throw "The app is running but showed no main window (windows: '$all')." }
Write-Host "Started fine: '$title' (windows: '$all')"
Stop-Process $p -Force
