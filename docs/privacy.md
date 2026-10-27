# Privacy

WoWBoxer Window Layouts collects **no telemetry** and has no accounts.

- **What stays on your PC:** your layouts (`%APPDATA%\WoWBoxer\Layouts\profiles`), settings
  (`%APPDATA%\WoWBoxer\Layouts\settings.json`) and logs (`%LOCALAPPDATA%\WoWBoxer\Layouts\logs`). Logs and Diagnose
  reports replace your Windows user folder with `%USERPROFILE%`, so they are safe to send when you ask for help.
- **The only network call:** an installed copy checks GitHub Releases
  (`github.com/WoW-Multiboxing/wowboxer-layouts`) for a new version at start. No data about you or your PC is sent
  beyond a normal web request. Switch it off with `"checkForUpdates": false` in `settings.json`.
- **What it reads on your PC:** the list of running processes and windows (to find WoW), and WoW's `Config.wtf`
  display settings for Diagnose. It never reads game memory and never sends input to the game.
