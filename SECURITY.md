# Security policy

## Supported versions
Only the newest release gets fixes. Installed copies update themselves, so a fix reaches users with the next release.

## Reporting a vulnerability
Please **do not** open a public issue for a security problem.

Report it privately on GitHub: **[Security → Report a vulnerability](https://github.com/WoW-Multiboxing/wowboxer-layouts/security/advisories/new)**.
Include what you found, how to reproduce it, and what an attacker could do with it.

We aim to answer within 7 days and to ship a fix within 30 days for a confirmed problem. We will credit you in the
release notes unless you would rather stay anonymous.

## Scope
In scope: the app, `wbx`, the installer and updater as built by `.github/workflows/release.yml`, and anything that
could make the app send input to a game, read game memory or leak personal data. That would break the project's
hard rules, so we treat it as a security bug.

Out of scope: Blizzard's games and Battle.net, Windows itself, and SmartScreen warnings about unsigned builds
(see [docs/code-signing-policy.md](docs/code-signing-policy.md)).

## Verifying a download
Each release has `SHA256SUMS.txt`. In PowerShell: `Get-FileHash .\WoWBoxer-Setup.exe -Algorithm SHA256` and compare.
Releases are built only by GitHub Actions from a tagged commit of this repository.
