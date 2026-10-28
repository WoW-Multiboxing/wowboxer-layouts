# Contributing

Thanks for helping. Bug reports, layout ideas and pull requests are all welcome.

## The hard rules (a change that breaks one is not merged)
The app **only moves, resizes and restyles windows.**
- No keystrokes, mouse events or input hooks sent to or aimed at a game. No input broadcasting, round-robin or
  focus-switch hotkeys.
- No reading of game memory, no injection, no login automation, no stored passwords.
- Never steal focus: windows are placed with `SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS`.
- The one Windows setting the app may change is hover to activate (`SystemParametersInfo`), only on the user's click,
  with the user's original values kept and restorable.
- No telemetry. The update check stays the only network call. Logs stay local and hide the user folder.

## Reporting a bug
Open an [issue](https://github.com/WoW-Multiboxing/wowboxer-layouts/issues/new/choose) with the bug template. A
**Diagnose** report (Settings → Tools → Diagnose, or `wbx diagnose`) helps most. It hides your Windows user name.
Look through it before attaching and remove anything you would rather not share. Security problems go to
[SECURITY.md](SECURITY.md) instead.

## Building and testing
See [Build from source](README.md#build-from-source). Before a pull request:

```
dotnet build WoWBoxer.Layouts.slnx -c Release
dotnet test WoWBoxer.Layouts.slnx -c Release
```

The build treats warnings as errors. Layout maths belongs in `WoWBoxer.Layouts.Core` with unit tests. Code that calls
Windows belongs in `WoWBoxer.Layouts.Win32`. The `tools/` scripts test against fake WoW windows
(`tools/fakewow/FakeWow.cs`), so you do not need the game to test placement.

## Pull requests
- Small and focused, with a clear description of what changes for the user and how you tested it.
- Code, UI text and docs in English. Match the style around your change.
- Add a line to `CHANGELOG.md` under the next version.
- No personal data in commits: account or character names, paths with your user name, Diagnose reports.
- By contributing you agree that your contribution is licensed under the [MIT licence](LICENSE).

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).
