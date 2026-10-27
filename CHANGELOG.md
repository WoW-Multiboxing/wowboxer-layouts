# Changelog

## Unreleased

### v0.2.2 – owner's test of v0.2.1 fixed (2026-10-27)
- **Always on top works both ways:** applied on every placement – also when the window is already in place (the main
  window never became topmost) – and cleared with HWND_NOTOPMOST when switched off (followers stayed on top). Also at
  app start and on every edit with live on. Checked on the real windows with `tools/topmost-check.ps1`.
- **Only the selected game is arranged:** each window's game comes from its install folder (`_classic_era_`,
  `_forever_` …; exe names are shared between games). Layouts remember their game (older files = Classic Era), each
  game remembers its last layout, and the WoW client/account hints follow the game. `wbx --game classic|forever`.
- **Forever client recorded:** `_forever_\Wow.exe`, class `waApplication Window`, starts in Windowed (Fullscreen)
  (`docs/wow-display-findings.md`).
- **Action bar, really solvable:** UI Scale is stored in the shared Config.wtf (verified on our own Forever client).
  "Fix action bar" writes the one UI Scale the squad needs (owner: 0.93) – only while all clients of that install are
  closed, with a backup – or shows the exact in-game steps. "UI Scale off" undoes it; "Shorten window instead" keeps
  the old 4:3 route. The action-bar mark is now measured on the open window (solid dot) instead of guessed (ring);
  "buttons clipped" starts below 1.26 (owner's screenshot; was 1.2). `wbx info`/`wbx snap` print the same verdict.
- **Visible version:** title bar and About show the version (was stuck at 0.1.0), "What's new" once after an update.
- Inspector: a window picker next to the header (keyboard and UI Automation friendly).

### v0.2.1 – "make it work" (unattended bug-hunt run)
- **Classes never guessed:** slots show "Unknown class" (neutral colour) until set. Name, class and WoW account are set
  per row on the Squad tab; the account list and a "last played here" hint (with Use) come read-only from the WTF
  folders. Plan for automatic classes via the WoWBoxer addon: `docs/classes-and-accounts.md`.
- **Drag and drop that stays in sync:** drag a Squad row by ⋮⋮ to move a character (name, class, account and its
  window) to another slot; drop a window onto another on the canvas to swap places. Rows, canvas and – with live on –
  the real windows always follow; Undo works for both.
- **Launch:** Start squad can open N WoW login screens (only the missing ones) instead of Battle.net; "Close clients I
  started" closes only those. Research in `docs/launch-research.md` (Battle.net auto-login is never used: it could
  disconnect a running client).
- **Action bar:** "Full action bar" layout switch, per-window "Fit action bar", in-game UI-scale hint (marked untested).
  UI-scale storage checked read-only (`docs/wow-display-findings.md`).
- **Fixes:** resize nudge left a window 2 px small (now off by default, re-places after); open windows already in a slot
  are matched to it ("Not in place" status); Start squad keeps the rows' order; second start with `--start NAME`
  reaches the running app; drag onto a text box did nothing; unnamed rows did not move real windows.
- **Testing:** `docs/test-plan.md` (full matrix with results), `tools/ui-sweep.ps1` (64 UI Automation checks, all
  passing), `tools/guarded-drag.ps1`; `docs/known-issues.md`, `docs/backlog.md`. 800 unit tests.

### v0.2 – "feels like a game client"
- **New look** after the website and the app mockup v0.2: custom "War Table" theme (navy panels with gold corner
  brackets, Cinzel headings, red/gold "Enter World" buttons, WoW-tooltip boxes, gold switches), custom title bar with
  the shield crest, tabs **Setup · Squad · Guide · Settings · About**, live status in the title bar, page transitions,
  glide animation when windows change place, new shield icon, installer splash and welcome screen.
- **Setup:** I. how many clients (2–8), II. layout cards with live previews of your monitors (Main + side, Overlays,
  Grid, 2 monitors, Free for all – one click applies), III. the canvas: the taskbar is drawn and labelled (the
  "unused strip" was the taskbar), "Cover the taskbar area" with an auto-hide check, only the monitors in use (or all),
  gold number medallions with an action-bar verdict per window, live move of open WoW windows, sticky footer
  (Undo · Reset to preset · saved automatically · Apply to open windows · Start squad), class colour per window.
- **Squad:** one row per character and its window (status Waiting → Placed, Identify, ▲▼ order), a live
  "Start your squad" checklist (Battle.net running, Play with account 1 … N), layout preview, activity log.
- **Guide:** quest-log chapters on a parchment page (prepare WoW, layouts, start, switching windows, troubleshooting,
  what it never does), a helper that opens the Windows hover setting, and a link to the website guide.
- **Exact sizes:** with title bars on, the aspect rule and the presets use the *game area* (window minus the 32 px
  caption/border, scaled with DPI), so "4:3" really gives the game a 4:3 picture.
- Core: Free-for-all preset, optional `class` per slot, `preset` per layout (for Reset); 797 tests.
- Fixes: opening the app never moves windows; the window being edited is drawn above its neighbours.
- Tested on the owner's PC with screenshots, UI Automation and a guarded real mouse drag on the app's own window.

### App (M2–M5)
- **WPF app** with the Windows 11 Fluent dark theme (Mica): monitors drawn to scale, colour-coded slots with size
  and aspect (green = 4:3+, full Classic action bar).
- **Editor:** drag to move, handles to resize, snapping to monitors and other slots (Alt = free), arrow-key nudge,
  Ctrl+arrows resize, Tab selection, numeric X/Y/W/H, always-on-top and keep-size per slot, undo (Ctrl+Z).
- **Layouts:** auto-saved in `%APPDATA%\WoWBoxer\Layouts\profiles`; new from preset, duplicate, rename, delete;
  `WBX1:` codes and `.json` files; capture the current windows.
- **Squad:** Start squad (Battle.net + watcher), Arrange now, reorderable window list, Identify (click-through
  numbers on each WoW window, never activated), give title bars back, Diagnose report, logs.
- **Tray icon**, single instance, `--tray` / `--start NAME`, desktop shortcut per layout, first-run screen.
- **Installer and auto-update** (Velopack, GitHub Releases): `release.yml` builds a 10 MB setup that installs the
  .NET 10 Desktop Runtime when missing. Unsigned until SignPath approves; see `docs/code-signing-policy.md`.
- `docs/privacy.md`: no telemetry; the update check is the only network call.
- Fix: the app's own process (`WoWBoxer.Layouts.exe` starts with "wow") was detected as a WoW client.
  Detection rules moved to Core with tests.
- Shared engine: `SquadPlan`, `Arranger`, `SquadWatcher`, `Diagnostics` in Win32, used by the CLI and the app.

### M1 follow-up
- `wbx snap` (PrintWindow pictures of each client, read-only) and Config.wtf display cvars in diagnose.
- Findings in `docs/wow-display-findings.md`: Classic's action bar is complete only at 4:3 or wider; 1.21 shows no
  black bars (the 5:4 rule is a soft guideline); Config.wtf is shared by all clients.

### M0–M1
- Repo created (private, MIT). Added CLAUDE.md, plan, research and the PowerShell prototype as reference.
- M0: solution skeleton (`WoWBoxer.Layouts.slnx`) with Core, Win32, Cli (`wbx`), App (WPF stub) and Core.Tests (xUnit);
  shared `Directory.Build.props` (nullable, warnings as errors), `global.json` (.NET 10 SDK),
  Per-Monitor-V2 / asInvoker manifests, GitHub Actions build + test on `windows-latest`.
- M1 Core: `wowboxer-layout/1` profile model + JSON, `WBX1:` share codes, validation; layout engine
  (% of work area → pixels, edge rounding, 5:4 shrink-anchor fit, per-slot fit override); presets
  `side`, `overlay`, `grid`, `dual` for 2–8 clients. `side` with 3 clients reproduces the launcher exactly on the
  reference setup (1690×1392 + 2× 870×696). 592 xUnit cases (presets × N × 1080p/1440p/21:9/4K/dual/mixed DPI).
- M1 Win32: port of the launcher's `FmWin` with `LibraryImport` (instead of CsWin32): game windows by class,
  `Wow*.exe` name and `\World of Warcraft\` path; monitors with work area and DPI; borderless; async no-activate
  placement; topmost for overlay slots; resize nudge; elevation check; oldest-first via `GetProcessTimes`.
- M1 CLI: `wbx start | arrange | diagnose | info | layout`. Start = 30 s re-apply per new window, one nudge,
  closed windows free their slot. Admin mismatch (UIPI) is explained, never auto-elevated (unlike the launcher).
  Logs and diagnose reports in `%LOCALAPPDATA%\WoWBoxer\Layouts\logs`, user profile path redacted.
- Field test (3 Classic Era clients, 2560×1392 work area): `wbx diagnose` and `arrange` find and place all
  windows exactly. Removing title bars makes WoW draw action bars/minimap at the wrong size; the 2 px resize
  nudge does not help. **Title bars are now kept by default**; `--borderless` opts in.
- Framed windows are placed by their visible edges (DWM extended frame bounds), so no gaps between them.
- New: `wbx capture --save FILE.json` saves the current arrangement (e.g. made with Windows Snap) as a layout;
  `wbx restore` gives windows their title bars back; `wbx info` shows the size the game renders at.
- Not ported: the launcher's `Shortcut` mode (the app's installer will make shortcuts in M5).
