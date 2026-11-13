# Changelog

## Unreleased (goes into the next release section)
- Help: **Report a problem now opens the new #help forum** on the WoWBoxer Discord (invite `discord.gg/VRqjjftwdg`):
  "Paste with Ctrl+V into a new post in #help and press Enter." The old #setup-help invite still lands on a signpost.
- Releases are announced in the WoWBoxer Discord (#announcements for stable, #beta for betas).

## v0.7.2-beta.1 – Window swap fixed on Forever, sound follows the main window (unreleased)
**Beta channel only.**

Fixed (owner, 2026-11-10: with 3 Forever clients, swaps made the windows go back and forth and the main window ended up
at the wrong size):
- **One swap at a time.** A swap hotkey pressed while the windows of the last swap are still moving waits until they have
  landed (at most 2 wait; more presses are skipped with a note in the status bar), so fast presses no longer pull the
  windows two ways at once.
- **Both windows of a swap are placed together and kept there for 5 seconds** (the game's own resize right after a move),
  not for 30 seconds like a window that is still loading.
- **A client that keeps its own size is left alone.** When the game puts a window back to the same other size 3 times in
  a row, WoWBoxer stops resizing it and says once, in plain words, which size the game keeps and what to change – instead
  of resizing it every second and making it flip between two sizes. Each swap or Re-arrange tries again.
- `wbx swaptest --rounds N` tests window swap on the open clients (hotkey swaps, drag-swaps and fast presses, watched
  every 20 ms) and puts every window back.

New, off by default (owner, 2026-11-10):
- **Sound follows the main window**: only the WoW client in the main slot plays; the other clients are muted, or play
  quietly at "Other clients at" % of their own level. Turn it on with the new speaker in Your squad (Setup) or in
  Settings › Sound. It follows every swap hotkey, a drag-swap on the map, Re-arrange, and windows that open or close –
  at once. "Follow the active window instead" lets the window that has your keys play; layouts without a main window
  (Grid) always work that way.
- Works through **Windows' volume mixer only** (per program, like Windows' own Volume mixer): nothing is sent to the
  game, and Discord, music and every other program keep their sound. Clients that only start playing later, clients
  that restart, and several output devices are handled.
- Each client's **own volume is kept** and put back when you turn it off, when WoWBoxer closes and when you uninstall
  it. A client that was closed while muted gets its level back the next time it plays (Windows remembers a program's
  level per exe).
- WoWBoxer reads WoW's **Sound in Background** (Config.wtf) and says so when it is off – otherwise the main window goes
  quiet while you hover another one. While every client of that install is closed, "Turn on Sound in Background" sets
  it for you (a backup of Config.wtf is kept). It also tells you when sound is off in WoW altogether.
- Guide: new chapter "Sound follows the main window". `wbx audio` lists the WoW clients in the volume mixer;
  `wbx audio --rounds 10` tests the feature against the real mixer and puts every level back.

## v0.7.1 – Window swap keeps its main slot, Auto-fit fits every bar, fixes from #help (2026-11-10)
Everything from v0.7.1-beta.1 and v0.7.1-beta.2 (below), now for everyone, plus these fixes for problems players
reported in #help (released straight to stable on the owner's decision):
- **WowUp (and other programs whose name starts with "Wow") are never a WoW client.** Only Blizzard's game exe names
  count (Wow, WowClassic, their -64 / PTR / beta versions); an exe with a new name still counts when it sits in a
  World of Warcraft game folder. Settings refuses a WoW client path that is not a game, and a saved one is ignored.
- **A layout keeps its monitor.** Monitors are recognised by place and size first, then by Windows' device ID
  (Windows can swap the IDs of two monitors of the same model after sleep or a restart). Moving a Custom layout to other
  monitors keeps every window's place. While a monitor of the layout is off, the app uses 1 monitor without saving that:
  your saved layout comes back by itself when the monitor does.
- **Windows no longer flip in front of each other without stopping** when another program keeps changing "on top"
  (PowerToys Always On Top, a pin tool, a second WoWBoxer): after 6 corrections in 10 seconds WoWBoxer leaves that
  window alone and says why in Activity; Start or Re-arrange tries again. More than 8 swap hotkeys in 2 seconds pause
  swapping for 5 seconds (a stuck key or a keyboard macro).
- Help: "Shift, Ctrl or Alt does nothing after moving to another window" – release it and press it again there; the new
  window did not see the key go down.

## v0.7.1-beta.2 – Auto-fit fits every action bar, also on Forever (2026-11-10)
**Beta channel only.** Everything from v0.7.1-beta.1, plus:
Owner bug (2026-11-06): with action bars 2 and 3 on, buttons were missing in the small windows. Auto-fit only made room
for Classic Era's main bar, and it never ran on Forever.
- **Auto-fit now fits every bar you have on**: bars 1–3, the right bars and the bag and menu buttons, on **Classic Era
  and Forever**. Which bars are on comes from each character's settings (`enableMultiActionBars` in its
  config-cache.wtf). Where they are comes from the WoWBoxer AddOn's measurement when there is one (Edit Mode on
  Forever), otherwise from each game's default layout.
- **Forever:** bars 2 and 3 sit side by side above bar 1, so bars 1–3 and the bags need about 1190 UI units. A
  870×696 follower draws its UI at scale 1.00 (960 units wide), so the game pushed bar 3 onto bar 2 and the bags off
  the edge. Auto-fit sets one UI Scale for the install: 0.77 for the owner's Main + side layout. **Classic Era:** bars
  2 and 3 sit inside the main bar and the right bars above it, so nothing changes there (0.93 stays 0.93).
- If even the smallest UI Scale (0.64) can't fit them, the card says so in plain words: make that window larger or
  turn off a bar. The backup and the notice are as before.
- The map, the window details and **Shorten this window instead** use the same check ("All bars fit" / "Bars cut").
- `wbx bars` shows, per account, which bars are on, where they come from, and the UI Scale that fits them (reads
  files only).
- **Auto-fit says why when it does not write** (owner, 2026-11-10: Forever never got its UI Scale, and nothing said
  why). It now checks again when the squad switches to another game (the app had last been used with Classic Era, so it
  checked that one at start), and every decision not to write is one line in Activity: Auto-fit off, game not known,
  already fits, or clients of that game running (it then writes when the last one closes, and the map says so).
- The bag and menu buttons now count as covered when they lie on bar 1 in the same row (the game pushes them there in a
  narrow window).

## v0.7.1-beta.1 – Window swap keeps its main slot (2026-11-04)
**Beta channel only.** Fixes from the owner's beta test of v0.7.0: the swap hotkeys fired, but each press only logged
"click the crown on a window to make it the main slot". A drag-swap had turned "Trio" (Main + side) into Custom, and
the main slot was lost.
- **The main slot is carried over** when a preset turns into Custom (moving a window, a drag-swap, the Custom card). It
  follows the large place: after a drag-swap, the client now in the large window is in the main slot.
- **Custom without a crown uses its clearly largest window** (at least 10 % larger than the next one), per party. It
  only asks when the windows are about the same size. Grid still asks.
- **No main slot? You see it:** a swap hotkey shows a short notice in the status bar and the crowns on the map pulse.
  No pop-up, no focus change. It is written to the log once, not on every press.
- The crown and **IN MAIN SLOT** in the squad list now show whenever swap hotkeys are on, not only while swapped.
- Choosing another preset brings that preset's own main slot back instead of an old crown.
- **Same size** in Advanced editing is now **Ctrl+Shift+M**. Alt+Z is taken by the NVIDIA overlay on every PC that has
  it. The hotkey editor also warns about the default NVIDIA / AMD overlay shortcuts (Alt+Z, Alt+R, Alt+F1–F3,
  Alt+F9/F10, Ctrl+Shift+E/S/O/I).

## v0.7.0 – Window swap, Advanced editing, Custom layouts (2026-11-03)
Everything tested on the beta channel as v0.7.0-beta.1 – beta.4, now for everyone. The app code is that of
v0.7.0-beta.4. (Update channels and the beta channel itself came in v0.6.1.)
- **Window swap hotkeys** (Settings › Advanced, off by default): move a client's window into the main slot – the one
  there takes its old place. Ctrl+Alt+Shift+Right / Left (next / previous), +1…0 (that client), +Backspace (home);
  all editable, applied with **Apply hotkeys**. The hotkey goes to WoWBoxer, never to the game; a swap only moves
  windows and never changes which window has the keys. Moves windows only – nothing is sent to the game. Blizzard has
  not ruled on window-swap tools; use at your own judgement.
- **Remove title bars works properly** – contributed by [Limerobin](https://github.com/Limerobin) (PR #1): borderless
  windows are exactly the game area, Main + side fills its column without gaps; new switch on Setup. Still off by
  default.
- **Advanced editing** on Your screens: select several windows (Ctrl/Shift+click, drag a box, Ctrl+A), move them
  together, match size (Alt+Z), align, distribute, fill a row or column, **fill the empty space, the selection's span
  or a drawn area** (row / column / grid, live preview with each window's action-bar verdict), Fit 4:3, snap, grid,
  lock aspect, exact X/Y/W/H, Undo/Redo (Ctrl+Z / Ctrl+Y).
- **Custom** (was "Free for all"): your own layout. Choosing it keeps what is on the map; moving a window of any preset
  switches to Custom; every change is kept as a draft; leaving with unsaved changes asks once (Save as… / Keep as
  draft / Discard). Old layouts and codes are migrated.
- **Paste a layout code** fixed: long codes stay inside the dialog, Paste button, live check, Enter = OK.
- Squad rows have an optional Role (tank / healer / damage); Guide chapters "Swap windows (advanced)" and "Build your
  own layout (Custom)".

## v0.7.0-beta.4 – Fill a custom area (2026-11-03)
**Beta channel only.** Everything from v0.7.0-beta.3, plus three ways in Advanced editing to fill an area of your
choice with the selected windows (next to Fill row / Fill column for the whole monitor):
- **Fill empty space:** tiles the selection into the largest free rectangle – the part of the monitor not covered by
  the windows you did not select (window 1 on the left half → four selected windows fill the right half). With
  several free areas it takes the one the selection is mostly in.
- **Fill selection span:** equal windows from the first window's left/top edge to the last one's right/bottom edge
  (PowerPoint-style, within the selection).
- **Fill area…:** drag a rectangle on the map (over windows too) or type it in px or % of the monitor.
- For all three: **Row / Column / Grid** (rows × columns automatic – cells as close to 4:3 as possible – or set),
  the gap, and the order (current left-to-right / top-to-bottom, or by client number). Each one is a **live
  preview** first: the area is highlighted and every window shows where it goes and its action-bar verdict there;
  **Apply** makes it one undo step, **Cancel** or Esc leaves everything as it was.
- Tests: free-space detection (1–3 monitors, taskbar on/off, overlapping windows and windows sticking out), the three
  fill modes, grid sizes, order and typed areas.

## v0.7.0-beta.3 – Advanced editing and Custom layouts (2026-11-03)
**Beta channel only.** Everything from v0.7.0-beta.2, plus:
- **Advanced editing** on "Your screens": a toggle on the map's header opens a slim toolbar above the map (icons with
  tooltips and shortcuts). Click selects a window, Ctrl/Shift+click adds or removes, dragging on the empty map draws
  a box that selects across monitors, Ctrl+A selects all, Esc clears. Dragging any selected window moves the whole
  group; arrow keys nudge it 1 px (Shift: 10 px). The first window picked is the **reference** (gold outline):
  - **Match size:** same width, same height, same size (**Alt+Z**) – every window keeps its top-left corner.
  - **Align** left, centre, right, top, middle, bottom – to the reference, or to the monitor with one window.
  - **Distribute** horizontally / vertically with equal gaps; **Fill row / Fill column** makes the windows the same
    size and fills the monitor's usable width or height edge to edge in their order (optional gap, 0 by default).
  - **Fit 4:3**, **Swap when dropped on another window** (off inside Advanced editing, so windows can sit next to or
    over each other; always on outside it), **Snap**, **Show grid** (twelfths of each monitor), **Lock aspect ratio**.
  - X / Y / W / H for the selection (blank where they differ). **Undo / Redo** (Ctrl+Z / Ctrl+Y) for every step.
  - The real windows follow when a drag or an operation ends, not on every mouse move. Shortcuts work only inside the app.
- **"Free for all" is now "Custom"** – a sandbox. Choosing Custom keeps what is on the map (or brings this layout's
  last custom draft back); moving or resizing a window of any preset switches to Custom from exactly that
  arrangement; every change is kept as a draft (survives restarts). Leaving Custom with unsaved changes asks once:
  **Save as…** / **Keep as draft** / **Discard** – the only question. Old layouts and share codes saying "free" are
  read as Custom and saved again in the new form.
- **Fixed: Paste a layout code.** Long codes no longer push OK and Cancel off the screen: fixed width, the code wraps
  in a box that scrolls, a **Paste** button, a live check ("Valid WBX1 code · 3 clients · Main + side" or a clear
  error), Enter = OK when the code is valid.
- Guide: new chapter **Build your own layout (Custom)**. Website screenshots: a new "Advanced editing" picture.
- Tests: selection, match size, align, distribute, fill (1–3 monitors, mixed DPI, taskbar on/off), Custom drafts,
  the leave question, migration and code checking. `tools/edit-sweep.ps1` (UI Automation of the toolbar, Custom and
  the import dialog in demo mode) and `tools/edit-real-check.ps1` (Fill row and Undo on real Classic Era clients).

## v0.7.0-beta.2 – Remove title bars, fixed (thanks to Limerobin) (2026-11-02)
**Beta channel only.** Everything from v0.7.0-beta.1 (window swap hotkeys), plus:
- **Remove title bars works properly now – contributed by [Limerobin](https://github.com/Limerobin) (PR #1).** Borderless
  windows get real popup chrome (`WS_POPUP`, the window is exactly the game area, a 1 px nudge so WoW rereads its mouse
  bounds), and Main + side rebuilds its column for the new frame size, so the followers fill it without gaps. New
  switch on Setup (Your squad, above Hover to activate), the same option as Settings › Window rules. Still **off by
  default**. Tested on three Classic Era clients (character screen): client area = window, no gaps, title bars come
  back when switched off and with "Give title bars back", nothing else changes. Please check the action bar, minimap
  and mouse in game.
- Our additions to it: while the app is stopped the switch leaves the windows alone (they change at Start); a layout
  whose windows were moved by hand keeps them (only an unchanged preset is rebuilt); a swap goes back home first.
- `tools/borderless-check.ps1`: the real-client test (exact window state saved and put back). Win32 unit tests
  (`tests/WoWBoxer.Layouts.Win32.Tests`). CI's start-up check waits up to 30 s.

## v0.7.0-beta.1 – Window swap hotkeys (beta) (2026-11-02)
**Beta channel only** (Settings › Updates › Update channel: Beta). Stable installs do not get this version.
- **Window swap hotkeys** (Settings › Advanced, **off by default**). Hotkeys move a client's window into the main slot;
  the client that sat there takes the chosen client's old slot. Clients keep their number, name and class; position,
  size, monitor and "always on top" belong to the slot. A swap only moves windows with the same placement code as
  always: it never changes which window has the keys (hover to activate or a click does that) and sends nothing to
  the game. Note in the app: "Moves windows only – nothing is sent to the game. Blizzard has not ruled on window-swap
  tools; use at your own judgement."
- Hotkeys (global, `RegisterHotKey` with no auto-repeat; the key press goes to WoWBoxer, never to the game), all
  editable or clearable: next / previous client to main **Ctrl+Alt+Shift+Right / Left**, client 1…10 to main
  **Ctrl+Alt+Shift+1…0**, back to home layout **Ctrl+Alt+Shift+Backspace**. The editor captures a combination
  ("press the keys"), shows clashes (another action, or another program has it) and warns about fewer than two
  modifiers and combinations Windows uses (Alt+Tab, Win+…, Alt+Shift language switch …). Changes are marked "not
  applied yet" and only take effect with **Apply hotkeys** (Discard keeps the ones in use). Settings shows how many
  are active.
- **Main slot:** the large slot in Main + side and Overlays (one per party with 6–10 clients). In Grid and Free for all
  you pick it: with swap on, every window on the map has a crown – click it ("MAIN SLOT"); a hint stays above the map
  until each party has one. Stored in the layout (`swapMainSlots`).
- Next / previous go through party 1 by default (or the party of the active window, or all clients), in number
  order, skipping clients whose window is closed and wrapping around. Your own order: ready-made "All in number
  order", "Leader ↔ each follower" and "Leader ↔ healer" (shown when a healer is known: a row's new optional
  **Role** – Tank / Healer / Damage – or a priest), or build one from client chips (click to add, drag to move).
- Your squad shows "IN MAIN SLOT" / "in slot N"; the tray has **Back to home layout** and **Disable swap hotkeys**.
  Re-arrange, another layout or turning swap off puts everyone home. "Remember swapped positions after restart" is
  off by default (the app starts in the home layout; windows are still recognised where they sit).
- `WoWBoxer.exe --swap next|previous|home|1…10` runs a swap in the open app (scripts, tests).
- Guide: new chapter **Swap windows (advanced)**. CLAUDE.md: the owner's decision (2026-11-02) recorded as the second
  exception to the hard rules.
- Tests: cycle logic (skip closed, wrap, previous, custom orders with repeats, parties), main slots per layout, hotkey
  parsing / validation / clashes, swap and reset round trips (52 new). `tools/swap-check.ps1`: end to end with the
  Dev build and three fake WoW windows (every swap lands exactly, "on top" follows the slot, the active window never
  changes, hover on and off; with `-Keys` the real hotkeys, and the fake's key log proves the main key never arrives).

## v0.6.1 – Update channels and a safer way to test (2026-11-02)
First released as **v0.6.1-beta.1** on the new Beta channel (the proof that the pipeline works); promoted to stable on
the same commit after a few quiet days.
- **Settings › Updates › Update channel: Stable or Beta.** Stable (the default) gets normal releases only. Beta also
  gets test versions (GitHub pre-releases, tags `vX.Y.Z-beta.N`) a few days early, and still gets every stable release.
  A change counts from the next update check; back to Stable never installs an older version – you keep yours until a
  newer stable one is out. Copies installed from `WoWBoxer-Beta-Setup.exe` start on Beta.
- **Settings › Experimental:** new risky features arrive here switched off, so anyone can turn one off again without
  waiting for an update. Empty for now ("Nothing to try right now").
- **WoWBoxer Dev** (`tools/dev-build.ps1 -Install`): a test build from any branch that installs next to the normal app
  – its own install folder, shortcuts, Apps entry, settings, layouts and logs, a DEV badge and a teal icon, no
  auto-update. Each app warns when the other one is open (both would place the same windows).
- The version in the title bar, About and reports now includes the pre-release part (`0.6.1-beta.1`).
- Release pipeline (`docs/release-process.md`): main is stable; work happens on `feature/*` branches with CI on every
  push and pull request; main is protected against force-push and deletion; releases are only built from commits on
  main; a beta can be promoted to stable on the same commit; after publishing, the workflow checks that
  `/releases/latest` is the newest stable release and never a beta. A manual re-run now builds the tag it names.
- `tools/channel-check`: runs the app's own update code against the real GitHub releases for a pretend install
  ("would a stable 0.6.0 get the beta?") without installing anything.
- The start-up check (`tools/smoke-start.ps1`, CI) looks at every window of the app for the main one; it failed once
  on the runner because the What's new dialog can hide the main window from `Process.MainWindowTitle`.

## v0.6.0 – Now simply called WoWBoxer (2026-10-31)
- **The app is now called "WoWBoxer"** everywhere you see it: title bar, About, welcome screen, What's new, the tray,
  Diagnose and Report a problem, the installer, the Start menu and desktop shortcuts and Windows' Apps list. Where app
  and addon need telling apart: "WoWBoxer" (the app) and "WoWBoxer AddOn".
- **The exe is `WoWBoxer.exe`** (was `WoWBoxer.Layouts.exe`). Updating from 0.5.x moves everything along: Velopack
  points every shortcut into the install folder at the new exe – the Start menu, desktop and taskbar ones and your own
  layout shortcuts, with their `--start` arguments – and "Update now" opens the renamed app with the same arguments.
  Tested with a throwaway package before release (title and exe renamed in one update).
- Unchanged, so nothing breaks: the install folder and Velopack id (`WoWBoxer.Layouts`), settings, layouts and logs
  folders, the `wbx` command line, the repository and the code's names. The old launcher
  `WoWBoxer Window Layouts.exe` stays in the install folder; it still opens the app.
- **Website screenshots, always current.** `--demo` opens the app with made-up monitors (2560×1440 + 1920×1080) and a
  made-up squad; nothing real on the PC is read or changed. `--screenshots FOLDER` renders every screen (Setup with 3
  clients, 2 monitors + Free for all, 10 clients in two parties, window details, Settings, Help, About, the tray menu,
  the welcome screen) and `tools/make-screenshots.ps1` turns them into WebP (1440 and 720 wide) plus `manifest.json`.
  The set lives in `docs/screenshots/site/`; CI renders it on every push, and every release gets `site-screenshots.zip`
  built from its own version.

## v0.5.1 – A Help tab (2026-10-30)
- **New tab: HELP** (between Settings and About; Ctrl+4 or F1). Left: *Get help* – Report a problem, and the Discord,
  YouTube and website links – and *See what the app sees* – Identify all windows, Diagnose, Open logs, Give title
  bars back. Right: *Common problems*, each with its fix (stopped app, admin rights, windows jumping back, cut-off
  action bar, Windowed (Fullscreen), wrong character, ignored windows, hover too quick, unplugged monitor, SmartScreen,
  idle disconnects).
- Settings › Tools keeps the setup tools (Identify, Close clients I started, title bars, layouts folder, welcome
  screen); Diagnose, Open logs and Report a problem moved to Help. Guide › If something looks wrong has an "Open Help"
  button; the tray's Report a problem opens Help first. About is Ctrl+5 now.

## v0.5.0 – Community links and Report a problem (2026-10-30)
- **Community links, small and always there:** Discord, YouTube and website icons in the status bar, the same with
  labels in About, and one line – "Join the WoWBoxer Discord – tips, layouts and help" – on the welcome screen and at
  the bottom of What's new. Never a pop-up, reminder or badge.
- **Links in one place:** the app reads `https://wowboxer.com/links.json` at most once a day (cached on the PC;
  format `wowboxer-links/1`, see `Core/CommunityLinks.cs`) and uses the built-in links until then or when it cannot be
  read. Only https links to Discord, YouTube and wowboxer.com are accepted. It follows the "Check for updates" switch,
  so that switch off means no network at all. docs/privacy.md explains it.
- **Report a problem** (Guide › If something looks wrong, Settings › Tools, tray menu): builds a report – version,
  Windows, layout, clients, monitors, each slot's status, action bar / UI Scale, hover setting, last 50 log lines –
  with the Windows user folder and name, WoW account and character names hidden. You see and can edit it, add "What
  happened?", and **Post in Discord** copies it as one Discord message (a code block, shortened to fit 2,000
  characters by dropping the oldest log lines), saves the full report (plus a read-only Diagnose that moves no window)
  as a .txt in the logs folder, and opens the invite to `#setup-help`: "Paste with Ctrl+V in #setup-help and press
  Enter." Nothing is ever posted by the app.
- About no longer links to the source code on GitHub (no GitHub links in the app); the README still does.
- Unit tests for the links file (overrides, broken files, unsafe links), redaction (a fake user name, accounts,
  characters, whole words only) and the Discord message size.

## v0.4.2 – The layout cards are back (2026-10-30)
- **Pick a layout with the cards again** (owner's preference): Main + side, Overlays, Grid and Free for all side by
  side, each with its live preview for the current clients and monitors – one click switches. The dropdown from v0.4.0
  is gone; the Monitors section stays right below the cards, and the chosen layout's short description under them.

## v0.4.1 – What's new fits the screen (2026-10-30)
- **What's new is smaller and scrolls.** It lists only the six newest changes (the rest is here in the CHANGELOG),
  never grows taller than the screen (at most 640 px), and the list scrolls while the title and **Continue** always stay
  in view. Before, the long list pushed Continue off the bottom of the screen (owner's report on v0.4.0). Enter or Esc
  also closes it.

## v0.4.0 – Up to 10 clients; layout and monitors as two clear choices (2026-10-30)
- **1–10 clients.** The number buttons go to 10 (two rows of five): clients 1–5 are party 1, 6–10 party 2, with
  client 6 as party 2's main. Your squad groups its rows as "Party 1" and "Party 2" above 5 clients; dragging a row into
  the other party works as before (rows, map and real windows follow).
- **Layout is a dropdown.** Section II is now one compact dropdown – Main + side, Overlays, Grid, Free for all – each
  entry with a live mini preview; the chosen layout's short description sits under it.
- **New section: Monitors** (right below the layout, only with 2+ monitors): "1 monitor", "2 monitors" … up to the
  number Windows reports, each showing which ones ("Primary + right · 2560×1440 + 1920×1080"). With fewer than all,
  "Which ones" picks them (default: the primary first, then left to right). Default: 1 monitor, the primary.
- **Your screens shows exactly the chosen monitors**, numbered 1, 2, 3 … in their real arrangement and scale, and glides
  when the layout, monitors or client count change.
- **Every layout works across the chosen monitors:**
  - *Main + side*: up to 5 clients on 1 monitor as before; on 2+ monitors the main fills monitor 1 and the followers
    spread over the others (this replaces the separate "2 monitors" layout – older layouts made with it open as Main +
    side on two monitors). 6–10 clients: one Main + side block per party – one per monitor, or the two halves of one
    monitor (side by side or stacked, whichever gives the bigger game windows; **Flip** turns it).
  - *Overlays*: main on monitor 1 with the followers floating along the bottom, or on monitor 2 ("Followers on monitor
    2"). 6–10 clients: two rows on one monitor, or one row per monitor with party 2's main under its row.
  - *Grid*: tiles spread evenly over the chosen monitors.
  - *Free for all*: new windows start spread over the chosen monitors; drag a window anywhere, also across a monitor
    border – it lands on the monitor holding its centre and snaps to each monitor's edges.
- **The action-bar verdict for the whole layout** sits under the layout ("The whole action bar fits in 7 of 10
  windows …"). With two parties on one monitor it suggests using 2 monitors.
- **Saved layouts remember their monitors** (by Windows' device ID, with place and size as a fallback when IDs change).
  When monitors are plugged in or out the list refreshes; if a monitor of the layout is gone, it goes back to 1
  monitor with a short notice (Ctrl+Z, or choose the monitors again when it is back). Share codes leave the device IDs
  out.
- **Updates:** when the app opens and you already have the newest version, the activity log now says so ("You have
  the newest version (0.4.0)."), so a quiet start is not a mystery. When a newer one is out, it asks "Update now /
  Later" as since v0.3.5 (that worked: 0.3.5 → 0.3.6 on the owner's PC; there was simply nothing newer to offer after).
- Guide › chapter 2, README and the plan explain it. CLI: `--clients` 1–10, `--preset free`.
- Unit tests: every layout × 1–3 monitors (incl. three mixed-DPI monitors numbered out of order) × 1–10 clients, plus
  parties, split, monitor matching and fallback (2,338 cases). `tools/ui-sweep.ps1` covers the new controls.
- `docs/test-plan.md`: repaired characters that an earlier edit had double-encoded ("âœ…" → "✅").

## v0.3.6 – update test (2026-10-29)
A small release to check that v0.3.5 asks "Update now / Later" when it opens. No other changes.

## v0.3.5 – Hover starts at 30 ms, off on quit; the app asks to update when it opens (2026-10-29)
- **Hover to activate starts at 30 ms.** Turning the switch on (Setup › Your squad, Settings › Switching windows, the
  tray or the Guide) now writes a 30 ms delay together with "on", whatever delay Windows had before; the slider jumps
  to 30 ms with it. The slider stays free (0–500 ms) and still applies when you let go.
- **Reset to default** next to the delay slider (Setup and Settings) sets the delay back to 30 ms at once, with a
  "Default: 30 ms" label under the slider (the mark on the slider is the same 30 ms). It is separate from "Restore my
  original Windows settings", which still puts back your own values from before you first used the switch.
- **Quitting turns hover to activate off.** Closing the app (✕, tray › Quit, signing out of Windows) switches it off
  again; the delay stays for next time. Settings › Switching windows › "Turn hover to activate off when WoWBoxer quits"
  (on by default). "Restore them when WoWBoxer quits" still puts back your own values instead.
- **The app asks to update when it opens.** If a new version is out, a box asks "Update now" or "Later". Update now
  downloads it, closes the app and opens the new version (What's new shows what changed); Later installs it when you
  quit. Before, every update was only installed on quit without a word, so the app opened one version behind. A tray
  start (no window) still installs on quit. This version still arrives the old way; from the next one on you are asked.
- Guide › Switch between windows explains the default, Reset and quitting.

## v0.3.4 – Hover to activate is the WoWBoxer way to switch (2026-10-29)
- **Hover to activate is the WoWBoxer way to switch windows** (owner's decision). The old notices next to the switch
  (Setup › Your squad, Settings › Switching windows) are gone, and so is the advice about other ways to switch
  windows (Guide, What's new, README, known issues). In their place, the facts: hover to activate is a Windows setting; it
  sends nothing to the game; WoWBoxer never sends keys or clicks.
- Behaviour is unchanged: off by default, only changed on your click, your own Windows values kept and restored
  (Settings › Restore, on quit if chosen, always on uninstall).
- **Website links now open https://wowboxer.com/** (the site's new domain) instead of the Vercel address: the Guide,
  the first-run screen, About and the rules link next to the switch.
  The README links to the website, and the GitHub repos list it as their website.

## v0.3.3 – Fix: the app did not start (2026-10-28)
- **v0.3.2 closed right away on start.** The Settings screen used the tooltip box style that only the Setup screen
  defined, so the main window could not be built. The style now lives in the shared theme. Installed v0.3.2 cannot
  update itself (it never gets far enough to check), so install this version with WoWBoxer-Setup.exe.
- CI and the release now start the built app and fail if it does not open its main window (`tools/smoke-start.ps1`),
  so a start-up crash like this cannot be released again.

## v0.3.2 – Start/Stop and hover to activate (2026-10-28)
Released at the owner's request before the live test on real WoW windows (needs the PC idle). Hover to activate
is off by default; its Windows values are unit-tested only so far.
- **Start / Stop placing windows.** The app opens **stopped** and leaves every window alone until you press **Start
  placing windows** (the main button while stopped). While started, a **■ Stop** button sits next to it; the tray menu
  has the same Start/Stop. Found by the owner: opening the app during a Retail session moved the Retail windows into
  the layout. Desktop layout shortcuts (`--start`) and `--resume` start at once; Settings › Windows › "Start placing
  windows when the app opens" (off by default) brings back the old behaviour. Opening clients from the app also starts.
- **Hover to activate, built in:** a switch for Windows' own "Activate a window by hovering over it with the mouse"
  (Ease of Access) in Your squad on the Setup tab, in Settings › Switching windows, in the tray menu and in the
  Guide's "Switch between windows" chapter (it replaces the button that opened the Windows setting). With it: a
  **Delay** slider (0–500 ms, 30 ms marked as recommended, written when you let go) and **Bring window to front when
  activated** (off keeps floating overlays visible; best with "Always on top"). Per user, no admin
  (`SystemParametersInfo` with `SPIF_UPDATEINIFILE | SPIF_SENDCHANGE`, so it is saved and applies at once). Changes
  made outside the app show up within a second. Off by default; the app never changes it on its own.
- **Your own values are kept:** the first change saves your Windows values (on/off, delay, bring to front).
  Settings › "Restore my original Windows settings" puts them back; "Restore them when WoWBoxer quits" (off by
  default) does it on quit; uninstalling always does it (Velopack uninstall hook).
- Notice next to the switch (WoW tooltip style) explaining that hover to activate is a Windows feature, not part of
  WoW, with links to the Guide chapter and the website's rules page. (Replaced in v0.3.4 by a short factual line.)
- Website links point to the live site (`wowboxer-site.vercel.app`) instead of a placeholder.
- New app-wide slider style (gold track, marked values).

## v0.3.1 – update test (2026-10-28)
A small release to check that installed copies of v0.3.0 update themselves.
- Docs: the release file list in `docs/releasing.md` matches what a release really contains.

## v0.3.0 – first public release (2026-10-28)
The first public release. Same app as v0.2.4 (plug and play), now installable from GitHub Releases with auto-update.
- **Install:** `WoWBoxer-Setup.exe` (stable link: `/releases/latest/download/WoWBoxer-Setup.exe`). Installs per user
  without admin rights and fetches the .NET 10 Desktop Runtime from Microsoft if it is missing. Also
  `WoWBoxer-Portable.zip` and `SHA256SUMS.txt`.
- **Updates:** installed copies update themselves from GitHub Releases (applied when you quit). A beta channel for
  prereleases exists; stable installs never see betas.
- **Open source:** README, CONTRIBUTING, SECURITY, CODE_OF_CONDUCT, issue/PR templates, updated privacy notes,
  code signing policy (SignPath Foundation application prepared; this release is unsigned).
- **Release pipeline:** notes from this file, stable file names, checksums, draft-then-publish, website notification.
- Highlights since the first builds (details below): layouts for 1–8 clients on one or more monitors, a to-scale
  monitor map, automatic placement of every new WoW window, Auto-fit action bar for Classic Era, always-on-top per
  window, layout sharing (`WBX1:` codes), tray, desktop shortcuts, Diagnose, and the `wbx` command line.

## Earlier builds (private)

### v0.2.4 – plug and play (2026-10-27)
Owner: "it works well, but too much is going on". Before/after: `docs/screenshots/setup-before-v0.2.4.png`,
`setup-after-v0.2.4.png` (+ `-details`, `settings-after-v0.2.4.png`).
- **Always watching:** the app always waits for WoW windows and puts each new one in the next free slot (placed again
  for 30 s while WoW loads); a closed window frees its slot, the others keep theirs; a window that already sits in a
  slot keeps it. The "Move WoW windows live" switch, "Apply to open windows", "Start squad", "Stop watching" and the
  start checklist are gone. **Pause watching** lives in the tray menu (and `--pause` / `--resume`, also sent to a running
  copy); while paused nothing is moved or kept on top.
- **Status instead of steps:** each slot on the map has a ring round its number (pulsing = Awaiting…, green =
  Connected, amber = Placing… / Moved) and a label; each squad row has the same chip. Title bar: "Awaiting clients ·
  1 of 3 connected" → "All 3 connected" (dot: pulsing gold / green / amber / grey when paused).
- **One primary button:** "OPEN N WOW CLIENTS" (login screens for the empty slots only, from the open clients' install,
  else the last used one) → "AWAITING CLIENTS 1/3" → "RE-ARRANGE" (snap everything back, also F5). "Open Battle.net
  instead" link next to it. Undo/Reset are icon buttons; "Saved automatically as …" stays.
- **No game selector:** every WoW install is treated the same (Classic Era, Forever, Retail, PTR …), found by its install
  folder. With windows of several installs open the squad follows the install with the most windows (tie: it stays
  with the one it follows; at the start the one opened first); the others are left alone with a notice "1 Forever
  window ignored · Include". Layouts no longer belong to a game. UI Scale, Auto-fit and account hints follow the
  squad's install.
- **Less on screen:** "Full action bar" switch gone (Auto-fit action bar does it, on by default); the map shows the
  monitors in use; Window rules, Action bar (UI Scale) and Tools (Identify all, Close clients I started, title bars
  back, Diagnose) moved to Settings; Saved layout is a compact dropdown with Save as… and ⋯ (Rename, Delete, share,
  import, capture); the help line is a "?" tooltip; the activity log opens from the status bar.
- **Squad rows:** one line per window – drag handle, number in the class colour, name (optional), pin, status chip,
  ▾. The ▾ details hold class, WoW account (+ last-played hint), Always on top, Identify, X/Y/W/H, Keep size and the
  action-bar line.
- **1 client = normal play:** the client numbers now go 1–8. Picking 1 makes one window fill the main monitor (no multiboxing) without closing the app; other open clients are left where they are. Back to 3 (or Undo) and the squad returns.
- Typing a name in a squad row no longer shows the faded placeholder ("Main", "Follower 1") under the text until it is saved.
- Changing a name, class or "on top" no longer re-arranges every window (only a change of the windows' places does).
- Fixes found while testing: an open row closed by itself after a moment (a focus style trigger undid the selection –
  now a focus handler keeps it); a window closed while being placed was reported as "WoW runs as administrator";
  extra windows were named after the squad's game.
- New tools: `tools/plug-and-play-check.ps1` (19 checks with fake WoW windows on monitor 2, `tools/fakewow/FakeWow.cs`);
  `ui-sweep.ps1` now needs the app started with `--pause`.

### v0.2.3 – owner's test of v0.2.2 fixed (2026-10-27)
- **One main screen:** Squad is merged into Setup – clients, layout, saved layout and window rules on the left, the map
  in the middle, "Your squad" on the right (rows per window: number, name, class, account, status, pin, Identify, drag
  ⋮⋮ to reorder). Selecting a row selects the window on the map and opens its details inline (X/Y/W/H, Always on top,
  Keep size, action bar). Start with Battle.net / Login screens, Apply and Start squad in the footer; tools, start
  checklist and activity at the bottom of the squad panel. Tabs: Setup · Guide · Settings · About.
- **Auto-fit action bar** (default on, Settings and the action-bar box): whenever no client of the install runs – app
  start, before Start squad, when the last client closes, or after a layout change – the UI Scale the layout needs is
  written (backup kept), with a notice on the map and a line in the activity log. "Fix action bar" stays as the
  manual fallback; the UI explains why it only works while the clients are closed. Confirmed in game: UI Scale 0.93
  shows the whole bar in the owner's 830×664 followers. Classic Era only for now (Forever's UI is not verified;
  the button works there).
- **Always on top, reliable:** applies instantly with live on or off; the open-window list now refreshes every 2 s
  app-wide (it only did on the Squad tab); Start squad's slot mapping drives the rows; a passive guard (WinEvent hooks:
  foreground, location, reorder – listening only) re-asserts on-top when anything changes it. A pin on the map and on
  each row shows the real state (red = being put right).
- Fixes found while testing: dragging a squad row did nothing in the new list (the list took the mouse); Undo after a
  row drag restored names but not which window/account was in which row; the guard logged our own changes as
  "set by something else"; "Unknown class" label shortened to "Unknown".
- `tools/topmost-check.ps1`: toggles with live off + 24 disturbance rounds (another program clears/sets on-top, window
  moved, foreground switched between our own windows).

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
