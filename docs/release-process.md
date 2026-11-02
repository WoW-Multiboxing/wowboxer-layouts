# Release process

How a change gets from an idea to everyone's PC. The mechanics of building a release (workflow, files, Velopack) are
in [releasing.md](releasing.md); this page is the order of steps and the rules.

```
feature/* branch ──► dev build ──► owner's checklist ──► merge to main (green CI) ──► vX.Y.Z-beta.N
   (CI on every push)  (tools/dev-build.ps1)                                         (pre-release, Beta channel)
                                                                                              │  N days, no issues
                                                                                              ▼
                                                              vX.Y.Z on the SAME commit (stable, /releases/latest)
                                                                                              │
                                                                                              ▼
                                                                          website updates itself (download box)
```

## 1. Branches
- **main = stable.** Everything on main may be released. Never commit new work straight to main.
- New work goes on a **`feature/<short-name>`** branch (`fix/<name>` for a bug fix). CI (`build.yml`) runs on every push
  to every branch and on every pull request: build, 2000+ tests, start-up check, the dev flavour, website screenshots.
- Merge to main **only** with green CI **and** the feature's test checklist passed (see 3). Use a pull request, so the
  checks are visible on it: `gh pr create --fill`, then `gh pr merge --squash` (or `--merge`) once green.
- main is protected by a ruleset: no force-push, no deletion. The release workflow refuses tags that are not on main.

## 2. Dev build (side by side)
`./tools/dev-build.ps1 -Install` builds **WoWBoxer Dev** from whatever is checked out (any branch, uncommitted changes
included) and installs it next to the normal app:

| | WoWBoxer | WoWBoxer Dev |
|---|---|---|
| Velopack package / install folder | `WoWBoxer.Layouts` · `%LOCALAPPDATA%\WoWBoxer.Layouts` | `WoWBoxer.Dev` · `%LOCALAPPDATA%\WoWBoxer.Dev` |
| Shortcut, Apps entry | WoWBoxer | WoWBoxer Dev (uninstall it in Settings › Apps) |
| Settings, layouts, logs | `%APPDATA%\WoWBoxer\Layouts`, `%LOCALAPPDATA%\WoWBoxer\Layouts` | `%APPDATA%\WoWBoxer\Dev`, `%LOCALAPPDATA%\WoWBoxer\Dev` |
| Looks | gold icon | teal icon, **DEV** badge in the title bar, "WoWBoxer Dev" in the title and tray |
| Updates | from GitHub (Stable or Beta channel) | never; run the script again for a newer dev build |
| Version | `X.Y.Z` / `X.Y.Z-beta.N` | `X.Y.Z-dev.<yyyyMMddHHmm>` |

Shared, because they belong to Windows or the game: **hover to activate** (one Windows setting – quitting either app
turns it off if "Turn hover off when WoWBoxer quits" is on), the WoW windows and WoW's `Config.wtf`. Run one app at a
time; each warns in the activity line when the other one is open.

## 3. The owner's checklist
Every feature branch gets a short checklist in its pull request description: what to try with the dev build, in the
game where needed, and what "works" looks like. The owner ticks it (or answers in the PR); then it is merged.
Risky features ship behind a switch in **Settings › Experimental** (see 6) and the checklist says to turn it on.

## 4. Beta
1. In the feature's pull request (or a small `release/X.Y.Z` one): bump `<Version>` in `Directory.Build.props` and add
   `## vX.Y.Z – title (date)` to `CHANGELOG.md` (and `WhatsNewWindow.Changes`). A beta uses that section; a
   `## vX.Y.Z-beta.N` section wins if there is one (e.g. for beta.2's extra fixes). Merge it (green CI).
2. Tag the merge commit on main: `git tag vX.Y.Z-beta.1 origin/main && git push origin vX.Y.Z-beta.1`. The workflow builds a GitHub **pre-release** on the
   Velopack channel `beta` (files `WoWBoxer-Beta-Setup.exe` …). It never becomes `/releases/latest`; the workflow checks.
3. Who gets it: installs whose **Settings › Updates › Update channel** is **Beta** (or that were installed from
   `WoWBoxer-Beta-Setup.exe`), at their next start. Stable installs never see it.
4. Check from your PC without installing anything:
   `dotnet run --project tools/channel-check -- --installed 0.6.0 --setting Beta --expect 0.6.1-beta.1`
   (runs the app's own update code against the real GitHub releases for a pretend install).

> **Opting in needs a stable release first.** Installs only see the Update channel setting from v0.6.1 on. Anything a
> stable user needs in order to take part (a new setting, a switch) must reach stable before it can be used: v0.6.0
> users could not choose Beta until v0.6.1 was promoted (2026-11-02). Testers on an older stable can always install
> `WoWBoxer-Beta-Setup.exe` from the pre-release instead (same install, settings kept).

## 5. Stable
After **N days without issues** (default 3; 1 for a tiny change) on the beta:
1. Tag the **same commit** as the beta: `git tag vX.Y.Z <beta-commit> && git push origin vX.Y.Z`
   (find it with `git rev-list -n 1 vX.Y.Z-beta.N`). Same commit = exactly what the testers ran.
2. The workflow builds the stable release on channel `win` and marks it *Latest*. Stable installs get it at their next
   start; beta installs get it too (Beta reads both channels and takes the newer).
3. The website updates itself: the workflow notifies `wowboxer-site` (or its 3-hourly refresh picks it up). Its download
   button is `…/releases/latest/download/WoWBoxer-Setup.exe`, which GitHub serves from the newest non-pre-release only.

## 6. Feature flags (Settings › Experimental)
- A risky feature is registered in `ExperimentalFeatures.All` (`src/WoWBoxer.Layouts.Core/ExperimentalFeatures.cs`)
  with an id, a title and one sentence, and checks `ExperimentalFeatures.IsOn(settings.Experimental, id)` before it does
  anything. It is **off for everyone** until they switch it on, and anyone can switch it off again – no release needed.
- When it has proven itself (a stable release with no issues), remove the check and the entry: it becomes a normal
  feature. Never reuse an id.
- Settings shows "Nothing to try right now" when the list is empty.

## 7. Rollback: ship a fixed version fast
Velopack never downgrades, and a published release must not be deleted (users may have it). So "rollback" = a **new,
higher version** without the problem:
1. Risky feature behind a flag? Tell users to switch it off (Settings › Experimental) – done, then fix calmly.
2. Otherwise: `git revert` the bad change on a `fix/*` branch (or fix it), PR, green CI, merge.
3. Bump the patch version, CHANGELOG section, tag `vX.Y.(Z+1)` directly (no beta needed for a revert) – the workflow
   notes "a direct stable release". Installs get it at their next start.
4. A bad **beta** only: tag `vX.Y.Z-beta.(N+1)` with the fix; stable users never saw it. Optionally edit the bad
   pre-release's notes to say "replaced by beta.N+1" (do not delete it).

## 8. Promote a beta (summary)
```
git fetch --tags
git tag v0.6.1 $(git rev-list -n 1 v0.6.1-beta.1)
git push origin v0.6.1
gh run watch          # stable release, /releases/latest = v0.6.1, website notified
```
