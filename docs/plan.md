# WoWBoxer Window Layouts – plan for the Windows desktop app

> 2026-10-27. Status: **M0–M4 done, M5 built (installer + auto-update; signing waits for SignPath), v0.2 UI done (game-client look after the app mockup). M6 field test with Forever from 4–5 Nov.** Builds on `01_Research/platform/03-window-program.md` (stack, signing, pitfalls) and on what the PowerShell launcher taught us (`03_Output/Multibox-Launcher/`).
> Written in English because it goes to Claude Code and into a public (MIT) repo.

## 1. Goal
A small, free, open-source Windows app that **starts Battle.net and places 2–N WoW windows** in a layout you choose. You can pick a recommended preset or drag your own layout, on one or more monitors. It **never sends input to the game**.

**Product goal:** a real, modern desktop app that is clearly better than WowOpenBox – Windows 11 look (dark theme,
Mica), visual monitor canvas with drag-and-drop layouts, presets, one-click "Start squad", Identify & assign, tray,
first-run screen, installer + auto-update. The CLI (`wbx`) is only the engine and test harness.

## 2. Hard rules (compliance)
- Only move, resize and restyle windows. No keystrokes, no mouse events, no input hooks, no round-robin, no focus-switch hotkeys in v1.
  Exception (owner, 2026-10-28): a switch for Windows' own hover-to-activate setting – a Windows setting, not input; off by default, only on the user's click, original values kept and restored. Since 2026-10-29 it is *the* WoWBoxer way to switch windows; the UI states facts only (a Windows setting, sends nothing to the game) – no warning notice, never "approved", "allowed by Blizzard" or "100% safe".
- No reading of game memory, no injection, no login automation. The player presses Play themselves, once per account.
- The app shows a short "what it does / never does" screen on first run and links to the WoWBoxer rules page.
- Optional later features (e.g. a "swap main window" hotkey) are off by default and clearly labelled.

## 3. Decisions (from research)
| Topic | Decision | Why |
|---|---|---|
| Stack | **C# / .NET 10 LTS + WPF**, P/Invoke via source-generated `LibraryImport` | Reuses the launcher's C# (`FmWin`). WPF Canvas suits a drag/resize editor. Per-Monitor-V2 DPI. .NET 8/9 reach end of support 10 Nov 2026. (M1: `LibraryImport` instead of CsWin32 – ~30 calls, a near 1:1 port of `FmWin`, no extra package. Revisit if the API surface grows.) |
| Installer and updates | **Velopack** + GitHub Releases | One installer with auto-update and delta packages |
| Code signing | **SignPath Foundation** (free for OSI open source) | Azure Artifact Signing is not open to Danish individuals. Unsigned files trigger a strong SmartScreen warning. |
| Licence | MIT | Agreed 2026-10-27 |
| Name | "WoWBoxer Window Layouts" (exe `WoWBoxer.Layouts.exe`) | the owner's brand choice. Trademark risk is noted in LOG. |
| Where to build | **Claude Code on the owner's Windows PC** | Cowork has no Windows or .NET. Claude Code can build, test and run there. |

## 4. How it works
```
Monitors ──► Layout profile (JSON, % per monitor) ──► Slot rectangles in pixels (PMv2)
WoW windows found (class GxWindowClass* / waApplication Window, process Wow*, path \World of Warcraft\)
     └─► assigned to slots (launch order, or "identify & assign")
     └─► placed: SetWindowPos (async, no activate), borderless, re-applied while WoW loads (~30 s)
```
**Layout profile (shared with the website's layout designer):**
```json
{ "format": "wowboxer-layout/1", "name": "Overlay 5", "clients": 5,
  "slots": [
    { "n": 1, "role": "main", "monitor": "primary", "x": 0, "y": 0, "w": 100, "h": 100, "z": "bottom" },
    { "n": 2, "role": "follower", "monitor": "primary", "x": 8.3, "y": 73, "w": 19, "h": 23, "z": "top" } ],
  "options": { "borderless": true, "minAspect": 1.25, "fit": "shrink-anchor", "useTaskbarArea": false } }
```
- Positions are in % of a monitor's work area, so one profile works on any resolution. `monitor` is "primary", an index, or a saved device ID.
- **Built-in presets for N = 2–8:**
  - **Main + side:** like today's launcher.
  - **Overlays:** the main client fills the screen, and the followers float in a row along the bottom (as in the mockup).
  - **Grid.**
  - **Main on monitor 1, followers on monitor 2.**
- **Share code:** `WBX1:` + base64 of the JSON, to copy between the website, the app and friends.

## 5. Lessons the app must keep (from the launcher)
- Find windows by **process name and path**, not only by window class. Classic Era's class is `waApplication Window`; Forever's is unknown until launch.
- **5:4 minimum aspect** before WoW letterboxes. This is our own measurement, so make it a profile setting.
- WoW resizes itself while loading, so **re-apply placement for about 30 s** per new window.
- Use **Per-Monitor-V2 DPI**, `SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS`, and never steal focus.
- **Admin mismatch (UIPI):** detect it and *tell the user* to start Battle.net without admin rights. Auto-elevating the app is a last resort, because antivirus dislikes it.
- **Logs and Diagnose** from day one: a text file the user can send.
- **Must test – overlays plus hover-to-activate:** when the mouse activates the main window, Windows may bring it to the front and hide the overlays. Fix by making overlay windows *topmost* (per-slot `z: "top"`). Test with Windows' hover setting (and its "raise" behaviour).
- Tip for the guide: raise WoW's *Background FPS* (`maxFPSBk`) so follower windows stay smooth.
- **Open question:** "Windowed" vs "Windowed (Fullscreen)". Our README and WowOpenBox disagree, so test both in Forever.

## 6. Version 1 scope
**v1 (MVP):**
1. **Monitor view:** detects monitors, resolution, DPI and taskbar, and draws them to scale.
2. **Presets:** pick a squad size (2–8) and a preset; see a live preview.
3. **Editor:**
   - drag and resize slots on the canvas
   - snap to edges and to other slots
   - arrow-key nudge
   - 5:4 warning
   - numbering and colour per slot (class colours optional)
4. **Profiles:** save, rename and duplicate. Import/export via `WBX1:` code or a .json file.
5. **Start squad:**
   - starts Battle.net if it isn't running
   - shows "press Play with account 1, 2, 3 …" (account names optional)
   - places each window as it appears
6. **Arrange now:** places the open windows by launch order.
7. **Identify & assign:** shows a big number on each WoW window for a few seconds (our own click-through overlay), so the user can reorder the assignment in the app.
8. **Tray icon** (Arrange now, Profiles, Quit). The app starts minimised to the tray when launched from a shortcut.
9. **Diagnose:** writes a report (monitors, windows, classes, processes, elevation, test move).
10. **First-run screen:** what the app does and never does, with a link to the rules page.

**Later (v1.1+):**
- per-profile Config.wtf hints (CVars)
- an optional "swap main" hotkey, off by default
- a picture-in-picture preview
- locale (Danish)
- winget package
- reading layout codes straight from the website

## 7. Repo and project structure
```
wowboxer-layouts/                      (GitHub, MIT, public)
  src/WoWBoxer.Layouts.Core/           layout maths, profile JSON, presets   (no WPF; unit-tested)
  src/WoWBoxer.Layouts.Win32/          window finding, placing, DPI, monitors, elevation (CsWin32)
  src/WoWBoxer.Layouts.App/            WPF UI: monitor canvas, editor, profiles, tray, first-run
  src/WoWBoxer.Layouts.Cli/            "wbx arrange|diagnose|start" (headless, same core; replaces the .cmd files)
  tests/WoWBoxer.Layouts.Core.Tests/   xUnit: presets for N=2–8 on 1080p/1440p/21:9/4K/dual, 5:4 fit, rounding, JSON round-trip
  docs/                                user guide, code-signing policy (required by SignPath), privacy (no telemetry)
  .github/workflows/                   build + test on every push; release: Velopack pack → SignPath sign → GitHub Release
```

## 8. Milestones (rough, in Claude Code sessions)
| # | Milestone | Done when | Est. |
|---|---|---|---|
| M0 | Set-up | Repo, .NET 10 SDK, solution skeleton, CI green, MIT licence, README | 1 |
| M1 | Core + CLI | Presets and layout maths with tests. CLI `arrange`/`diagnose`/`start` matches the PowerShell launcher on the owner's PC (Classic Era, 3 windows). | 2 |
| M2 | App shell | WPF window, monitor canvas to scale, preset picker with live preview, profiles saved | 2 |
| M3 | Editor | Drag/resize/snap/nudge, 5:4 warning, import/export `WBX1:` | 2 |
| M4 | Squad flow | Start squad, watcher with 30 s re-apply, Identify & assign, tray, first-run screen | 2 |
| M5 | Ship | Velopack installer + auto-update, SignPath application and signed release, download on the website | 1–2 (+ SignPath approval time) |
| M6 | Field test | the owner's 2–5 clients in Forever on 1 and 2 monitors, including overlays + hover setting. Bugs fixed. | 1–2 |

Total: about 10–13 working sessions. M0–M2 can start before launch; M4/M6 need the Forever client.

## 9. Test plan
- **Unit (CI):** every preset × N = 2–8 × common monitor setups; 5:4 fit; no slot outside its work area; JSON round-trip.
- **Manual matrix:**

  | Area | Cases |
  |---|---|
  | Client | Classic Era, Forever |
  | Windows | 2, 3, 5 |
  | Screen | 1080p 100 %, 1440p 125 %, 4K 150 %, 2 monitors with mixed DPI |
  | WoW display mode | Windowed / Windowed (Fullscreen) |
  | Admin | Battle.net normal / as admin |
  | Hover setting | On / off |

- Every release ships with Diagnose, so bug reports always include the facts.

## 10. Next steps (in order)
1. **The owner, before the first Claude Code session (about 30 min):**
   - Install **Git** and the **.NET 10 SDK** (or Visual Studio 2026 Community with ".NET desktop").
   - Create a **new GitHub repo** `wowboxer-layouts` (public, MIT) under WoW-Multiboxing.
   - Create a local folder outside OneDrive, e.g. `D:\Claude lokalt\wowboxer-layouts`. Keeping it out of OneDrive avoids sync fights with `bin/obj`.
   - Create the project folder `<private project folder>` (Claude can do this) for docs and the log.
2. **Claude Code session 1:** M0 + start of M1. Use the kickoff prompt below.
3. **At Forever launch (4–5 Nov):**
   - Run the existing PowerShell launcher + `Diagnose-Forever.cmd`.
   - Note Forever's exe name and window class, and test the display mode.
   - Feed the results into M1.
4. **Apply to SignPath** as soon as the repo has CI and a first release candidate. Approval can take time.
5. **Website:** add the "Window app" page and the layout designer to the site plan, using the same `wowboxer-layout/1` JSON.

## 11. Kickoff prompt for Claude Code (copy-paste)
```
You are building "WoWBoxer Window Layouts", a free MIT-licensed Windows app (C#/.NET 10, WPF) that starts
Battle.net and places 2–N World of Warcraft windows in a chosen layout. It must NEVER send input to the game.
Read first (paths in this repo): docs/plan.md (this plan),
docs/research-window-program.md (research), and reference/ForeverLauncher.ps1
(the working PowerShell prototype; port its FmWin class and its rules: process/path detection, 5:4 aspect,
30 s re-apply, PMv2 DPI, no focus stealing, admin detection, logs/diagnose).
Today: milestone M0 + M1. Create the solution structure from section 7 in this repo, CI on GitHub Actions,
Core with presets (side, overlay, grid, dual-monitor) for N=2–8 + xUnit tests, and a CLI
(wbx arrange | diagnose | start) that reproduces the PowerShell launcher. Run the tests, then let me try
"wbx diagnose" and "wbx arrange" with my Classic Era clients. Keep a CHANGELOG and update docs as you go.
```
