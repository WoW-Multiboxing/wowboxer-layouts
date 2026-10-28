# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/). *(Application in progress. Until it is approved, releases are unsigned.)*

## What is signed
Only files built from this repository's source by the `release` workflow on GitHub Actions:
- `WoWBoxer.Layouts.exe`, `WoWBoxer.Layouts.*.dll`, `wbx.exe`, `wbx.dll` (our own binaries, signed before packing),
- the Velopack installer `WoWBoxer.Layouts-win-Setup.exe` (copied as `WoWBoxer-Setup.exe`).

Third-party files (the .NET runtime, Velopack, NuGet packages) are not re-signed by us.

## How
- Builds run only on GitHub-hosted runners from a pushed tag (`.github/workflows/release.yml`). Nobody signs files
  built on a personal PC.
- Each signing request is **approved manually** by an approver in SignPath before the certificate is used.
- Everyone with a role below uses multi-factor authentication on GitHub and SignPath.

## Team roles
| Role | Who |
|---|---|
| Committers and reviewers | Members of the [WoW-Multiboxing](https://github.com/WoW-Multiboxing) GitHub account. External contributions arrive as pull requests and are reviewed before merge |
| Approvers | The repository owner (WoW-Multiboxing) |

## Privacy
This program will not transfer any information to other networked systems unless specifically requested by the user
or the person installing or operating it. The one exception is the update check against GitHub Releases, which runs
at start for installed copies, sends no data about the user and can be switched off in Settings.
See [privacy.md](privacy.md).

## What the program does
It only moves, resizes and restyles windows. It never sends keystrokes or mouse events to games, never reads game
memory and never injects code. Source: <https://github.com/WoW-Multiboxing/wowboxer-layouts> (MIT).

---

## Maintainers: SignPath Foundation application (ready to paste)

Apply at <https://signpath.org/apply> (needs the owner's account; the repository must be public with a release).

| Field | Answer |
|---|---|
| Project name | WoWBoxer Window Layouts |
| Repository | https://github.com/WoW-Multiboxing/wowboxer-layouts |
| Homepage | https://github.com/WoW-Multiboxing/wowboxer-layouts (switch to the website once it has a public domain) |
| Licence | MIT (OSI approved), `LICENSE` in the repository root, no proprietary parts |
| Short description | A free Windows desktop app that places several World of Warcraft game windows in a chosen layout (one or more monitors) for manual multiboxing. It only moves and resizes windows; it never sends input to games. |
| Programming language / platform | C# / .NET 10 / WPF, Windows 10 and 11 x64 |
| Build system | GitHub Actions on GitHub-hosted `windows-latest` runners; workflow `.github/workflows/release.yml`, triggered by version tags |
| Artifacts to sign | Portable executables (`.exe`, `.dll`) of our own assemblies, and the Velopack `Setup.exe` installer |
| Release distribution | GitHub Releases (also linked from the project website); in-app updates via Velopack from the same releases |
| Code signing policy URL | https://github.com/WoW-Multiboxing/wowboxer-layouts/blob/main/docs/code-signing-policy.md |
| Privacy policy URL | https://github.com/WoW-Multiboxing/wowboxer-layouts/blob/main/docs/privacy.md |
| Does it collect user data / telemetry? | No. The only network traffic is the optional update check against GitHub Releases. |
| Does it bundle adware, crypto miners, or install other software? | No. The installer may install Microsoft's .NET 10 Desktop Runtime if missing (official Microsoft installer). |
| Does it modify other software? | No code or memory changes. It moves/resizes other programs' windows through standard Windows APIs, and optionally writes the documented `uiScale` setting in the game's own `Config.wtf` text file when the game is closed (with a backup). |
| Team | Owner/approver: the WoW-Multiboxing GitHub account (MFA on). No other maintainers yet. |
| Why signing | Unsigned downloads show a SmartScreen warning that scares away non-technical players; signing lets users verify that releases come from this repository's CI. |

### Switching signing on after approval
1. In SignPath: create project slug `wowboxer-layouts`, signing policy `release-signing` (manual approval), and two
   artifact configurations:
   - `app-binaries`: a zip containing `*.exe` and `*.dll` → sign each PE file with Authenticode;
   - `installer`: a zip containing `*-Setup.exe` → sign it with Authenticode.
   Link the trusted build system "GitHub.com" to the project.
2. In this repo (Settings → Secrets and variables → Actions):
   - secret `SIGNPATH_API_TOKEN` (a SignPath CI user token with *submitter* role),
   - variables `SIGNPATH_ORGANIZATION_ID` (from SignPath) and `SIGNPATH_PROJECT_SLUG` = `wowboxer-layouts`,
   - variable `SIGNPATH_ENABLED` = `true`.
3. Tag a patch release. The workflow pauses until an approver approves each of the two signing requests in SignPath.
