# CLAUDE.md – WoWBoxer Window Layouts

Free, MIT-licensed Windows app (C# / .NET 10 / WPF) that starts Battle.net and places 2–N World of Warcraft
windows in a chosen layout. Part of the WoWBoxer project (manual multiboxing: one key press = one action on one character).

## Read first
1. `docs/plan.md` – the plan: rules, stack, architecture, v1 scope, milestones M0–M6, test plan.
2. `docs/research-window-program.md` – research behind the decisions (signing, DPI, pitfalls).
3. `reference/ForeverLauncher.ps1` – the working PowerShell prototype. Port its `FmWin` C# class and its rules.
4. `CHANGELOG.md` – what has been done so far.

## Hard rules (never break)
- The app only moves, resizes and restyles windows. **No keystrokes, no mouse events, no input hooks,
  no round-robin, no focus-switch hotkeys, no memory reading, no injection, no login automation.**
- Never steal focus: `SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS`.
- No telemetry. Logs stay local.

## Lessons from the prototype (keep them)
- Find WoW windows by process name (`Wow*.exe`) and path (`\World of Warcraft\`), not only by window class.
  Classic Era 2026: class `waApplication Window`, exe `WowClassic.exe`. Forever: unknown until launch (4 Nov 2026).
- 5:4 minimum aspect before WoW letterboxes (own measurement; make it a setting).
- WoW resizes itself while loading: re-apply placement for ~30 s per new window.
- After moving a borderless window, WoW may keep drawing at the old size (cut-off action bars).
  The prototype's `$ResizeNudge` (shrink 2 px and back) is the candidate fix – still untested.
- Per-Monitor-V2 DPI. Detect admin mismatch (UIPI) and tell the user instead of silently elevating.
- Diagnose report and log file from day one.

## Working rules
- Repo is **private now, public (MIT) at MVP**. Don't commit personal data (account names, paths with user names).
- Owner's reference setup: one monitor, work area 2560×1392, 3 clients (main 1690×1392 left, two 870×696 stacked right).
- Build and run on Windows (Claude Code on the owner's PC). CI on GitHub Actions (`windows-latest`) must stay green.
- Keep `CHANGELOG.md` updated, small commits, clear messages. Code, UI and docs in English.
- The owner's project notes (Danish) live outside this repo in a private project folder
  (path kept in the maintainer's local notes). After each session, add a short `[WS-C]` entry to its `LOG.md`
  and update row C in its `00_Workstreams.md` if you have access.
