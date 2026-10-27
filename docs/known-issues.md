# Known issues and limits

Newest first. "Workaround" is what to do today; "Plan" is the fix.

## Open
- **Classes are not detected.** The app cannot see which character a client plays (no memory reading, and the WTF
  files hold no class). Class and name are set by hand per slot; "Unknown class" until then. *Plan:* the WoWBoxer
  addon writes character/class to its SavedVariables (`docs/classes-and-accounts.md`).
- **Which WoW account is in which window cannot be known from outside.** All clients have the same command line;
  logs are shared. The user picks the account per slot once (Squad tab); "last played here" is a hint from folder
  times, never a fact.
- **Action bar fix inside the game is unverified.** UI Scale (`useUiScale`/`uiScale`) is set nowhere on the owner's PC,
  so where WoW stores it (shared `Config.wtf` or per account) is still unproven; the app shows the derived value
  (`UI Scale ≤ 0.75 × aspect`) marked "not tested yet" and writes nothing. Edit Mode's per-account file format is
  undocumented – not written either. *Workaround:* "Full action bar" / "Fit action bar" (4:3 game area).
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
