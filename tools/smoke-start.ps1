# Starts the app (stopped, so no window is touched) and fails if it does not stay open with its main window.
# Catches start-up crashes that unit tests miss, e.g. a XAML resource only one view defines (v0.3.2).
# Used by CI (build and release); safe to run locally.
# -DataFolder Dev for the WoWBoxer Dev build (its settings live in %APPDATA%\WoWBoxer\Dev).
param([Parameter(Mandatory)][string]$Exe, [int]$Seconds = 15, [string]$DataFolder = 'Layouts')
$ErrorActionPreference = 'Stop'

# Skip the first-run screen on a clean machine (CI); an existing settings file is left alone.
$settings = Join-Path $env:APPDATA "WoWBoxer\$DataFolder\settings.json"
if (-not (Test-Path $settings)) {
    New-Item -ItemType Directory -Force (Split-Path $settings) | Out-Null
    '{ "firstRunDone": true, "checkForUpdates": false }' | Set-Content -Encoding utf8 $settings
}

$p = Start-Process $Exe -ArgumentList '--pause' -PassThru
# Look for the main window during the whole wait, not only at its end: on a slow runner a dialog that opens over it
# (What's new) can hide it from Process.MainWindowTitle at one moment (flaky v0.6.1-beta.1 release run).
$deadline = (Get-Date).AddSeconds($Seconds)
$title = $null
while ((Get-Date) -lt $deadline -and -not $p.HasExited) {
    Start-Sleep -Milliseconds 500
    $p.Refresh()
    if (-not $title -and $p.MainWindowTitle -like 'WoWBoxer*') { $title = $p.MainWindowTitle }
}
if ($p.HasExited) { throw "The app exited during start-up (exit code $($p.ExitCode)). See the Application event log." }
if (-not $title) { Stop-Process $p -Force; throw 'The app is running but showed no main window.' }
Write-Host "Started fine: '$title'"
Stop-Process $p -Force
