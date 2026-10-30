# Releasing

Releases are built only by GitHub Actions (`.github/workflows/release.yml`) from a pushed tag. Nobody uploads files
by hand.

## Stable release
1. Bump `<Version>` in `Directory.Build.props`.
2. In `CHANGELOG.md`, add a section headed exactly `## vX.Y.Z – short title (YYYY-MM-DD)`. Its body becomes the
   GitHub release notes (and Velopack's package notes). The workflow stops if the section is missing. The in-app
   "What's new" window is separate: update `WhatsNewWindow.Changes` for changes a player notices.
3. Commit, then `git tag vX.Y.Z && git push origin main vX.Y.Z`.
4. Watch the run: `gh run watch` (about 4 minutes).

The workflow tests, publishes app + `wbx` (framework-dependent, win-x64), packs with Velopack (delta against the
previous release of the same channel), uploads to a **draft** release, adds the extra files and the notes, then
publishes it as *Latest*. Last, it tells the website (see below).

### Files on every release
| File | What |
|---|---|
| `WoWBoxer-Setup.exe` | **Stable name** – the website links `…/releases/latest/download/WoWBoxer-Setup.exe` |
| `WoWBoxer-Portable.zip` | Stable name for the portable zip (no install, no auto-update) |
| `WoWBoxer.Layouts-win-Setup.exe`, `WoWBoxer.Layouts-win-Portable.zip` | The same files under Velopack's own names (kept so older links work) |
| `WoWBoxer.Layouts-X.Y.Z-full.nupkg`, `-delta.nupkg` (from the second release on), `releases.win.json`, `RELEASES` | Velopack update feed. Installed apps read these |
| `SHA256SUMS.txt` | SHA-256 of every `.exe`, `.zip` and `.nupkg` |
| `site-screenshots.zip` | The website's screenshot set for this version: `*.webp` (1440×900 and `-720` 720×450) + `manifest.json` (see below) |

`https://github.com/WoW-Multiboxing/wowboxer-layouts/releases/latest/download/<file>` always serves the newest
**non-prerelease**, so the stable names never need changing on the website.

### Installer behaviour (Velopack)
- Installs per user into `%LOCALAPPDATA%\WoWBoxer.Layouts` with Start-menu and desktop shortcuts. **No admin rights.**
- `--framework net10.0-x64-desktop`: Setup checks for the .NET 10 Desktop Runtime (x64) and, if missing, downloads
  Microsoft's installer and runs it before installing the app. The runtime is machine-wide, so **that step alone
  shows a UAC prompt**. On a PC that already has the runtime there is no prompt at all.
- Setup options: `--silent`, `--log <file>`, `--installto <dir>`. Do **not** pass app arguments after `--`:
  Velopack 1.2.161's Setup panics on them (`Mismatch between definition and access of EXE_ARGS`) and hangs, found
  2026-10-28. After install, Setup always starts the app once.
- Updates: the app checks GitHub at start (if enabled), downloads in the background and applies the update when
  the app quits.

### Website screenshots
`tools/make-screenshots.ps1` starts the app with `--screenshots` – **demo mode**: made-up monitors (2560×1440 primary +
1920×1080), a made-up squad (names and classes), every window "Connected"; nothing real is read or changed (no
windows, Windows settings, Config.wtf, processes or network; settings and logs go to a temporary folder). The app renders
each screen itself with WPF's renderer, off-screen and without focus, so it works the same on a GitHub runner as on a
PC. The script converts the pictures to WebP and writes `manifest.json`:
`{ app, appVersion, images: [ { file, small, title, alt, caption, appVersion, order, width, height, smallWidth, smallHeight } ] }`.

- The set is committed in `docs/screenshots/site/` (run the script after UI changes; it writes there by default).
- `build.yml` renders it on every push (artifact `site-screenshots`), so a break shows up before a release.
- `release.yml` renders it again from the release's own build into `docs/screenshots/site/` and attaches
  `site-screenshots.zip`. If rendering ever fails on the runner, the committed set is attached instead (warning in the log).
- `WoWBoxer.Layouts.exe --demo` opens the same demo for a look by hand; it runs next to an open WoWBoxer.
- Text uses Segoe UI Variable where Windows has it (Windows 11) and Segoe UI otherwise, so runner pictures can differ
  from local ones by a pixel here and there.

## Beta channel (prereleases)
Tag `vX.Y.Z-beta.N` (any version with a `-`). The workflow then:
- packs with `--channel beta` (files `WoWBoxer.Layouts-beta-Setup.exe` …, feed `releases.beta.json`), adds
  `WoWBoxer-Beta-Setup.exe` / `WoWBoxer-Beta-Portable.zip`, and publishes a GitHub **prerelease**;
- never becomes `/releases/latest`, so stable users and the website's download button are unaffected.

An install made from a beta Setup follows the beta channel (the app reads its channel and includes prereleases when
checking). Stable installs never see betas. To move a tester back to stable: uninstall, install `WoWBoxer-Setup.exe`.
The CHANGELOG needs a `## vX.Y.Z-beta.N` section too.

## Website notification
After publishing, the workflow sends `repository_dispatch` (event type `release-published`, payload
`{repo, tag, version, prerelease, url}`) to `WoW-Multiboxing/wowboxer-site`, whose `refresh-releases` workflow
should listen for it (`on: repository_dispatch: types: [release-published]`). Without the secret the step is skipped
and the site still refreshes every 3 hours.

**Secret `SITE_DISPATCH_TOKEN`** (repo Settings → Secrets and variables → Actions): a fine-grained personal access
token, owner `WoW-Multiboxing`, repository access *Only select repositories* → `wowboxer-site`, permission
**Contents: Read and write** (that is what the dispatches API requires; Metadata: read is added automatically),
expiry 1 year (set a reminder).

## Re-running or fixing a release
- Failed before publishing: fix, delete the draft release and the tag (`gh release delete vX.Y.Z --cleanup-tag`),
  tag again.
- Re-run for an existing tag: Actions → release → *Run workflow* with the version.
- Never delete a published release that users may have updated to; publish a new patch version instead.

## Code signing
Off until SignPath Foundation approves the project. See [code-signing-policy.md](code-signing-policy.md) for how to
switch it on (repository variables and one secret; the workflow steps are already in place).
