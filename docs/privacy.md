# Privacy

WoWBoxer Window Layouts collects **no telemetry**, shows no ads and has no accounts. Nothing about you, your PC or
your characters is sent anywhere.

## The only network call
An installed copy asks GitHub Releases (`github.com/WoW-Multiboxing/wowboxer-layouts`) whether a newer version
exists when the app starts, and downloads it if so. That is a normal web request to GitHub. GitHub sees your IP
address, as with any download ([GitHub's privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement)).
No identifier, setting or usage data is sent. Switch it off in **Settings → Updates and privacy**
(or `"checkForUpdates": false` in `settings.json`). A portable copy or one built from source never checks.

The installer may also download the .NET 10 Desktop Runtime from Microsoft, once, if your PC does not have it.

## What stays on your PC
| What | Where |
|---|---|
| Layouts | `%APPDATA%\WoWBoxer\Layouts\profiles\*.json` |
| Settings: window titles, update check, Battle.net / WoW paths, the WoW account names you pick per slot (for the "last played" hint), your original hover-to-activate values | `%APPDATA%\WoWBoxer\Layouts\settings.json` |
| Logs and Diagnose reports | `%LOCALAPPDATA%\WoWBoxer\Layouts\logs` |
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
When you ask for help, you choose what to share. Diagnose writes the report to a file; nothing is uploaded.
