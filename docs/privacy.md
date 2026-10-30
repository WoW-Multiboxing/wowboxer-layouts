# Privacy

WoWBoxer Window Layouts collects **no telemetry**, shows no ads and has no accounts. Nothing about you, your PC or
your characters is sent anywhere.

## Network calls (two, both behind one switch)
**1. The update check.** An installed copy asks GitHub Releases (`github.com/WoW-Multiboxing/wowboxer-layouts`) whether a newer version
exists when the app starts, and downloads it if so. That is a normal web request to GitHub. GitHub sees your IP
address, as with any download ([GitHub's privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement)).
No identifier, setting or usage data is sent. Switch it off in **Settings → Updates and privacy**
(or `"checkForUpdates": false` in `settings.json`). A portable copy or one built from source never checks.

**2. The community links.** At most once a day the app reads `https://wowboxer.com/links.json` – a tiny file with
the Discord, YouTube and website links, so they can change without a new version. It is a plain `GET` request with no
cookies, identifiers or data about you (the website sees your IP address, as with any page). The file is kept in
`%LOCALAPPDATA%\WoWBoxer\Layouts\links.json`; until it has been read, or when it cannot be, the links built into the
app are used. Only https links to discord.gg/discord.com, youtube.com and wowboxer.com are accepted from it. The same
switch (**Check for updates**) turns this off too, so with it off the app uses no network at all.

Opening a link (the Discord, YouTube and website icons, or "Post in Discord") opens it in your browser – that is you
visiting the site, not the app sending anything.

The installer may also download the .NET 10 Desktop Runtime from Microsoft, once, if your PC does not have it.

## What stays on your PC
| What | Where |
|---|---|
| Layouts | `%APPDATA%\WoWBoxer\Layouts\profiles\*.json` |
| Settings: window titles, update check, Battle.net / WoW paths, the WoW account names you pick per slot (for the "last played" hint), your original hover-to-activate values | `%APPDATA%\WoWBoxer\Layouts\settings.json` |
| Logs, Diagnose reports and problem reports (`wowboxer-report-*.txt`) | `%LOCALAPPDATA%\WoWBoxer\Layouts\logs` |
| The community links file and when it was last read | `%LOCALAPPDATA%\WoWBoxer\Layouts\links.json`, `links.checked` |
| The app itself (installed copy) | `%LOCALAPPDATA%\WoWBoxer.Layouts` |

Logs and Diagnose reports replace your Windows user folder with `%USERPROFILE%`. Shared layouts (`WBX1:` codes and
layout files) contain only window positions, sizes, names you typed for the slots and class colours. They never
contain account names. Uninstalling removes the app. Delete the two `WoWBoxer` folders to remove your data too.

## What it reads and writes on your PC
- **Reads:** the list of running processes and windows (to find WoW and Battle.net), the monitor layout, and in your
  WoW folder: `WTF\Config.wtf` (display settings) and the folder names under `WTF\Account` (which accounts and
  characters have played on this PC). It never reads game memory.
- **Writes, only if you use it:** *Auto-fit action bar* sets `uiScale`/`useUiScale` in `Config.wtf` while all
  clients of that install are closed, and keeps a one-time backup `Config.wtf.wowboxer-backup`. Nothing else in the
  file changes.
- **Changes a Windows setting, only if you switch it on:** *Hover to activate windows* sets Windows' own "Activate a
  window by hovering over it with the mouse", its delay and "bring to front" for your Windows user (stored by Windows
  under `HKCU\Control Panel\Desktop`). Your values from before the first change are kept in `settings.json`
  (`hoverOriginal`) and put back by Settings › Restore, on quit if you choose, and when you uninstall.
- It never sends keystrokes or clicks to the game.

## Reporting a problem
**Report a problem** (Guide › If something looks wrong, Settings › Tools, the tray menu) builds a report on your PC:
app and Windows version, the layout, client count, monitors (resolution and scale), each slot's window status, the
action-bar / UI Scale state, the hover-to-activate setting and the last 50 log lines. Your Windows user folder and user
name, your WoW account names and your character names are replaced with `%USERPROFILE%`, `<user>`, `<account>` and
`<character>`. You see the whole report and can change anything before it leaves the window.

**Post in Discord** then copies it to the clipboard (one Discord message: your "What happened?" text and the report as
a code block), saves a longer report – plus a read-only Diagnose that moves no window, and the last 300 log lines, also
redacted – as a `.txt` in the logs folder, and opens the invite to `#setup-help` on the WoWBoxer Discord. You paste it
yourself; the app never posts anything, and has no webhook or bot token. The `.txt` is only shared if you drag it into
Discord yourself. Diagnose (Settings › Tools) still writes its own report file; nothing is uploaded.
