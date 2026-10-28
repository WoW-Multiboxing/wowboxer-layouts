# Starts the app (stopped, so no window is touched) and fails if it does not stay open with its main window.
# Catches start-up crashes that unit tests miss, e.g. a XAML resource only one view defines (v0.3.2).
# Used by CI (build and release); safe to run locally.
param([Parameter(Mandatory)][string]$Exe, [int]$Seconds = 15)
$ErrorActionPreference = 'Stop'

# Skip the first-run screen on a clean machine (CI); an existing settings file is left alone.
$settings = Join-Path $env:APPDATA 'WoWBoxer\Layouts\settings.json'
if (-not (Test-Path $settings)) {
    New-Item -ItemType Directory -Force (Split-Path $settings) | Out-Null
    '{ "firstRunDone": true, "checkForUpdates": false }' | Set-Content -Encoding utf8 $settings
}

$p = Start-Process $Exe -ArgumentList '--pause' -PassThru
$deadline = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $deadline -and -not $p.HasExited) { Start-Sleep -Milliseconds 500; $p.Refresh() }
if ($p.HasExited) { throw "The app exited during start-up (exit code $($p.ExitCode)). See the Application event log." }
if (-not $p.MainWindowTitle) { Stop-Process $p -Force; throw 'The app is running but shows no main window.' }
Write-Host "Started fine: '$($p.MainWindowTitle)'"
Stop-Process $p -Force
