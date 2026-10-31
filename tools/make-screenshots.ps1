<#
.SYNOPSIS
  Release-quality screenshots of the app for the website: WebP at 1440x900 and 720x450 plus manifest.json.

.DESCRIPTION
  Runs the app with "--screenshots" (demo mode: made-up 2560x1440 + 1920x1080 monitors, a made-up squad, every window
  "Connected"; nothing real on the PC is read or changed, no network). The app renders each screen itself with WPF's
  renderer, off-screen and without focus, so the pictures do not depend on the screen size or scale - it works on a
  GitHub Actions runner too. This script converts the PNGs to WebP with Python + Pillow and writes manifest.json:
    { "app": "WoWBoxer", "appVersion": "0.5.1", "images": [
      { "file": "setup-main-side.webp", "small": "setup-main-side-720.webp", "title", "alt", "caption",
        "appVersion", "order": 1, "width": 1440, "height": 900, "smallWidth": 720, "smallHeight": 450 }, ... ] }
  "order" = the order they are best shown in. Same version + same Pillow = byte-identical files.

.EXAMPLE
  ./tools/make-screenshots.ps1                                  # build Release, write docs/screenshots/site
  ./tools/make-screenshots.ps1 -Exe publish/app/WoWBoxer.exe -Out out/site-screenshots -Zip out/site-screenshots.zip
#>
param(
    [string]$Exe,
    [string]$Out = (Join-Path $PSScriptRoot '..\docs\screenshots\site'),
    [string]$Zip
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

if (-not $Exe) {
    dotnet build (Join-Path $root 'src\WoWBoxer.Layouts.App') -c Release --nologo -v q | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $Exe = Join-Path $root 'src\WoWBoxer.Layouts.App\bin\Release\net10.0-windows\WoWBoxer.exe'
}
$Exe = (Resolve-Path $Exe).Path

python -c "import PIL" 2>$null
if ($LASTEXITCODE -ne 0) { python -m pip install --quiet --disable-pip-version-check pillow; if ($LASTEXITCODE -ne 0) { throw 'Could not install Pillow.' } }

$png = Join-Path ([IO.Path]::GetTempPath()) ("wowboxer-shots-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $png | Out-Null
try {
    $p = Start-Process $Exe -ArgumentList '--screenshots', "`"$png`"" -PassThru
    if (-not $p.WaitForExit(180000)) { $p.Kill(); throw 'The app did not finish the screenshots within 3 minutes.' }
    if ($p.ExitCode -ne 0 -or -not (Test-Path (Join-Path $png 'shots.json'))) {
        $err = Join-Path $png 'error.txt'
        throw "Screenshots failed (exit $($p.ExitCode)). $(if (Test-Path $err) { Get-Content $err -Raw })"
    }

    $Out = [IO.Path]::GetFullPath($Out)
    New-Item -ItemType Directory -Force $Out | Out-Null
    Get-ChildItem $Out -File | Where-Object { $_.Extension -in '.webp', '.json' } | Remove-Item

    # PNG -> WebP: 1440x900 (quality 90) and a 720x450 thumbnail (Lanczos, quality 88). UI text stays sharp at q90.
    $py = @'
import json, os, sys
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
shots = json.load(open(os.path.join(src, "shots.json"), encoding="utf-8"))
images = []
for order, s in enumerate(shots["shots"], start=1):
    im = Image.open(os.path.join(src, s["file"] + ".png")).convert("RGB")
    full, thumb = s["file"] + ".webp", s["file"] + "-720.webp"
    im.save(os.path.join(out, full), "WEBP", quality=90, method=6)
    small = im.resize((720, round(720 * im.height / im.width)), Image.LANCZOS)
    small.save(os.path.join(out, thumb), "WEBP", quality=88, method=6)
    images.append({"file": full, "small": thumb, "title": s["title"], "alt": s["alt"], "caption": s["caption"],
                   "appVersion": shots["version"], "order": order,
                   "width": im.width, "height": im.height, "smallWidth": small.width, "smallHeight": small.height})
manifest = {"app": "WoWBoxer", "appVersion": shots["version"], "images": images}
with open(os.path.join(out, "manifest.json"), "w", encoding="utf-8", newline="\n") as f:
    json.dump(manifest, f, ensure_ascii=False, indent=2)
    f.write("\n")
print(f"{len(images)} screenshots for v{shots['version']} -> {out}")
'@
    $script = Join-Path $png 'convert.py'
    Set-Content $script $py -Encoding utf8
    python -I $script $png $Out
    if ($LASTEXITCODE -ne 0) { throw 'WebP conversion failed.' }

    if ($Zip) {
        $Zip = [IO.Path]::GetFullPath($Zip)
        if (Test-Path $Zip) { Remove-Item $Zip }
        Compress-Archive -Path (Join-Path $Out '*.webp'), (Join-Path $Out 'manifest.json') -DestinationPath $Zip
        Write-Host "Zip: $Zip"
    }
}
finally {
    Remove-Item -Recurse -Force $png -ErrorAction SilentlyContinue
}
