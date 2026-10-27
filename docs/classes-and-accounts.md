# Characters, classes and accounts

## What the app can know from outside (read-only, no game memory)
| Fact | Source | Reliable? |
|---|---|---|
| Which WoW accounts exist on this PC | folders `WTF\Account\<ACCOUNT>` next to the client | yes |
| Which characters each account has played here | `WTF\Account\<ACCOUNT>\<Realm>\<Character>` | yes |
| Which character an account played last | newest write time of those character folders | hint only (WoW writes on logout) |
| Class of a character | – (not in WTF files) | no |
| Which window belongs to which account | – (identical command lines, shared logs) | no |

So in the app (Squad tab): the user picks the **WoW account per slot once** (stored locally, never in shared layouts),
sees "Last played here: Name – Realm" with a **Use** button, and sets the **class by hand**. Until then the slot shows
**"Unknown class"** in a neutral colour – the app never guesses.

## The automatic route: the WoWBoxer addon (workstream E builds the addon side)
The addon runs inside each client, so it knows the character for certain. It may only *write its own SavedVariables*;
WoW saves them on logout and `/reload`.

Proposed contract (`WTF\Account\<ACCOUNT>\SavedVariables\WoWBoxer.lua`, account-wide variable `WoWBoxerDB`):

```lua
WoWBoxerDB = {
  ["bridge"] = {
    ["version"] = 1,
    ["character"] = "Name",        -- UnitName("player")
    ["realm"] = "Realm",           -- GetRealmName()
    ["class"] = "ROGUE",           -- select(2, UnitClass("player"))  (file token, English)
    ["level"] = 12,
    ["loggedInAt"] = 1793100000,   -- time() at PLAYER_LOGIN
    ["savedAt"] = 1793100500,      -- time() at PLAYER_LOGOUT / reload
  },
}
```

App side (planned):
1. For each slot with a chosen account, read that account's `SavedVariables\WoWBoxer.lua` (tiny Lua-table parser, read-only).
2. If `bridge.savedAt` is newer than the slot's last update: fill name and class, marked "from the WoWBoxer addon".
3. Refresh when the file changes (FileSystemWatcher) – i.e. after the player logs out, switches character or types `/reload`.
4. The account → window link stays the user's choice (row order); the addon cannot see the OS window either.

Optional later: the addon shows the slot number the app expects (read from a small file the app writes into the
addon's folder before the client starts) – only if workstream E wants it; it must stay a file, never input.
