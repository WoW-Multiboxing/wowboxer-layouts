# WoWBoxer Window Layouts

Free, open-source (MIT) Windows app for **manual** World of Warcraft multiboxing.
It starts Battle.net and places 2–N game windows in a layout you choose – one monitor or several.

**It never sends input to the game.** No key or mouse broadcasting, no automation, no login automation.
It only moves and resizes windows.

Status: early development (milestone M1: command line). See [`docs/plan.md`](docs/plan.md) for the plan and milestones.

## Command line (`wbx`)

Needs Windows 10/11 and the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

```
wbx start      start Battle.net, then place each WoW window as it opens (you press Play yourself)
wbx arrange    place the WoW windows that are open now, oldest first
wbx diagnose   write a report (monitors, windows, processes, admin rights) and try one placement
wbx info       show monitors, the planned slots and the open WoW windows (moves nothing)
wbx layout     print the layout as JSON and as a WBX1: share code
```

Examples:

```
wbx arrange                                   main left, followers stacked right (5:4)
wbx arrange --preset grid
wbx start --clients 3 --names Paladin,Priest,Warlock --game forever
wbx start --preset dual --clients 5           main on monitor 1, followers on monitor 2
wbx layout --preset overlay --clients 4 --json > overlay4.json
wbx arrange --profile overlay4.json
```

Presets (2–8 clients): `side` (default), `overlay` (followers float on top of the main window),
`grid`, `dual` (needs two monitors). Run `wbx help` for every option.

Tips:
- Set WoW's Display Mode to **Windowed**.
- Title bars stay on by default: without them WoW can draw its action bars at the wrong size. `--borderless` removes them anyway.
- Your own layout: arrange the windows by hand (Windows Snap, mouse), then `wbx capture --save my.json`.
  From then on `wbx arrange --profile my.json` (or `wbx start --profile my.json`) puts them back exactly.
- If windows do not move, Battle.net probably runs as administrator. Start it normally and try again;
  `wbx diagnose` tells you.
- Logs stay on your PC: `%LOCALAPPDATA%\WoWBoxer\Layouts\logs`. Nothing is sent anywhere.

## Build

```
dotnet build WoWBoxer.Layouts.slnx
dotnet test WoWBoxer.Layouts.slnx
dotnet publish src/WoWBoxer.Layouts.Cli -c Release -o publish/wbx
```

World of Warcraft® is a trademark of Blizzard Entertainment, Inc. This project is not affiliated with or endorsed by Blizzard.
