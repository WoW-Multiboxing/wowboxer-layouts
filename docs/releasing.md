# Releasing

Releases are built only by GitHub Actions (`.github/workflows/release.yml`) from a pushed tag on a commit that is on
main. Nobody uploads files by hand. The order of steps and the rules: [release-process.md](release-process.md).

## Stable release
Usually a promotion of a beta (same commit, see [release-process.md](release-process.md) §5); these steps are what that
commit must contain.
1. Bump `<Version>` in `Directory.Build.props`.
2. In `CHANGELOG.md`, add a section headed exactly `## vX.Y.Z – short title (YYYY-MM-DD)`. Its body becomes the
   GitHub release notes (and Velopack's package notes). The workflow stops if the section is missing. The in-app
   "What's new" window is separate: update `WhatsNewWindow.Changes` for changes a player notices.
3. On a branch → pull request → green CI → merge. Then tag the commit on main: `git tag vX.Y.Z <commit> && git push
   origin vX.Y.Z` (for a promotion, `<commit>` is the beta's: `git rev-list -n 1 vX.Y.Z-beta.N`).
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
- `WoWBoxer.exe --demo` opens the same demo for a look by hand; it runs next to an open WoWBoxer.
- Text uses Segoe UI Variable where Windows has it (Windows 11) and Segoe UI otherwise, so runner pictures can differ
  from local ones by a pixel here and there.

## Beta channel (prereleases)
The order of steps (feature branch → dev build → beta → stable on the same commit) is in
[release-process.md](release-process.md). Mechanics: tag `vX.Y.Z-beta.N` (any version with a `-`) on a commit on main.
The workflow then:
- packs with `--channel beta` (files `WoWBoxer.Layouts-beta-Setup.exe` …, feed `releases.beta.json`), adds
  `WoWBoxer-Beta-Setup.exe` / `WoWBoxer-Beta-Portable.zip`, and publishes a GitHub **prerelease**;
- never becomes `/releases/latest` (checked after publishing), so stable users and the website's download button are
  unaffected;
- takes its notes from `## vX.Y.Z-beta.N` in the CHANGELOG, or else from `## vX.Y.Z`, so the same commit can be tagged
  `vX.Y.Z` later (promotion).

Who gets a beta: installs whose **Settings › Updates › Update channel** is **Beta** (default: the channel the copy was
installed from – Stable, or Beta for `WoWBoxer-Beta-Setup.exe`). Beta reads the `beta` feed (pre-releases) and the
stable `win` feed and takes the newer; Stable reads only `win`. Never a downgrade: back to Stable keeps the installed
beta until a newer stable is out. Check without installing: `dotnet run --project tools/channel-check -- --installed
0.6.0 --setting Beta`.

## Website notification
After publishing, the workflow sends `repository_dispatch` (event type `release-published`, payload
`{repo, tag, version, prerelease, url}`) to `WoW-Multiboxing/wowboxer-site`, whose `refresh-releases` workflow
should listen for it (`on: repository_dispatch: types: [release-published]`). Without the secret the step is skipped
and the site still refreshes every 3 hours.

**Secret `SITE_DISPATCH_TOKEN`** (repo Settings → Secrets and variables → Actions): a fine-grained personal access
token, owner `WoW-Multiboxing`, repository access *Only select repositories* → `wowboxer-site`, permission
**Contents: Read and write** (that is what the dispatches API requires; Metadata: read is added automatically),
expiry 1 year (set a reminder).

## Discord
The last step of `release.yml` posts the release to the WoWBoxer Discord with `tools/discord-announce.mjs` (the WS-H
announcer; one file, Node 18+, no dependencies; the master copy lives in the project folder – re-copy it, never edit it
here). A stable tag goes to **#announcements** with an @Updates ping and links to wowboxer.com/window-app/; a
`vX.Y.Z-beta.N` tag goes to **#beta** with an @Beta testers ping and links to the GitHub pre-release. The post takes
3–5 lines from the tag's CHANGELOG section (a bullet that opens with **bold** is a headline item; lines about Alt+Tab,
"grey zone" or safety claims are dropped). A manual re-run (`workflow_dispatch`) posts nothing unless its **announce**
box is ticked.

- Secrets: `DISCORD_WEBHOOK_ANNOUNCEMENTS`, `DISCORD_WEBHOOK_BETA`, `DISCORD_WEBHOOK_TEST`. Variables (role ids, not
  secret): `DISCORD_ROLE_UPDATES`, `DISCORD_ROLE_BETA`. A missing webhook prints a notice and skips the post; a failed
  post never fails the release (`continue-on-error`).
- Test: Actions → **discord-test** → Run workflow (tag, default `v0.6.1`) posts the same message to the private
  #bot-test with no pings. The log must say "posted to DISCORD_WEBHOOK_TEST". Locally: add `--dry-run` to see the JSON.

## Re-running or fixing a release
Release tags are final: the ruleset "release tags are final" blocks moving or deleting any `v*` tag (no bypass).
- Failed before publishing: fix on a branch → PR → merge, then Actions → release → *Run workflow* with the same
  version only if the tag's commit is still right; otherwise tag the next number (e.g. `-beta.3` or the next patch).
  Delete the draft release by hand if one was left.
- Re-run for an existing tag: Actions → release → *Run workflow* with the version (it builds that tag).
- Never delete a published release that users may have updated to; publish a new patch version instead.

## Repository security (checked 2026-11-02)
- Only the owner account has access (no collaborators, deploy keys or webhooks). Contributions come as pull requests
  from forks and are never given write access. The owner account uses two-factor authentication.
- `main`: ruleset "main is stable" – pull request + green `build` required, no force-push, no deletion, no bypass.
  Tags `v*`: ruleset "release tags are final" – no update, no deletion.
- Actions: workflow token read-only by default (only `release.yml` asks for `contents: write`, and it runs only for
  tags); workflows from forks need approval for every outside contributor; only GitHub's own actions and
  `signpath/github-action-submit-signing-request` may run. Fork PRs run `pull_request`, never `pull_request_target`.
- Secret scanning with push protection, Dependabot alerts and security updates are on. No secrets are stored yet
  (`SITE_DISPATCH_TOKEN` and the SignPath token are added only when needed).
- Merging: GitHub's merge button (and `gh pr merge`) writes the account's personal e-mail into the merge commit, and
  `--author-email` only takes verified addresses. So: make the branch one commit (squash locally) with the noreply
  identity, push it, wait for green CI, then fast-forward `main` to it:
  `git push origin <pr-head-sha>:refs/heads/main` – GitHub counts that as merging the PR (the ruleset allows it,
  because the commit is the open PR's head and its checks passed).

## Code signing
Off until SignPath Foundation approves the project. See [code-signing-policy.md](code-signing-policy.md) for how to
switch it on (repository variables and one secret; the workflow steps are already in place).
