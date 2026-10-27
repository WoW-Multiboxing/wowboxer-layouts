# Test plan

How the app is tested, and the result of the last full run. Rule for all tests: only the app's own window is driven
(UI Automation, or a guarded real mouse that aborts if the cursor is over any other process). Nothing is ever sent to
a game window; game windows are only checked by position (`wbx info`) and pictures (`wbx snap`, read-only).

Tools:
- `dotnet test` – Core unit tests (layout maths, presets, JSON, editing, WTF hints, PNG, detection rules).
- `tools/ui-sweep.ps1` – UI Automation sweep over every tab (64 checks). Turns "live" off while it runs.
- `tools/guarded-drag.ps1` – real mouse drag that aborts the moment the cursor is over any other process.
- `wbx info` / `wbx snap` – exact window rectangles / pictures of the game windows.

Last full run: **2026-10-27, owner's PC** (monitor 1 2560×1440, monitor 2 1920×1080, 3 Classic Era clients).
Legend: ✅ passed · ⚠️ passed with a note · ❌ failed (see known issues) · ⏸ not testable here

## Layout engine (unit tests, 800 cases)
| Area | Cases | Result |
|---|---|---|
| Presets side / overlay / grid / dual / free × 2–8 clients × 1080p, 1440p@125 %, 21:9, 4K@150 %, dual, dual mixed DPI | inside work area, no overlap (except overlays/free), followers ≥ 5:4 game area, valid profile | ✅ |
| Reference setup (launcher numbers) | 1690×1392 + 2× 870×696 | ✅ |
| Title bars | game area = window − 2×32 px (×DPI); "4:3" gives a 4:3 game area (888×696 window) | ✅ |
| Editing | drag, resize, min size, snap (move keeps size), snap lines, re-home to another monitor, clamp | ✅ |
| JSON / WBX1 codes | round trip incl. class + preset, plan example, readable errors | ✅ |
| WTF hints | accounts, realms, characters newest first, leftovers skipped, missing install | ✅ |
| Detection | WowClassic yes; WoWBoxer.Layouts, wbx, WowVoiceProxy, BlizzardError no; install-path fallback | ✅ |

## Setup tab (UI sweep)
| Item | Result |
|---|---|
| Clients 2–8 × each card (Main + side, Overlays, Grid, 2 monitors, Free for all) → saved layout has that count and preset | ✅ 35/35 |
| "2 monitors" card disabled with one monitor | ⏸ (PC has two monitors; logic covered by unit test) |
| Full action bar on/off → min aspect 4:3 / 1.25 | ✅ |
| Cover the taskbar area on/off | ✅ (hint text depends on auto-hide; this PC: taskbar always shown) |
| All monitors on/off | ✅ |
| Undo (4 clients → back to 3), Reset to preset | ✅ |
| Fit action bar (window 1 → 4:3 game area) | ✅ |
| Save as, Rename (file moves), Delete (confirm) | ✅ |
| Copy code → clipboard `WBX1:`; Paste code → new layout | ✅ |
| Open file / Save file (Windows file dialogs) | ⏸ manual – OS dialogs |
| Capture current windows | ✅ (v0.1 run) |
| Canvas: drag a window (real mouse) → saved, WoW windows not moved with live off; Undo restores | ✅ |
| Canvas: drop window 2 on window 3 → places swap in layout **and** on screen (live); Undo swaps back | ✅ |
| Canvas: edited window drawn on top; taskbar strip drawn; only used monitors unless "All monitors" | ✅ |
| Opening the app never moves windows | ✅ (fixed in v0.2) |

## Squad tab
| Item | Result |
|---|---|
| Rows = slots; name/class/WoW account editable; "Unknown class" until set | ✅ |
| WoW account list from WTF folders; "Last played here" hint + Use | ✅ (5 accounts found; hint is labelled as hint) |
| First look: windows already in a slot are matched to it (was launch order → wrong "Waiting") | ✅ fixed |
| Status: No window / Placed / Not in place / Check Activity | ✅ |
| Drag row 2 onto row 3 (real mouse) → rows, layout and real windows swap | ✅ (first failed twice: text box swallowed the drop; unnamed rows did not trigger live – both fixed) |
| Launch mode Battle.net / Login screens saved | ✅ |
| Start squad, Login screens, 4-client layout with 3 clients open → opens exactly 1 login screen, placed in slot 4 | ✅ |
| Close clients I started → closes only that one; owner's 3 clients untouched | ✅ |
| Start squad keeps the rows' order (was launch order) | ✅ fixed |
| Checklist ticks (Battle.net running, account rows) | ✅ |
| Identify / Identify all (click-through numbers, never activated) | ✅ (v0.1/v0.2 runs) |
| Arrange now / Apply to open windows | ✅ |
| Give title bars back, Diagnose report | ✅ |

## Other tabs and app life
| Item | Result |
|---|---|
| Guide: 6 chapters, hover-setting helper, website link | ✅ (link opens a placeholder URL) |
| Settings: switches toggle and persist; game exe found automatically; re-read accounts | ✅ |
| About | ✅ |
| Restart: last layout, tab, live, launch mode remembered | ✅ |
| Second start with `--tray --start NAME` while running → running app starts that layout | ✅ fixed (args were dropped) |
| Single instance (second start only shows the first) | ✅ |
| Tray menu (open, start, arrange, identify, quit) | ⏸ right-click on the tray needs real input on the shell – not automated |
| First run screen | ✅ |
| Installer (vpk pack, 10.7 MB, splash) | ✅ built; ⏸ not installed on the owner's PC |
| Monitor change while running (plug/unplug, DPI change) | ⏸ needs hardware change |
| 1 monitor only | ⏸ (unit-tested) |

## Game-side checks
| Item | Result |
|---|---|
| Windows placed exactly (`wbx info`) incl. title-bar frame correction | ✅ |
| Action bar complete only at 4:3 game area; clipped ends below (`wbx snap`) | ✅ (see wow-display-findings.md) |
| UI Scale / Edit Mode bar scale per follower fixes the bar | ⏸ needs one in-game change by the owner |
| Direct `WowClassic.exe` start → WoW login screen, no effect on running sessions | ✅ |
| Resize nudge leaves window 2 px small (owner's run 16:02) | ❌→✅ fixed: nudge off by default, re-place after nudge |
