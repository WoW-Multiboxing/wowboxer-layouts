# Test plan

How the app is tested, and the result of the last full run. Rule for all tests: only the app's own window is driven
(UI Automation, or a guarded real mouse that aborts if the cursor is over any other process). Nothing is ever sent to
a game window; game windows are only checked by position (`wbx info`) and pictures (`wbx snap`, read-only).

Tools:
- `dotnet test` â€“ Core unit tests (layout maths, presets, JSON, editing, WTF hints, PNG, detection rules).
- `tools/ui-sweep.ps1` â€“ UI Automation sweep over every tab (73 checks). Start the app with `--pause` first (it refuses
  otherwise), so its many layout changes never move a game window.
- `tools/plug-and-play-check.ps1` â€“ always watching end to end with 5 fake WoW windows (`tools/fakewow/FakeWow.cs`, a
  plain window that never takes focus, in a scratch "World of Warcraft\_classic_era_" folder) on monitor 2 only. The
  fakes outnumber the real clients, so the squad follows them and real windows are left alone; every step that moves or
  opens anything first checks that the squad is the fake install and stops (paused) otherwise. 19 checks.
- `tools/topmost-check.ps1` â€“ toggles "Always on top" per slot through the app (row â–¾ details) and reads WS_EX_TOPMOST on the
  real windows, then 24 disturbance rounds (another program clears/sets on-top, a window moves, focus switches between
  our own windows) with slots 2+3 on top.
- `tools/guarded-drag.ps1` â€“ real-mouse drags inside our own window only (squad row reorder).
- `tools/guarded-drag.ps1` â€“ real mouse drag that aborts the moment the cursor is over any other process.
- `wbx info` / `wbx snap` â€“ exact window rectangles / pictures of the game windows.

Last full run: **2026-10-27 ~23:00, v0.2.4, owner's PC** â€“ the owner was playing the whole time, so every test that
moves windows ran on fake windows on monitor 2 (see the incident note below). v0.2.3 run: ~21:05 (owner said "go ahead"; windows and settings saved before and
restored after). Previous run: v0.2.2 at ~20:10, below. v0.2.2 run: **owner's PC** (monitor 1 2560Ã—1440, monitor 2 1920Ã—1080, 3 Classic Era
clients, plus a Forever client we started and closed ourselves). Before each test that moves windows the PC had to be
idle â‰¥ 3 min (the owner was using it); the owner's exact window positions and topmost flags were saved before and
restored afterwards. Previous run: v0.2.1 the same afternoon.
Legend: âœ… passed Â· âš ï¸ passed with a note Â· âŒ failed (see known issues) Â· â¸ not testable here

## v0.2.4 checks (plug and play)
| Item | Result |
|---|---|
| Unit tests (incl. squad install: most windows, tie stays, tie at start = opened first) | âœ… 830 |
| UI sweep (paused app): no game selector / live switch / Full action bar / All monitors, one primary button, rows + chips, â–¾ details open and stay open, window rules + Auto-fit + tools in Settings, activity from the status bar, Fix action bar refuses with clients running, layouts Ã— counts, undo/reset, share codes, rename/delete, guide, settings | âœ… 73/73 |
| Real clients: the owner's 3 Classic Era windows are found at start, keep their slots: "All 3 connected", RE-ARRANGE | âœ… (screenshot `setup-after-v0.2.4.png`) |
| Mixed installs: a fake Forever window â†’ "1 Forever window ignored Â· Include", not moved; Include â†’ "1 extra Forever window left as it is" (slots full), still not moved | âœ… |
| Paused start: no window moved; resume â†’ fakes 1â€“3 placed in their slots on monitor 2, fakes 4â€“5 extra | âœ… |
| A closed window frees its slot; the waiting extra window moves into it | âœ… |
| 5 slots / 4 windows â†’ "Awaiting clients Â· 4 of 5 connected", button OPEN 1 WOW CLIENT â†’ exactly one client of the squad's install opens and is placed â†’ "All 5 connected"; Close clients I started closes only that one | âœ… |
| Paused: a window moved by hand stays (row "Moved"); resume puts it back | âœ… |
| Always on top via â–¾ details, 4 rounds on/off: WS_EX_TOPMOST follows within 200 ms both ways | âœ… 8/8 |
| Real windows untouched through the fake-window tests (positions before = after, also after resume) | âœ… |
| Squad row drag (guarded real mouse) + Undo, on fake windows | see below |
| Alt-Tab / clicking / hover between real WoW clients | â¸ owner â€“ the tests never activate a WoW window |

**Incident during the first fake-window runs (22:37â€“22:42), fixed:** the first test version used 4 fakes against the
owner's 3 clients. Closing one fake made it 3 vs 3, the squad switched back to the real install mid-test, and the next
steps moved the owner's 3 real windows onto the test layout on monitor 2 (for under a minute, twice) and opened a real
Classic Era login screen (twice; both closed again by us, nothing logged in). Everything was put back (positions, layout,
settings byte-identical). Fixes: the squad now keeps its install on a tie, the test uses 5 fakes, and every moving step
checks the squad's install first and stops paused otherwise.

## v0.2.3 checks (owner's report on v0.2.2)
| Item | Result |
|---|---|
| UI Scale 0.93 (owner pressed Fix action bar, restarted clients) â†’ both 830Ã—664 followers show the whole bar incl. gryphons + backpack (snapshots); main too | âœ… in game |
| Auto-fit on a fake Classic Era install (scratch folder, copy of ping.exe as WowClassic.exe): app start â†’ 0.93 + backup + notice; while a client runs â†’ nothing; last client closes â†’ 0.93; before Start squad â†’ 0.93 | âœ… 4/4 |
| Auto-fit never touches Forever (bar rule not verified there) and never writes while a client of the install runs | âœ… (code + owner's real Config.wtf unchanged by the tests) |
| Always on top, live mode OFF: every slot toggled on/off 3Ã— â†’ WS_EX_TOPMOST follows at once | âœ… 18/18 |
| Guard: 24 disturbances (cleared/set by another program, moved 3 px, foreground switched Notepad â†” app), slots 2+3 on top â†’ corrected within 1 s | âœ… 24/24 |
| Squad row drag (guarded real mouse) row 2 â†’ row 1: real windows swap, on-top follows the slot; Undo puts windows and rows back | âœ… (found + fixed: the list swallowed the drag; Undo did not restore window order) |
| No false "set by something else" log after our own changes | âœ… fixed |
| One screen: rows select the slot, details inline, footer launch/apply/start, no Squad tab | âœ… (UI sweep) |
| Alt-Tab / clicking / hover-to-activate between real WoW clients | â¸ owner â€“ the tests never activate a WoW window |

## v0.2.2 checks (owner's report on v0.2.1)
| Item | Result |
|---|---|
| Always on top, every slot (main included), toggled on/off 3Ã— through the app â†’ WS_EX_TOPMOST on the real window, and saved in the layout | âœ… 18/18 (`tools/topmost-check.ps1`) |
| App start restores "on top" of open windows without moving them (main became topmost, follower 3 cleared, positions unchanged) | âœ… |
| Hover-to-activate: setting read (off on this PC; "raise on hover" on); topmost band rules documented | âš ï¸ not switched on â€“ that is the owner's Windows setting |
| Forever window open + Classic Era selected â†’ header "3 WoW windows open", Arrange leaves Forever at 0,0 2560Ã—1440 | âœ… |
| Game switch Classic Era â†” Forever â†’ own layouts ("My Forever squad", `game: forever`), Trio back on return | âœ… |
| Forever facts: `_forever_\Wow.exe`, class `waApplication Window`, Windowed (Fullscreen), re-maximises after a move | âœ… recorded |
| UI Scale storage: Forever client closed normally rewrote Config.wtf and kept `useUiScale`/`uiScale` â†’ shared Config.wtf | âœ… (file restored byte-identical) |
| Fix action bar, Classic Era with 3 clients running â†’ refuses, prints 0.93 + in-game steps, Config.wtf byte-identical | âœ… |
| Fix action bar, Forever (no client) â†’ writes `useUiScale 1`, `uiScale 0.92`, backup = original; UI Scale off â†’ `useUiScale 0` | âœ… (restored afterwards) |
| Action-bar mark measured on the open windows: main 1726Ã—1360 "Gryphon ends clipped", followers 830Ã—664 "Buttons clipped" â€“ matches `wbx snap` and the owner's screenshot | âœ… |
| Bar actually complete in Classic Era after UI Scale 0.93 | â¸ needs the owner to restart all clients once |
| Version in title bar ("v0.2.2") and window title; What's new shown once, then not again | âœ… |
| `publish\app` and `publish\wbx` rebuilt = 0.2.2 | âœ… |

## Layout engine (unit tests, 826 cases)
| Area | Cases | Result |
|---|---|---|
| Presets side / overlay / grid / dual / free Ã— 2â€“8 clients Ã— 1080p, 1440p@125 %, 21:9, 4K@150 %, dual, dual mixed DPI | inside work area, no overlap (except overlays/free), followers â‰¥ 5:4 game area, valid profile | âœ… |
| Reference setup (launcher numbers) | 1690Ã—1392 + 2Ã— 870Ã—696 | âœ… |
| Title bars | game area = window âˆ’ 2Ã—32 px (Ã—DPI); "4:3" gives a 4:3 game area (888Ã—696 window) | âœ… |
| Editing | drag, resize, min size, snap (move keeps size), snap lines, re-home to another monitor, clamp | âœ… |
| JSON / WBX1 codes | round trip incl. class + preset, plan example, readable errors | âœ… |
| WTF hints | accounts, realms, characters newest first, leftovers skipped, missing install | âœ… |
| Detection | WowClassic yes; WoWBoxer.Layouts, wbx, WowVoiceProxy, BlizzardError no; install-path fallback | âœ… |
| Game from install folder | era, forever, anniversary, retail, beta, unknown; layouts keep their game, old files = Classic Era | âœ… |
| UI Scale | read/write Config.wtf lines, scale for full bar (0.93 / 0.95), squad = smallest, verdict thresholds | âœ… |

## Setup tab (UI sweep)
| Item | Result |
|---|---|
| Clients 2â€“8 Ã— each card (Main + side, Overlays, Grid, 2 monitors, Free for all) â†’ saved layout has that count and preset | âœ… 35/35 |
| "2 monitors" card disabled with one monitor | â¸ (PC has two monitors; logic covered by unit test) |
| Full action bar on/off â†’ min aspect 4:3 / 1.25 | âœ… |
| Cover the taskbar area on/off | âœ… (hint text depends on auto-hide; this PC: taskbar always shown) |
| All monitors on/off | âœ… |
| Undo (4 clients â†’ back to 3), Reset to preset | âœ… |
| Shorten window instead (window 1 â†’ 4:3 game area) | âœ… |
| Window picker in the inspector selects a window | âœ… |
| Save as, Rename (file moves), Delete (confirm) | âœ… |
| Copy code â†’ clipboard `WBX1:`; Paste code â†’ new layout | âœ… |
| Open file / Save file (Windows file dialogs) | â¸ manual â€“ OS dialogs |
| Capture current windows | âœ… (v0.1 run) |
| Canvas: drag a window (real mouse) â†’ saved, WoW windows not moved with live off; Undo restores | âœ… |
| Canvas: drop window 2 on window 3 â†’ places swap in layout **and** on screen (live); Undo swaps back | âœ… |
| Canvas: edited window drawn on top; taskbar strip drawn; only used monitors unless "All monitors" | âœ… |
| Opening the app never moves windows | âœ… (fixed in v0.2) |

## Squad tab
| Item | Result |
|---|---|
| Rows = slots; name/class/WoW account editable; "Unknown class" until set | âœ… |
| WoW account list from WTF folders; "Last played here" hint + Use | âœ… (5 accounts found; hint is labelled as hint) |
| First look: windows already in a slot are matched to it (was launch order â†’ wrong "Waiting") | âœ… fixed |
| Status: No window / Placed / Not in place / Check Activity | âœ… |
| Drag row 2 onto row 3 (real mouse) â†’ rows, layout and real windows swap | âœ… (first failed twice: text box swallowed the drop; unnamed rows did not trigger live â€“ both fixed) |
| Launch mode Battle.net / Login screens saved | âœ… |
| Start squad, Login screens, 4-client layout with 3 clients open â†’ opens exactly 1 login screen, placed in slot 4 | âœ… |
| Close clients I started â†’ closes only that one; owner's 3 clients untouched | âœ… |
| Start squad keeps the rows' order (was launch order) | âœ… fixed |
| Checklist ticks (Battle.net running, account rows) | âœ… |
| Identify / Identify all (click-through numbers, never activated) | âœ… (v0.1/v0.2 runs) |
| Arrange now / Apply to open windows | âœ… |
| Give title bars back, Diagnose report | âœ… |

## Other tabs and app life
| Item | Result |
|---|---|
| Guide: 6 chapters, hover-setting helper, website link | âœ… (link opens a placeholder URL) |
| Settings: switches toggle and persist; game exe found automatically; re-read accounts | âœ… |
| About | âœ… |
| Restart: last layout, tab, live, launch mode remembered | âœ… |
| Second start with `--tray --start NAME` while running â†’ running app starts that layout | âœ… fixed (args were dropped) |
| Single instance (second start only shows the first) | âœ… |
| Tray menu (open, start, arrange, identify, quit) | â¸ right-click on the tray needs real input on the shell â€“ not automated |
| First run screen | âœ… |
| Installer (vpk pack, 10.7 MB, splash) | âœ… built; â¸ not installed on the owner's PC |
| Monitor change while running (plug/unplug, DPI change) | â¸ needs hardware change |
| 1 monitor only | â¸ (unit-tested) |

## Game-side checks
| Item | Result |
|---|---|
| Windows placed exactly (`wbx info`) incl. title-bar frame correction | âœ… |
| Action bar complete only at 4:3 game area; clipped ends below (`wbx snap`) | âœ… (see wow-display-findings.md) |
| UI Scale fixes the bar (storage verified: shared Config.wtf) | â¸ one restart of the owner's clients after "Fix action bar" |
| Direct `WowClassic.exe` start â†’ WoW login screen, no effect on running sessions | âœ… |
| Resize nudge leaves window 2 px small (owner's run 16:02) | âŒâ†’âœ… fixed: nudge off by default, re-place after nudge |
