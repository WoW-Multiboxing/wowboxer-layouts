# 03 – Window program v1 (layout editor + arranger)

Research date: 27 Oct 2026. Starting point: `03_Output/Multibox-Launcher/ForeverLauncher.ps1`, which already does EnumWindows, SetWindowPos, borderless, PMv2 DPI, process-name detection, a 5:4 minimum aspect, auto-elevate and logging.
Hard rule for v1: **the program only moves and resizes windows. It never sends keys or clicks to a game client.**
Markers: **[verified]** = confirmed in a primary source, **[uncertain]** = single or secondary source, or our own empirical finding, **[derived]** = geometry we calculated ourselves.

---

## 1. Recommended stack

**Recommendation: C# on .NET 10 (LTS) + WPF, with P/Invoke to user32 (optionally via the CsWin32 generator). Ship it as a Velopack installer/updater from GitHub Releases.**

Reasons:
- **Reuse.** The `FmWin` C# class in ForeverLauncher.ps1 can be moved over almost unchanged. The whole app is one language, with no Rust↔JS bridge.
- **Drag/resize editor.** A WPF `Canvas` with `Thumb`/adorner controls is a standard, well-documented pattern for a monitor drawing with movable slots. AI coding assistants handle WPF/XAML well.
- **DPI.** WPF supports per-monitor DPI and, since .NET Framework 4.8, Per-Monitor V2 and mixed-mode scaling. Declare `PerMonitorV2` in the app manifest [verified, MS docs]. Note one open dotnet/wpf issue (#9803) about DPI settings on .NET 8 [uncertain, unresolved status].
- **Lifetime.** .NET 8 and 9 reach end of support on **10 Nov 2026**. .NET 10 is LTS and supported to Nov 2028 [verified].
- **Packaging caveat.** The .NET SDK disables trimming for WPF and WinForms [verified]. A self-contained single-file exe is therefore tens of MB (a ~70 MB anecdote [uncertain]). The alternative is a small framework-dependent exe that requires the .NET Desktop Runtime. Velopack produces an installer plus delta updates from a publish folder and supports GitHub Releases as the update source [verified, docs].

| Option | Size | Drag editor | Win32 interop | PMv2 DPI | Packaging/update | AI maintainability | Verdict |
|---|---|---|---|---|---|---|---|
| **.NET 10 WPF** | tens of MB self-contained, small framework-dependent | Good (Canvas/Thumb) | Native P/Invoke, reuse existing code | Yes (manifest) | Velopack, MSIX, winget | High | **Pick** |
| WinForms | Similar | Weaker (manual painting) | Native | "Limited automatic" per MS | Same | High | Fallback |
| WinUI 3 | Windows App SDK runtime on top | Good | P/Invoke | Yes | MSIX-centric, more moving parts [uncertain] | Medium | Not needed |
| Tauri 2 | Small (a few MB, WebView2 ships with Win10 1803+/Win11) [verified] | Very good (HTML/JS, could share UI with the Companion) | Rust `windows` crate, two languages | WebView handles UI. Rust side must handle PMv2 itself | NSIS/MSI + updater plugin | Medium | Runner-up |
| Electron | ~85+ MB [uncertain] | Very good | Needs native module | OK | electron-builder | High | Too heavy |
| AutoHotkey v2 | Tiny | Poor GUI | Easy WinMove | Manual | Compiled exe | Medium | Avoid: frequent AV false positives [uncertain, forum reports], and its "macro tool" image is bad next to a Blizzard-compliance claim |
| Keep PowerShell | Tiny | None realistic | Add-Type C# (works today) | Yes | .cmd files | Medium | Keep only as CLI/diagnose fallback. Blocked under Constrained Language Mode (already detected in the script) |

---

## 2. Code signing and SmartScreen (Oct 2026)

- **Azure Artifact Signing** (formerly Trusted Signing, GA Jan 2026): ~$9.99/month. **Individuals: USA and Canada only.** Organizations: USA, Canada, EU and UK [verified, MS Learn, updated 29 Aug 2026]. **The maintainer, as an individual in Denmark, does not qualify.** They would need a company in the EU.
- **OV certificate:** $150–300/year, worldwide, key on HSM/token since June 2023 [verified]. Certum sells cheaper options (cloud cert from €49, "open source" tier) [uncertain: price and Authenticode support not confirmed].
- **EV certificate:** $400+/year. It **no longer bypasses SmartScreen (removed 2024)** [verified].
- **SignPath Foundation:** free for OSI-licensed open source with no proprietary parts. Builds must be automated (e.g. GitHub Actions), every release needs manual approval, a "Code signing policy" page is required, the certificate is issued in SignPath Foundation's name, and the project needs some verifiable reputation [verified, signpath.org/terms]. **Best fit if v1 is open source.**
- **Microsoft Store (MSIX):** free signing, no SmartScreen warnings [verified]. A Win32 app that moves other processes' windows must pass Store review [uncertain].
- **Unsigned:** "strong SmartScreen block". Users have to click *More info → Run anyway* [verified]. Even signed files start with low reputation and build it per file/publisher over time. Microsoft publishes no threshold and offers no allow-list program [verified/uncertain mix, MS Q&A].
- **Antivirus:** tools that enumerate processes, change other windows' styles and self-elevate look like PUP/hacktool heuristics. No WPF-specific evidence was found. AHK-compiled exes and packed (UPX) binaries are often flagged [uncertain]. Mitigations: don't pack, sign, publish source, submit false positives to Microsoft.
- **winget:** submissions are validated automatically, including Defender/SmartScreen scans. Unsigned installers appear to be accepted in practice, but no official statement was found [uncertain].

---

## 3. Win32 and WoW pitfalls

1. **Mixed DPI.** Run as PMv2 so all coordinates are physical pixels [verified]. Read per-monitor DPI with `GetDpiForMonitor` and per-monitor work areas with `EnumDisplayMonitors`. Re-query on `WM_DISPLAYCHANGE`/`WM_DPICHANGED`. WOB needed a dedicated fix for non-100 % scaling (v4.8.3, Aug 2025) [verified], so this is a known trap.
2. **Borderless.** Remove `WS_CAPTION|WS_THICKFRAME|WS_SYSMENU|WS_MINIMIZEBOX|WS_MAXIMIZEBOX`, then `SetWindowPos(...SWP_FRAMECHANGED)`. WoW resizes itself while loading, which is why the current launcher re-applies placement for 30 s [our empirical finding]. v1 should keep a short "watch and re-apply" window per new client.
3. **Display mode conflict.** Our launcher's README says **"Windowed (Fullscreen)" cannot be resized**, so use "Windowed". The WOB FAQ instead recommends "Fullscreen (Windowed)" with default window size, or Windowed with matching size and **render scale 100 %**, to avoid pixelation after swaps [verified as WOB's claim]. **Inconsistent, so test on Classic Era/Forever.**
4. **Config.wtf CVars** (set per client instead of forcing size from outside):
   - `gxWindow 1` = windowed [uncertain, community guide].
   - `gxMaximize` is ignored unless windowed, 1 = maximized windowed [verified, Wowpedia].
   - `gxWindowedResolution "WxH"` sets the window size [uncertain, retail forum workaround].
   - `windowResizeLock` [uncertain, archived WoWWiki, may be gone].
   - `gxMonitor` picks the monitor [Wowpedia].
   - No CVar for window **position** was found.
   - Per-client config files are possible with `Wow.exe -config Config-WOB1.wtf` (WOB's launchwow.bat) [verified that WOB does this]. This bypasses Battle.net, and whether direct launch still works for Classic Era/Forever in 2026 is **unverified**. The client may also overwrite Config.wtf on exit.
5. **Minimum aspect / letterboxing.** The 5:4 threshold (`MinAspect 1.25`) is **our own empirical value, not documented by Blizzard** [uncertain]. Make it a per-profile setting.
6. **Foreground/focus.** `SetForegroundWindow` only succeeds under the conditions Microsoft lists (e.g. the caller received the last input event). Otherwise the taskbar button flashes [verified]. When placing windows, always use `SWP_NOACTIVATE|SWP_ASYNCWINDOWPOS` (already done) so the tool never steals focus.
7. **Elevation (UIPI).** A lower-integrity process cannot message or manipulate a higher-integrity one [verified, general UIPI]. Our Diagnose sees error 5. WOB's advice is to restart Battle.net **not** as admin [verified]. Prefer telling the user to un-elevate Battle.net over auto-elevating the tool, because a tool that self-elevates looks worse to antivirus.

---

## 4. Existing tools and layout patterns

- **WowOpenBox (WOB2025)**: GPL-3, Tcl/Tk, 4.8.3. Features [verified]:
  - layout wizard with drag/resize, snap to grid and arrow-key nudging
  - auto-capture of new windows
  - borderless
  - swap hotkeys (`Ctrl-Shift-F2…` swaps window N into the main slot)
  - focus hotkeys and focus-follows-mouse
  - optional "stay on top"
  - **The July 2025 lesson:** WOB removed its Round-Robin feature after "Blizzard's sudden apparent flagging (without warning)". Its compliance is self-declared and still unconfirmed by Blizzard (issue #134).
- **ISBoxer** concepts: Regions, Home Region per slot, Swap Groups with an Active ("main") region, Roaming Slot, Reset Region that tries to change the game resolution [verified, ISBoxer wiki]. WOB's comparison table marks ISBoxer as "banned" for WoW [WOB claim].
- **FancyZones**: Grid and Canvas editors with per-monitor layouts. Good UX reference [verified].
- **Jambaboxer:** nothing found, so not assessed.

### Standard layouts [derived]

Assumptions: work area is used (taskbar excluded). Small windows are kept ≥ 5:4, or shrunk/anchored like today. Sizes below are for 1920×1080, 3440×1440 and 2×1920×1080.

| Clients | 16:9 single (1920×1080) | 21:9 (3440×1440) | Dual 16:9 |
|---|---|---|---|
| 2 | Main 1440×1080 + 480×384 top-right (rest free for chat/tools) | Side-by-side 2× 1720×1376 (5:4 fit) | One client per monitor, full screen |
| 3 | **Current launcher:** main 1245×1080 + 2× 675×540 right column | Main 2560×1440 + 2× 880×704 right column | Mon 1 main. Mon 2: 2× 960×768 side-by-side |
| 4 | Main 1440×1080 + 3× 480×360 right column, **or** 2×2 grid 960×540 | Main 2560×1440 + 3× 880×480 | Mon 1 main. Mon 2: 2×2 grid (3× 960×540) |
| 5 | Main 1440×1080 + 4× 480×270 right column | Main 2560×1440 + 4× 880×360 | Mon 1 main. Mon 2: 2×2 grid 4× 960×540 |
| 6+ | Grid only (windows get very small) | Main + 2-column strip | Mon 2 grid 3×2 (640×540) |

Notes:
- "Main" = the client you are playing at the moment. The forum consensus from boxers is that one large main beats many equal small windows [uncertain, forum anecdotes].
- On 1080p, 3-client main of 1245 px wide is narrower than 5:4. The launcher accepts this today, so verify there is no letterbox.

---

## 5. Hotkey "swap main" (window-only)

- **Technically:** `RegisterHotKey` in our app, then exchange two clients' rectangles with `SetWindowPos` (`SWP_NOACTIVATE`). No input reaches the game. Do **not** combine it with automatic focus switching in v1.
- **Policy:** Blizzard's support article 24258 says multiboxing is allowed and **input broadcasting** may be penalized [verified]. The May 2021 policy adds that it bans tools that "**automate or streamline multi-boxing in any way**" [verified, quoted]. A layout swap is plausibly fine and is WOB's core feature. However, "streamline" is vague, and Blizzard flagged WOB's round-robin without warning. **Grey zone, not officially confirmed.** Ship it **off by default**, with a clear note, one window move per key press, no queues or timers.

---

## 6. MVP feature list (v1)

1. **Monitor detection:** count, resolution, work area and DPI per monitor, drawn to scale.
2. **Layout editor:**
   - drag/resize slots with snap and numeric fields
   - live aspect indicator that warns below the minimum aspect
   - main/secondary labels
3. **Preset gallery:** the table above for 2/3/4/5/6 clients, adapted to the detected monitors, plus free/custom layouts.
4. **Profiles per squad (JSON):**
   - layout
   - slot names/characters and Battle.net account labels
   - game (Classic Era / Forever)
   - borderless, rename, minimum aspect
5. **Launch-order helper:**
   - starts Battle.net
   - shows "press Play for slot 1, 2, 3 …"
   - the next new window fills the next free slot, and closed windows free their slot
6. **Arrange now:** oldest-first, or remembered process→slot mapping.
7. **Diagnose/export log**, same as today.
8. **Elevation guidance:** detect an elevated client and explain the Battle.net fix. Auto-elevate is opt-in.
9. **Optional (off by default):** swap-main hotkey (§5).
10. **About/compliance screen:** "moves windows only, never sends input", with a link to the source.

Out of v1: focus switching, focus-follows-mouse, overlays, PiP/always-on-top, launching Wow.exe directly with `-config`.

---

## 7. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Blizzard reinterprets "streamline" (as with WOB's round-robin, 2025) | Account actions for users | Window-only scope, swap off by default, open source, no timers or queues |
| SmartScreen/antivirus warnings | Users don't install | SignPath (if OSS) or OV cert, no packing, publish hashes |
| Individual Artifact Signing unavailable in DK | Higher signing cost | SignPath, OV, or an EU company |
| WoW resets size/style, display-mode differences | "Doesn't work" reports | Re-apply window, test Windowed vs Windowed (Fullscreen), Diagnose |
| Mixed-DPI and multi-monitor bugs | Wrong placement | PMv2 everywhere, test matrix (100/125/150 %, mixed) |
| Forever client differs (exe/class) | Detection fails | Process name + path fallback (already there), Diagnose |
| .NET 8/9 EOL 10 Nov 2026 | Unsupported runtime | Target .NET 10 |

---

## Sources

- Blizzard Support 24258 – https://us.battle.net/support/en/article/24258
- Blizzard policy update May 2021 (quoted) – https://www.warcrafttavern.com/wow/news/blizzard-updates-policy-regarding-input-broadcasting/ (original: https://us.forums.blizzard.com/en/wow/t/policy-update-for-input-broadcasting-may-2021/956613)
- WowOpenBox repo, FAQ, Compare, Releases, Help – https://github.com/WowOpenBox/WowOpenBox · https://github.com/WowOpenBox/WowOpenBox/wiki/FAQ · https://github.com/WowOpenBox/WowOpenBox/wiki/Compare · https://github.com/WowOpenBox/WowOpenBox/releases · https://wowopenbox.org/help
- WOB launchwow.bat (`-config`) – https://raw.githubusercontent.com/WowOpenBox/WowOpenBox/main/launchwow.bat
- ISBoxer Window Layout – https://isboxer.com/wiki/Window_Layout
- FancyZones – https://learn.microsoft.com/windows/powertoys/fancyzones
- MS code signing options – https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- Artifact Signing quickstart – https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart
- SignPath Foundation terms – https://signpath.org/terms
- SmartScreen reputation Q&A – https://learn.microsoft.com/en-us/answers/a/12717557
- Certum code signing – https://shop.certum.eu/code-signing.html
- High-DPI desktop development – https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows
- SetForegroundWindow – https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow
- UIPI – https://en.wikipedia.org/wiki/User_Interface_Privilege_Isolation
- Trimming incompatibilities (WPF/WinForms) – https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities
- dotnet/wpf #9803 – https://github.com/dotnet/wpf/issues/9803
- .NET 8/9 end of support – https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/
- Velopack – https://docs.velopack.io/integrating/overview
- Tauri Windows installer / WebView2 – https://v2.tauri.app/distribute/windows-installer/
- Tauri size comparison (secondary) – https://noqta.tn/en/blog/tauri-2-desktop-apps-rust-web-technologies-2026
- Wowpedia gxMaximize – https://wowpedia.fandom.com/wiki/CVar_gxMaximize
- gxWindowedResolution workaround – https://us.forums.blizzard.com/en/wow/t/windowed-mode-resizing-quits-wow/1188554
- WoWWiki console variables (archive) – https://wowwiki-archive.fandom.com/wiki/Console_variables
- AHK false positives (forum) – https://www.autohotkey.com/boards/viewtopic.php?p=379105
- winget repository policy – https://learn.microsoft.com/en-us/windows/package-manager/package/repository
