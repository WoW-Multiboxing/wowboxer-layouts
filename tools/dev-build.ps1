# Builds "WoWBoxer Dev" from whatever is checked out (any branch, uncommitted changes included) and, with -Install,
# installs it next to the normal WoWBoxer. See docs/release-process.md.
#
#   ./tools/dev-build.ps1             # build artifacts/dev/releases/WoWBoxer.Dev-win-Setup.exe
#   ./tools/dev-build.ps1 -Install    # ... and install it (or update the installed dev build); it opens when done
#
# The dev build is its own app as far as Windows is concerned:
#   - Velopack package id WoWBoxer.Dev -> %LOCALAPPDATA%\WoWBoxer.Dev, Start menu/desktop shortcut "WoWBoxer Dev",
#     its own entry in Settings > Apps (uninstall it there; the normal app is not touched);
#   - settings, layouts and logs in %APPDATA%\WoWBoxer\Dev and %LOCALAPPDATA%\WoWBoxer\Dev;
#   - its own single-instance lock, a DEV badge in the title bar, teal icons, and no update check.
# Shared with the normal app, because they belong to Windows or the game: hover to activate, the WoW windows
# themselves and WoW's Config.wtf. Run one of the two at a time (each warns when the other is open).
#
# Version: <Directory.Build.props version>-dev.<yyyyMMddHHmm>, so every build is newer than the last one.
# Needs: .NET 10 SDK, vpk 1.2.161 (dotnet tool install -g vpk --version 1.2.161).
param(
    [switch]$Install,
    [string]$Out = (Join-Path $PSScriptRoot '..\artifacts\dev')
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$Out = [IO.Path]::GetFullPath($Out)

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) { throw 'vpk not found. Install it: dotnet tool install -g vpk --version 1.2.161' }

$props = [xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)
$base = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $base) { throw 'No <Version> in Directory.Build.props.' }
$base = $base.Split('-')[0]
$version = "$base-dev.$(Get-Date -Format 'yyyyMMddHHmm')"
$branch = (git -C $root rev-parse --abbrev-ref HEAD).Trim()
$commit = (git -C $root rev-parse --short HEAD).Trim()
$dirty = if (git -C $root status --porcelain) { ' + uncommitted changes' } else { '' }
Write-Host "WoWBoxer Dev $version from $branch @ $commit$dirty"

$publish = Join-Path $Out 'publish'
$releases = Join-Path $Out 'releases'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
New-Item -ItemType Directory -Force $releases | Out-Null

# Own artifacts path: the dev flavour's obj/bin never mix with the normal build's.
dotnet publish (Join-Path $root 'src/WoWBoxer.Layouts.App') -c Release -r win-x64 --self-contained false `
    --artifacts-path (Join-Path $Out 'build') -o $publish `
    -p:Version=$version -p:WbxDev=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
if (-not (Test-Path (Join-Path $publish 'WoWBoxer.exe'))) { throw 'The publish has no WoWBoxer.exe.' }

vpk pack --packId WoWBoxer.Dev --packVersion $version --packDir $publish `
    --mainExe WoWBoxer.exe --packTitle 'WoWBoxer Dev' --packAuthors 'WoWBoxer' `
    --icon (Join-Path $root 'src/WoWBoxer.Layouts.App/Assets/dev/app.ico') `
    --runtime win-x64 --framework net10.0-x64-desktop -o $releases
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed ($LASTEXITCODE)." }

# Keep the newest few packages only (each is ~10 MB).
Get-ChildItem $releases -Filter 'WoWBoxer.Dev-*-full.nupkg' | Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 | Remove-Item -Force
$setup = Join-Path $releases 'WoWBoxer.Dev-win-Setup.exe'
Write-Host "Setup: $setup"

if ($Install) {
    # Setup installs per user (no admin) into %LOCALAPPDATA%\WoWBoxer.Dev, or updates the dev build already there.
    Get-Process WoWBoxer -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$env:LOCALAPPDATA\WoWBoxer.Dev\*" } | ForEach-Object {
        Write-Host 'Closing the running WoWBoxer Dev first.'
        $_.CloseMainWindow() | Out-Null
        if (-not $_.WaitForExit(10000)) { Stop-Process $_ -Force }
    }
    $p = Start-Process $setup -ArgumentList '--silent' -PassThru -Wait
    if ($p.ExitCode -ne 0) { throw "Setup failed ($($p.ExitCode)). Log: %LOCALAPPDATA%\WoWBoxer.Dev\velopack.log" }
    # Setup --silent installs but does not open the app; open it from its own launcher (as the Start menu does).
    Start-Process (Join-Path $env:LOCALAPPDATA 'WoWBoxer.Dev\WoWBoxer Dev.exe')
    Write-Host "Installed and opened WoWBoxer Dev $version (Start menu: WoWBoxer Dev)."
}
