# Backlog (prioritised)

P1 = next, P2 = soon, P3 = later. Sources: design notes rounds 1–3 (project folder), field tests, own review.

## P1 – make it right for the Forever launch (4–5 Nov)
1. **Forever field test (M6) in game:** exe, folder, window class and display mode are known (login screen,
   `docs/wow-display-findings.md`); still to see: action bar, Edit Mode, UI after login. Run `wbx info` + `wbx snap`.
2. ~~Confirm "Fix action bar" in Classic Era~~ – done 2026-10-27 20:30: at UI Scale 0.93 the 830×664 followers show
   the whole bar (snapshots). Auto-fit does it by itself since v0.2.3.
2a. **Per-window action-bar fit belongs to the addon (workstream E).** WoW keeps one UI Scale per install, so the app
   can only pick the value the smallest window needs – the main window's UI shrinks too. Only an addon running in each
   client can scale just the action bar per character (e.g. `MainMenuBar:SetScale()` with a value per slot that the app
   hands over in a small file next to the addon before the clients start). Proposed to workstream E (2026-10-27): the addon sets `MainMenuBar:SetScale(min(1, UIParent:GetWidth() / 1024))` per client (out of combat) and reports `WoWBoxerDB.bridge.barFit = true`; when the app sees that for the squad's accounts, Auto-fit leaves the shared UI Scale alone. WS-E agreed (keys `bridge.barFit` + `bridge.barScale`, their backlog 8e35f16): opt-in in the addon, never on by default, after the owner's go and a taint check – so Auto-fit must keep working when `barFit` is absent or false (the common case).
2b. **"Make Forever windowed"**: write `GxMaximize "0"` while all Forever clients are closed (same guard and backup as
   UI Scale), because Forever starts in Windowed (Fullscreen) and cannot be placed then.
3. **Addon bridge (with workstream E):** read character, class, level and realm from the WoWBoxer addon's
   SavedVariables per account → fill name and class automatically (`docs/classes-and-accounts.md`).
4. **Release v0.2.3** (tag) and the SignPath application.
4b. **Owner field test of always-on-top with real alt-tab, clicks and hover-to-activate** (the automated test can only
   switch focus between our own windows – it never activates a WoW window).

## P2 – quality of life
5. Auto-select the layout that matches the current monitor setup (laptop vs desk).
6. Per-slot "remember this window's account" from Identify: click a row, then the matching window lights up.
7. Hotkey-free "Snap everything back" in the tray (= Arrange now) – already in the tray as Arrange now; rename for clarity.
8. Undo history list (not only Ctrl+Z one step at a time) and "Restore last good arrangement".
9. ~~Profiles per game~~ – done in v0.2.2 (layouts and last layout per game; account list follows the game's WTF).
10. Optional subtle UI sounds and an ambient particle layer (behind Animations; off by default).
11. Drag a row onto the canvas (or a canvas window onto a row) to assign.
12. ~~Real game-area state on the canvas~~ – done in v0.2.2 (solid dot = measured). Next: show the measured size
    on the Squad rows too.

## P3 – later
13. Locale (Danish), winget package.
14. Read layout codes straight from the website (deep link `wowboxer://layout/WBX1:…`).
15. Grey-zone features stay out unless the owner decides: swap-main hotkey (off by default, clearly labelled).
16. A tiny always-on-top "squad bar" with the checklist during launch.
