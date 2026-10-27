# Known issues and limits

Newest first. "Workaround" is what to do today; "Plan" is the fix.

## Open
- **Classes are not detected.** The app cannot see which character a client plays (no memory reading, and the WTF
  files hold no class). Class and name are set by hand per slot; "Unknown class" until then. *Plan:* the WoWBoxer
  addon writes character/class to its SavedVariables (`docs/classes-and-accounts.md`).
- **Which WoW account is in which window cannot be known from outside.** All clients have the same command line;
  logs are shared. The user picks the account per slot once (Squad tab); "last played here" is a hint from folder
  times, never a fact.
- **The UI Scale fix is not yet confirmed inside Classic Era.** Where it is stored is now verified on the Forever client
  (1.60, same engine): WoW keeps `useUiScale`/`uiScale` in the shared `WTF\Config.wtf` and rewrites it on exit, so
  one value applies to every client of an install. "Fix action bar" writes that one value (smallest the squad needs,
  e.g. 0.93 for the owner's 5:4 followers) – only while all clients of the install are closed, with a backup. Whether
  Classic Era 2026 reads it the same way, and that the bar then fits, needs one restart of the owner's clients.
  *Workaround today:* "Shorten window instead" (4:3 game area). Edit Mode's per-account file is still never written.
- **Forever opens in "Windowed (Fullscreen)"** (`GxMaximize 1`, borderless 2560×1440). In that mode the client
  maximises itself again after a move, so it cannot be placed (seen 2026-10-27). *Workaround:* in game set Display
  Mode = Windowed once. *Plan:* offer to write `GxMaximize "0"` while the Forever clients are closed (backlog).
- **Always on top with "hover activates windows" (Windows setting, off on the owner's PC):** hovering raises a window
  only within its band, so floating followers stay above a non-topmost main. If the *main* is also "on top", hovering
  it can raise it over overlapping followers – keep the main off "on top" in overlay layouts (the Overlays preset does).
- **"Launch all" cannot pick accounts.** Starting clients with Battle.net's auto-login would log in the account
  selected in Battle.net (and could disconnect a running client). The app only opens login screens; the user logs in.
- **Tray menu, file dialogs, monitor plug/unplug and one-monitor PCs are not covered by the automatic tests** (need
  real shell input or hardware changes). Tested by hand in earlier runs where noted in `docs/test-plan.md`.
- **Website guide link is a placeholder** (`MainViewModel.WebsiteGuideUrl`) until the website exists.
- **Release builds are unsigned** until SignPath approves; Windows SmartScreen will warn on the installer.
- **Idle clients get disconnected by the game** after about 30 minutes without input (seen during the unattended test
  run). Not caused by the app; the app sends no input and must not.
- UI Automation does not list the app's small dialogs (prompt, yes/no) at desktop level; `tools/ui-sweep.ps1` finds
  them by window handle. No effect for users or screen readers that follow focus.

## Fixed in v0.2.2 (2026-10-27, owner's test of v0.2.1)
- **Always on top never worked for the main window and could not be turned off for followers.** "On top" was only
  applied when a window had to move (main windows are usually already in place), and "not on top" never sent
  HWND_NOTOPMOST. Now set and cleared on every placement, at app start and on every edit (live on). Verified with
  WS_EX_TOPMOST on the real windows (`tools/topmost-check.ps1`).
- **A Forever window was pulled into the Classic Era layout.** Windows are now filtered by game, from the exe's
  install folder (`_classic_era_`, `_forever_` …; exe names are shared between games). Each game has its own layouts.
- **The action-bar mark was a guess from the layout.** It is now measured on the open window (game area + UI Scale);
  the threshold for "buttons clipped" was corrected to 1.26 after the owner's follower screenshot (1.25 cuts the bags; 1.269 shows every button).
- **Fixes were not visible:** the CLI and app reported version 0.1.0 (never bumped). Version now in the title bar and
  About, and "What's new" appears once after an update.

## Fixed (2026-10-27)
- Resize nudge could leave a window 2 px too small (owner's Start squad at 16:02: 830×694). Nudge is off by default;
  when on, the window is re-placed afterwards.
- Squad rows showed old trio classes (guessed colours). Classes are now never guessed.
- Rows showed "Waiting" for open windows that sat in each other's slots; first look now matches windows to the slots
  they occupy, and "Not in place" is shown for open-but-elsewhere windows.
- Dragging a squad row onto a name box did nothing (the text box took the drop); dragging unnamed rows did not move
  the real windows. Both fixed.
- Start squad ignored the rows' order (used launch order). Fixed.
- A second start with `--start NAME` (desktop shortcut while the app runs) only showed the window. Fixed.
- Opening the app ran a live arrange; the window being edited was drawn under its neighbour; app counted itself as a
  WoW client; activity list crash when scrolling (earlier fixes).
