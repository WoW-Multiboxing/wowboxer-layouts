# WoWBoxer Window Layouts

Free, open-source (MIT) Windows app for **manual** World of Warcraft multiboxing.
It starts Battle.net and places 2–8 game windows in a layout you choose – one monitor or several.

**It never sends input to the game.** No key or mouse broadcasting, no automation, no login automation.
It only moves, resizes and restyles windows. See [privacy](docs/privacy.md).

## The app

A game-client style app (dark "war table" look, gold trim, class colours) with five tabs:

- **Setup** – I. how many clients (2–8) · II. pick a layout card with a live preview (Main + side, Overlays, Grid,
  2 monitors, Free for all) · III. fine-tune on a to-scale map of your monitors: drag, resize, snap, nudge, undo,
  class colour and name per window, and a verdict per window on whether Classic's action bar fits. Your open WoW
  windows can follow live. Layouts save automatically; share them as `WBX1:` codes or files, or capture an
  arrangement you made by hand.
- **Squad** – Start squad opens Battle.net and places each window as it opens (you press Play), with a live
  checklist and one row per character. Arrange now, Identify, reorder.
- **Guide** – a short quest-log guide: WoW display settings, switching windows, troubleshooting, the rules.
- **Settings** / **About** – live moves, window titles, Battle.net path, updates, Diagnose, shortcut, licence, privacy.

Tray icon and a desktop shortcut that starts a layout in one click.

Install: download `WoWBoxer.Layouts-win-Setup.exe` from Releases. It installs the .NET 10 Desktop Runtime if needed
and updates itself.

Tips:
- Set WoW's Display Mode to **Windowed**. Title bars stay on by default (removing them made WoW draw its UI at the
  wrong size in our tests).
- Classic Era's action bar is only complete when a window is **4:3 or wider**; narrower windows clip the bar's ends.
  Choose "4:3" under *Window rules* if you want the full bar. Details: [docs/wow-display-findings.md](docs/wow-display-findings.md).
- If windows do not move, Battle.net probably runs as administrator. Start it normally; Diagnose tells you.

## Command line (`wbx`)

The same engine without a window – also our test harness.

```
wbx start      start Battle.net, then place each WoW window as it opens (you press Play yourself)
wbx arrange    place the WoW windows that are open now, oldest first
wbx diagnose   write a report (monitors, windows, processes, admin rights, Config.wtf) and try one placement
wbx info       show monitors, the planned slots, the open WoW windows and the size each renders at
wbx capture    save the current window arrangement as a layout:  wbx capture --save my.json
wbx snap       save a picture of what each WoW window shows (read-only)
wbx restore    give WoW windows their title bars back
wbx layout     print a layout as JSON and as a WBX1: share code
```

Examples: `wbx arrange --preset grid`, `wbx start --profile my.json --names Paladin,Priest,Warlock`,
`wbx arrange --preset dual --clients 5`. Run `wbx help` for every option.

## Build

```
dotnet build WoWBoxer.Layouts.slnx
dotnet test WoWBoxer.Layouts.slnx
dotnet run --project src/WoWBoxer.Layouts.App
```

Releases: push a tag `vX.Y.Z`; `.github/workflows/release.yml` builds the Velopack installer and publishes it.
Code signing: [docs/code-signing-policy.md](docs/code-signing-policy.md).

World of Warcraft® is a trademark of Blizzard Entertainment, Inc. This project is not affiliated with or endorsed by Blizzard.
