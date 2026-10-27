# Starting several clients – research (2026-10-27, owner's PC, Classic Era 1.15.9)

Rule: the app may **start** game processes. It never types credentials, never clicks Battle.net (Play, account picker)
and never sends input to the game. It only closes clients it started itself.

## What Battle.net does
Battle.net's Play starts the client as (read-only from the process list, all three of the owner's clients identical):

    WowClassic.exe -launcherlogin -uid wow_classic_era -initialgamemode=cera

`-launcherlogin` logs the client in with the Battle.net session and the **WoW account currently selected in Battle.net's
account dropdown**. The command line contains no account, so starting it N times would log in the same account N times –
and logging in an account that is already in game **disconnects that running client**. → The app must never start
clients with `-launcherlogin`. (Not tested live on purpose: it would have kicked one of the owner's running clients.)

`Battle.net.exe --exec="launch <code>"` does the same as Play (selected account only); Classic Era's code is unconfirmed
and it has the same one-account problem. Not used.

## Starting the client directly – tested
`WowClassic.exe` with **no arguments**, while Battle.net and three logged-in clients ran:
- opened a fourth window at the last saved windowed size (`gxWindowedResolution`, 807×696),
- showed WoW's own **login screen** (Account Name, Password, "Remember Account Name"),
- did not touch the three running sessions; closed again by its own PID. [verified]

So the compliant "launch all" is: **start N login screens, the user logs in to each himself** (typing, or with
"Remember Account Name" + password/authenticator). Accounts with several WoW licences get WoW's account picker after
login – also the user's click. The window watcher places each window as it appears.

## In the app
Squad tab → "How to start": *Battle.net – press Play per account* (default, as before) or *Open N login screens*.
The second starts only as many clients as are missing, remembers their process ids, and "Close clients I started"
only ever closes those. Game exe: found from a running client, the registry or the default folder; can be set in Settings.

## Not possible without breaking the rules
- Choosing the WoW account per instance automatically (needs Battle.net clicks or typed credentials).
- Mapping a window to its WoW account from outside: command lines are identical, logs are shared. Read-only hints only
  (see `docs/classes-and-accounts.md`).
