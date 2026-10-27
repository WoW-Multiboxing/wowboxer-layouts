# Changelog

## Unreleased

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
