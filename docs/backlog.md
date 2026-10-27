# Backlog (prioritised)

P1 = next, P2 = soon, P3 = later. Sources: design notes rounds 1–3 (project folder), field tests, own review.

## P1 – make it right for the Forever launch (4–5 Nov)
1. **Forever field test (M6):** exe name, window class, display mode, action bar; run `wbx diagnose` + `wbx snap`.
2. **Verify the in-game action-bar fix once** with the owner: set UI Scale on one follower, then check where WoW wrote
   it (shared `Config.wtf` or the account's `config-cache.wtf`). If per account: offer "write UI scale for this
   account" – only while that client is closed.
3. **Addon bridge (with workstream E):** read character, class, level and realm from the WoWBoxer addon's
   SavedVariables per account → fill name and class automatically (`docs/classes-and-accounts.md`).
4. **Release v0.2.0** (tag) and the SignPath application.

## P2 – quality of life
5. Auto-select the layout that matches the current monitor setup (laptop vs desk).
6. Per-slot "remember this window's account" from Identify: click a row, then the matching window lights up.
7. Hotkey-free "Snap everything back" in the tray (= Arrange now) – already in the tray as Arrange now; rename for clarity.
8. Undo history list (not only Ctrl+Z one step at a time) and "Restore last good arrangement".
9. Profiles per game (Classic Era / Forever) with separate account lists.
10. Optional subtle UI sounds and an ambient particle layer (behind Animations; off by default).
11. Drag a row onto the canvas (or a canvas window onto a row) to assign.
12. Show each window's real game-area size live on the canvas (from `wbx info` data) next to the planned size.

## P3 – later
13. Locale (Danish), winget package.
14. Read layout codes straight from the website (deep link `wowboxer://layout/WBX1:…`).
15. Grey-zone features stay out unless the owner decides: swap-main hotkey (off by default, clearly labelled).
16. A tiny always-on-top "squad bar" with the checklist during launch.
