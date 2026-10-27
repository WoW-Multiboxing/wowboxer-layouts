# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/) (application pending).

- **What is signed:** `WoWBoxer.Layouts.exe`, `wbx.exe` and the installer built by the `release` workflow from this
  repository's source. Nothing else.
- **How:** release builds run on GitHub Actions only (`.github/workflows/release.yml`). Every signing request is
  approved manually by a maintainer.
- **Team:** committers and reviewers – the repository owner (WoW-Multiboxing); approvers – the repository owner.
- **Privacy:** see [privacy.md](privacy.md). The program sends no telemetry.
- **What the program does:** it only moves, resizes and restyles windows. It never sends keystrokes or mouse
  events to games, never reads game memory and never injects code.
