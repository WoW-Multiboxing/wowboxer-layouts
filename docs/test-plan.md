# Test plan

How the app is tested, and the result of the last full run. Rule for all tests: only the app's own window is driven
(UI Automation, or a guarded real mouse that aborts if the cursor is over any other process). Nothing is ever sent to
a game window; game windows are only checked by position (`wbx info`) and pictures (`wbx snap`, read-only).

Tools:
- `dotnet test` – Core unit tests (layout maths, presets, JSON, editing, WTF hints, PNG, detection rules).
- `tools/ui-sweep.ps1` – UI Automation sweep over every tab (70 checks). Turns "live" off while it runs.
- `tools/topmost-check.ps1` – toggles "Always on top" per slot through the app and reads WS_EX_TOPMOST on the real windows.
- `tools/guarded-drag.ps1` – real mouse drag that aborts the moment the cursor is over any other process.
- `wbx info` / `wbx snap` – exact window rectangles / pictures of the game windows.

Last full run: **2026-10-27 evening, v0.2.2, owner's PC** (monitor 1 2560×1440, monitor 2 1920×1080, 3 Classic Era
clients, plus a Forever client we started and closed ourselves). Before each test that moves windows the PC had to be
idle ≥ 3 min (the owner was using it); the owner's exact window positions and topmost flags were saved before and
restored afterwards. Previous run: v0.2.1 the same afternoon.
Legend: ✅ passed · ⚠️ passed with a note · ❌ failed (see known issues) · ⏸ not testable here

## v0.2.2 checks (owner's report on v0.2.1)
| Item | Result |
|---|---|
| Always on top, every slot (main included), toggled on/off 3× through the app → WS_EX_TOPMOST on the real window, and saved in the layout | ✅ 18/18 (`tools/topmost-check.ps1`) |
| App start restores "on top" of open windows without moving them (main became topmost, follower 3 cleared, positions unchanged) | ✅ |
| Hover-to-activate: setting read (off on this PC; "raise on hover" on); topmost band rules documented | ⚠️ not switched on – that is the owner's Windows setting |
| Forever window open + Classic Era selected → header "3 WoW windows open", Arrange leaves Forever at 0,0 2560×1440 | ✅ |
| Game switch Classic Era ↔ Forever → own layouts ("My Forever squad", `game: forever`), Trio back on return | ✅ |
| Forever facts: `_forever_\Wow.exe`, class `waApplication Window`, Windowed (Fullscreen), re-maximises after a move | ✅ recorded |
| UI Scale storage: Forever client closed normally rewrote Config.wtf and kept `useUiScale`/`uiScale` → shared Config.wtf | ✅ (file restored byte-identical) |
| Fix action bar, Classic Era with 3 clients running → refuses, prints 0.93 + in-game steps, Config.wtf byte-identical | ✅ |
| Fix action bar, Forever (no client) → writes `useUiScale 1`, `uiScale 0.92`, backup = original; UI Scale off → `useUiScale 0` | ✅ (restored afterwards) |
| Action-bar mark measured on the open windows: main 1726×1360 "Gryphon ends clipped", followers 830×664 "Buttons clipped" – matches `wbx snap` and the owner's screenshot | ✅ |
| Bar actually complete in Classic Era after UI Scale 0.93 | ⏸ needs the owner to restart all clients once |
| Version in title bar ("v0.2.2") and window title; What's new shown once, then not again | ✅ |
| `publish\app` and `publish\wbx` rebuilt = 0.2.2 | ✅ |

## Layout engine (unit tests, 826 cases)
| Area | Cases | Result |
|---|---|---|
| Presets side / overlay / grid / dual / free × 2–8 clients × 1080p, 1440p@125 %, 21:9, 4K@150 %, dual, dual mixed DPI | inside work area, no overlap (except overlays/free), followers ≥ 5:4 game area, valid profile | ✅ |
| Reference setup (launcher numbers) | 1690×1392 + 2× 870×696 | ✅ |
| Title bars | game area = window − 2×32 px (×DPI); "4:3" gives a 4:3 game area (888×696 window) | ✅ |
| Editing | drag, resize, min size, snap (move keeps size), snap lines, re-home to another monitor, clamp | ✅ |
| JSON / WBX1 codes | round trip incl. class + preset, plan example, readable errors | ✅ |
| WTF hints | accounts, realms, characters newest first, leftovers skipped, missing install | ✅ |
| Detection | WowClassic yes; WoWBoxer.Layouts, wbx, WowVoiceProxy, BlizzardError no; install-path fallback | ✅ |
| Game from install folder | era, forever, anniversary, retail, beta, unknown; layouts keep their game, old files = Classic Era | ✅ |
| UI Scale | read/write Config.wtf lines, scale for full bar (0.93 / 0.95), squad = smallest, verdict thresholds | ✅ |

## Setup tab (UI sweep)
| Item | Result |
|---|---|
| Clients 2–8 × each card (Main + side, Overlays, Grid, 2 monitors, Free for all) → saved layout has that count and preset | ✅ 35/35 |
| "2 monitors" card disabled with one monitor | ⏸ (PC has two monitors; logic covered by unit test) |
| Full action bar on/off → min aspect 4:3 / 1.25 | ✅ |
| Cover the taskbar area on/off | ✅ (hint text depends on auto-hide; this PC: taskbar always shown) |
| All monitors on/off | ✅ |
| Undo (4 clients → back to 3), Reset to preset | ✅ |
| Shorten window instead (window 1 → 4:3 game area) | ✅ |
| Window picker in the inspector selects a window | ✅ |
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
| UI Scale fixes the bar (storage verified: shared Config.wtf) | ⏸ one restart of the owner's clients after "Fix action bar" |
| Direct `WowClassic.exe` start → WoW login screen, no effect on running sessions | ✅ |
| Resize nudge leaves window 2 px small (owner's run 16:02) | ❌→✅ fixed: nudge off by default, re-place after nudge |
