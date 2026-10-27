<#
  Multibox Launcher - opens and arranges your WoW clients on one screen (Forever or Classic Era).

  What it does (and nothing more):
    * starts the Battle.net app if it is not running,
    * watches for World of Warcraft windows and snaps each new one into its place
      (1st window = slot 1, 2nd = slot 2, 3rd = slot 3),
    * optionally removes the window title bars and renames the windows.
  It never sends key presses or mouse clicks to the game. You still press Play in Battle.net
  (choosing the account in the account dropdown) and you log in yourself.

  Modes (normally started through the .cmd files next to this script):
    -Mode Start     start Battle.net, then place each WoW window as it opens   (Start-*.cmd)
    -Mode Arrange   place the WoW windows that are already open, oldest first   (Arrange-*.cmd)
    -Mode Shortcut  create a desktop shortcut                                   (Create-Shortcut*.cmd)
    -Mode Info      only print the screen size and the planned window positions
    -Mode Diagnose  write diagnose.txt next to this script (what Windows sees) and try one placement

  Game (only changes names and the instructions - window handling is identical):
    -Game ClassicEra  (default)  shortcut "Classic Era Trio"
    -Game Forever                shortcut "Forever Trio" (start files in the Forever folder)

  Every run writes last-run.log next to this script.
#>
param(
  [ValidateSet('Start', 'Arrange', 'Shortcut', 'Info', 'Diagnose')]
  [string]$Mode = 'Start',
  [ValidateSet('Forever', 'ClassicEra')]
  [string]$Game = 'ClassicEra'
)

# =============================== SETTINGS (edit freely) ===============================
# Window slots in % of the screen (working area = screen minus taskbar). Order = order the windows open.
$Slots = @(
  @{ Name = 'Paladin'; X = 0;  Y = 0;  W = 70; H = 100; Anchor = 'Top'    },
  @{ Name = 'Priest';  X = 70; Y = 0;  W = 30; H = 50;  Anchor = 'Bottom' },
  @{ Name = 'Warlock'; X = 70; Y = 50; W = 30; H = 50;  Anchor = 'Top'    }
)
# Battle.net account (license) names in the same order as the slots - only used for the on-screen instructions.
$Accounts       = @('Account1', 'Account2', 'Account3')
# Classic Era: same slots, other names (your current Classic characters). Edit to match.
$ClassicEraNames = @('Warrior', 'Priest', 'Warlock')
$Monitor        = 'Primary'   # 'Primary' or a number (1 = first screen Windows reports, 2 = second ...)
$UseTaskbarArea = $false      # $false = stay above the taskbar; $true = use the whole screen
$Borderless     = $true       # remove title bars and borders so the windows tile edge to edge
$RenameWindows  = $true       # window titles become "WoW - Paladin" etc. (helps on the taskbar)
# WoW shows black bars when a window is narrower than about 5:4. With AvoidBlackBars the tool shrinks the
# height of such a slot to 5:4 and places it by its Anchor (Top / Center / Bottom) instead of letterboxing.
$AvoidBlackBars = $true
$MinAspect      = 1.25        # width / height below which WoW letterboxes (5:4). Adjust if you still see bars.
# FillHeight: widen the right column (to about 34 %) so the two small windows are 5:4 AND fill the full height.
# The main window gets the rest (about 66 %) and keeps the full height. $false = use the % widths in $Slots.
$FillHeight     = $true
# ResizeNudge: after placing, briefly resize each window by 2 px and back. This makes WoW rebuild its picture for the
# new window size. Without it the game can keep drawing at its old size, so the UI (action bars, minimap) looks cut off/offset.
$ResizeNudge    = $true
$AutoElevate    = $true       # if Windows blocks the move because WoW runs as administrator, restart this tool as administrator
$StartBattleNet = $true
$BattleNetExe   = ''          # leave empty to find it automatically
$WatchMinutes   = 10          # Start mode stops watching after this long, or 30 s after the last window is placed
# =======================================================================================

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $PSCommandPath
$isAdmin = $false

# Any unexpected error: show it, keep it in the log, exit with code 1 (the .cmd file then pauses).
trap {
  Write-Host ''
  Write-Host ('  ERROR: ' + $_.Exception.Message) -ForegroundColor Red
  if ($_.InvocationInfo) { Write-Host ('  ' + $_.InvocationInfo.PositionMessage) -ForegroundColor DarkGray }
  try { Stop-Transcript | Out-Null } catch { }
  exit 1
}

if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') {
  Write-Host ('  PowerShell runs in ' + $ExecutionContext.SessionState.LanguageMode + ' on this PC (a security policy).') -ForegroundColor Red
  Write-Host '  This tool needs FullLanguage to talk to Windows, so it cannot run here.' -ForegroundColor Red
  exit 1
}

try {
  $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
} catch { }

$logName = if ($Mode -eq 'Diagnose') { 'diagnose.txt' } elseif ($isAdmin) { 'last-run-admin.log' } else { 'last-run.log' }
try { Start-Transcript -Path (Join-Path $here $logName) -Force | Out-Null } catch { }

if ($Game -eq 'ClassicEra') {
  for ($i = 0; $i -lt [math]::Min($Slots.Count, $ClassicEraNames.Count); $i++) { $Slots[$i].Name = $ClassicEraNames[$i] }
  $GameLabel = 'Classic Era'; $ShortcutName = 'Classic Era Trio'; $StartCmd = 'Start-ClassicEra.cmd'
  $PickGame  = 'Pick "World of Warcraft Classic", set the version dropdown to "Classic Era".'
} else {
  $GameLabel = 'Forever';     $ShortcutName = 'Forever Trio';     $StartCmd = 'Forever\Start-Forever.cmd'
  $PickGame  = 'Pick "World of Warcraft", set the Game Version menu above Play to "World of Warcraft: Forever".'
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
public static class FmWin {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SetWindowText(IntPtr hWnd, string text);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr proc, int flags, StringBuilder name, ref int size);
    [DllImport("advapi32.dll")] public static extern bool OpenProcessToken(IntPtr proc, uint access, out IntPtr token);
    [DllImport("advapi32.dll")] public static extern bool GetTokenInformation(IntPtr token, int cls, out int info, int len, out int retLen);

    public static string ClassOf(IntPtr h) { StringBuilder sb = new StringBuilder(256); GetClassName(h, sb, sb.Capacity); return sb.ToString(); }
    public static string TitleOf(IntPtr h) { StringBuilder sb = new StringBuilder(512); GetWindowText(h, sb, sb.Capacity); return sb.ToString(); }
    public static uint Pid(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid; }
    public static string ProcName(uint pid) { try { using (Process p = Process.GetProcessById((int)pid)) { return p.ProcessName; } } catch { return "?"; } }
    public static bool IsWowName(string n) {
        if (n == null) return false;
        n = n.ToLowerInvariant();
        return n.StartsWith("wow") && !n.Contains("voice") && !n.Contains("error") && !n.Contains("crash");
    }
    public static string ImagePath(uint pid) {
        IntPtr hp = OpenProcess(0x1000, false, pid);
        if (hp == IntPtr.Zero) return "";
        try { StringBuilder sb = new StringBuilder(1024); int size = sb.Capacity; return QueryFullProcessImageName(hp, 0, sb, ref size) ? sb.ToString() : ""; }
        finally { CloseHandle(hp); }
    }
    // Fallback for a client with an unexpected exe name (e.g. Forever): any game exe inside a "World of Warcraft" folder.
    public static bool IsWowPath(uint pid) {
        string path = ImagePath(pid).ToLowerInvariant();
        if (!path.Contains("\\world of warcraft\\")) return false;
        string file = path.Substring(path.LastIndexOf('\\') + 1);
        return !file.Contains("launcher") && !file.Contains("battle.net") && !file.Contains("agent") && !file.Contains("error") && !file.Contains("crash") && !file.Contains("voice");
    }
    // visible top-level windows that have no owner (real app windows, not tooltips or dialogs)
    public static List<IntPtr> TopWindows() {
        List<IntPtr> list = new List<IntPtr>();
        EnumWindows(delegate (IntPtr h, IntPtr l) {
            if (IsWindowVisible(h) && GetWindow(h, 4) == IntPtr.Zero) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }
    // WoW game windows: window class GxWindowClass*, or a big window owned by a Wow*.exe process
    public static List<IntPtr> FindGameWindows() {
        Dictionary<uint, bool> wow = new Dictionary<uint, bool>();
        foreach (Process p in Process.GetProcesses()) {
            try { if (IsWowName(p.ProcessName)) wow[(uint)p.Id] = true; } catch { } finally { p.Dispose(); }
        }
        List<IntPtr> list = new List<IntPtr>();
        foreach (IntPtr h in TopWindows()) {
            bool gx = ClassOf(h).StartsWith("GxWindowClass");   // older clients; Classic Era 2026 uses "waApplication Window" and is found by process name
            if (!gx && !wow.ContainsKey(Pid(h)) && !IsWowPath(Pid(h))) continue;
            RECT r; GetWindowRect(h, out r);
            if (gx || ((r.Right - r.Left) >= 300 && (r.Bottom - r.Top) >= 200)) list.Add(h);
        }
        return list;
    }
    // 1 = runs as administrator, 0 = normal, -1 = could not check (usually means: administrator)
    public static int Elevation(uint pid) {
        IntPtr hp = OpenProcess(0x1000, false, pid);
        if (hp == IntPtr.Zero) return -1;
        try {
            IntPtr tok;
            if (!OpenProcessToken(hp, 0x0008, out tok)) return -1;
            try { int e, len; if (!GetTokenInformation(tok, 20, out e, 4, out len)) return -1; return e != 0 ? 1 : 0; }
            finally { CloseHandle(tok); }
        } finally { CloseHandle(hp); }
    }
    // synchronous move for Diagnose: 0 = ok, otherwise the Windows error code (5 = access denied)
    public static int TryMove(IntPtr h, int x, int y, int w, int hh) {
        if (SetWindowPos(h, IntPtr.Zero, x, y, w, hh, 0x0004 | 0x0010 | 0x0020 | 0x0040)) return 0;
        int e = Marshal.GetLastWin32Error(); return e == 0 ? -1 : e;
    }
    public static bool IsAt(IntPtr h, int x, int y, int w, int hh) { RECT r; GetWindowRect(h, out r); return r.Left == x && r.Top == y && (r.Right - r.Left) == w && (r.Bottom - r.Top) == hh; }
    public static string Rect(IntPtr h) { RECT r; GetWindowRect(h, out r); return r.Left + "," + r.Top + " " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top); }
}
'@

# Work in real pixels even with Windows display scaling (125 %, 150 % ...)
try { if (-not [FmWin]::SetProcessDpiAwarenessContext([IntPtr](-4))) { [void][FmWin]::SetProcessDPIAware() } } catch { [void][FmWin]::SetProcessDPIAware() }   # per-monitor aware (Win10 1703+), else system aware

function Write-Step([string]$text, [string]$color = 'Gray') { Write-Host ('  ' + $text) -ForegroundColor $color }

function Get-GameWindows { return @([FmWin]::FindGameWindows()) }

$script:TargetScreen = $null
$script:Renamed = @{}
function Get-TargetScreen {
  if ($script:TargetScreen) { return $script:TargetScreen }
  $script:TargetScreen = Find-TargetScreen
  return $script:TargetScreen
}
function Find-TargetScreen {
  $all = [System.Windows.Forms.Screen]::AllScreens
  if ($Monitor -eq 'Primary') { return [System.Windows.Forms.Screen]::PrimaryScreen }
  $i = [int]$Monitor - 1
  if ($i -lt 0 -or $i -ge $all.Count) { Write-Step "Monitor $Monitor not found - using the primary screen." 'Yellow'; return [System.Windows.Forms.Screen]::PrimaryScreen }
  return $all[$i]
}

function Get-SlotRect([int]$index) {
  $scr = Get-TargetScreen
  $area = if ($UseTaskbarArea) { $scr.Bounds } else { $scr.WorkingArea }
  $s = $Slots[$index]
  $x = $area.X + [math]::Round($area.Width * $s.X / 100)
  $y = $area.Y + [math]::Round($area.Height * $s.Y / 100)
  $right = $area.X + [math]::Round($area.Width * ($s.X + $s.W) / 100)
  $bottom = $area.Y + [math]::Round($area.Height * ($s.Y + $s.H) / 100)
  $keepFull = $false
  if ($FillHeight) {
    # Right column exactly as wide as a 5:4 window of half the screen height, the main window gets the rest.
    $colW = [int][math]::Ceiling($MinAspect * [math]::Round($area.Height / 2))
    $split = $area.X + $area.Width - $colW
    if ($s.X -eq 0) { $right = $split; $keepFull = $true } else { $x = $split; $right = $area.X + $area.Width }
  }
  $w = $right - $x; $h = $bottom - $y
  if ($AvoidBlackBars -and -not $keepFull -and $h -gt 0 -and ($w / $h) -lt $MinAspect) {
    $fitH = [int][math]::Floor($w / $MinAspect)
    switch ($s.Anchor) {
      'Bottom' { $y = $bottom - $fitH }
      'Center' { $y = $y + [int](($h - $fitH) / 2) }
      default  { }
    }
    $h = $fitH
  }
  return @{ X = [int]$x; Y = [int]$y; W = [int]$w; H = [int]$h }
}

function Set-Borderless([IntPtr]$hwnd) {
  if (-not $Borderless) { return $false }
  $GWL_STYLE = -16
  $style = [FmWin]::GetWindowLong($hwnd, $GWL_STYLE)
  $remove = 0x00C00000 -bor 0x00040000 -bor 0x00020000 -bor 0x00010000 -bor 0x00080000   # caption, sizing frame, min, max, system menu
  $new = $style -band (-bnot $remove)
  if ($new -ne $style) { [void][FmWin]::SetWindowLong($hwnd, $GWL_STYLE, $new); return $true }
  return $false
}

function Set-GameWindow([IntPtr]$hwnd, [int]$index) {
  if (-not [FmWin]::IsWindow($hwnd)) { return }
  $r = Get-SlotRect $index
  if ([FmWin]::IsZoomed($hwnd) -or [FmWin]::IsIconic($hwnd)) { [void][FmWin]::ShowWindowAsync($hwnd, 4) }   # SW_SHOWNOACTIVATE: un-maximize / un-minimize
  $styleChanged = Set-Borderless $hwnd
  if (-not $styleChanged -and [FmWin]::IsAt($hwnd, $r.X, $r.Y, $r.W, $r.H)) { return }   # already in place: do nothing
  # SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS (never steals focus, never waits on a busy client)
  $flags = 0x0004 -bor 0x0010 -bor 0x0020 -bor 0x0040 -bor 0x4000
  [void][FmWin]::SetWindowPos($hwnd, [IntPtr]::Zero, $r.X, $r.Y, $r.W, $r.H, $flags)
}

# Nudge: 2 px smaller, then the real size again, so WoW gets a fresh resize event and redraws at the window's size.
function Invoke-ResizeNudge([IntPtr]$hwnd, [int]$index) {
  if (-not $ResizeNudge -or -not [FmWin]::IsWindow($hwnd)) { return }
  $r = Get-SlotRect $index
  $flags = 0x0004 -bor 0x0010 -bor 0x4000   # no z-order change, no activate, async
  [void][FmWin]::SetWindowPos($hwnd, [IntPtr]::Zero, $r.X, $r.Y, $r.W - 2, $r.H - 2, $flags)
  Start-Sleep -Milliseconds 250
  [void][FmWin]::SetWindowPos($hwnd, [IntPtr]::Zero, $r.X, $r.Y, $r.W, $r.H, $flags)
}

# Rename once the window is in place (renaming waits for the game, so not while it is still loading).
function Set-GameTitle([IntPtr]$hwnd, [int]$index) {
  $key = [string]$hwnd + ':' + $index
  if ($RenameWindows -and -not $script:Renamed.ContainsKey($key)) { [void][FmWin]::SetWindowText($hwnd, 'WoW - ' + $Slots[$index].Name); $script:Renamed[$key] = $true }
}

function Test-Placed([IntPtr]$hwnd, [int]$index) { $r = Get-SlotRect $index; return [FmWin]::IsAt($hwnd, $r.X, $r.Y, $r.W, $r.H) }

function Get-WindowsOldestFirst {
  $wins = Get-GameWindows
  $rows = foreach ($h in $wins) {
    $start = [datetime]::MaxValue
    try { $start = (Get-Process -Id ([FmWin]::Pid($h))).StartTime } catch { }
    [pscustomobject]@{ Handle = $h; Start = $start }
  }
  return @($rows | Sort-Object Start | ForEach-Object { $_.Handle })
}

function Find-BattleNet {
  if ($BattleNetExe -and (Test-Path $BattleNetExe)) { return $BattleNetExe }
  $candidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Battle.net\Battle.net.exe'),
    (Join-Path $env:ProgramFiles 'Battle.net\Battle.net.exe')
  )
  foreach ($key in 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net', 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net') {
    try { $loc = (Get-ItemProperty $key -ErrorAction Stop).InstallLocation; if ($loc) { $candidates += (Join-Path $loc 'Battle.net.exe') } } catch { }
  }
  foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
  return $null
}

function Show-Plan {
  $scr = Get-TargetScreen
  $area = if ($UseTaskbarArea) { $scr.Bounds } else { $scr.WorkingArea }
  Write-Step ("Screen: " + $scr.DeviceName + "  area " + $area.Width + "x" + $area.Height + " at " + $area.X + "," + $area.Y) 'Cyan'
  for ($i = 0; $i -lt $Slots.Count; $i++) {
    $r = Get-SlotRect $i
    Write-Step ((($i + 1).ToString()) + ". " + $Slots[$i].Name.PadRight(8) + " -> " + $r.X + "," + $r.Y + "  " + $r.W + "x" + $r.H)
  }
}

function Get-AccountName([int]$i) { if ($i -lt $Accounts.Count -and $Accounts[$i]) { return $Accounts[$i] }; return ('account ' + ($i + 1)) }

# Windows blocks a normal program from moving windows of a program that runs as administrator.
# Returns $true if this tool was restarted as administrator (the caller should then stop).
function Resolve-Blocked([IntPtr]$hwnd, [int]$index, [string]$before) {
  $now = [FmWin]::Rect($hwnd); $want = Get-SlotRect $index
  $elev = [FmWin]::Elevation([FmWin]::Pid($hwnd))
  Write-Step ($Slots[$index].Name + ' window did not move into place.') 'Yellow'
  Write-Step ('   wanted ' + $want.X + ',' + $want.Y + ' ' + $want.W + 'x' + $want.H + '   is ' + $now + '   was ' + $before + '   game elevated: ' + $elev + '   tool admin: ' + $isAdmin) 'DarkGray'
  if ($elev -ne 0 -and -not $isAdmin) {
    Write-Step 'WoW seems to run as administrator, so Windows does not let this tool move it.' 'Yellow'
    if ($AutoElevate) {
      Write-Step 'Restarting this tool as administrator - click Yes in the Windows prompt.' 'Yellow'
      try {
        Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -Verb RunAs -ArgumentList @('-NoExit', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'), '-Mode', $Mode, '-Game', $Game)
        return $true
      } catch { Write-Step 'Not restarted (the prompt was declined). Right-click the .cmd file and choose "Run as administrator".' 'Yellow' }
    } else { Write-Step 'Right-click the .cmd file and choose "Run as administrator".' 'Yellow' }
  } elseif ($now -ne $before) {
    Write-Step 'The window moved, but the game changed the size again. Check Display Mode = Windowed in WoW.' 'Yellow'
  } else {
    Write-Step 'Run Diagnose.cmd with the games open and send the result (diagnose.txt).' 'Yellow'
  }
  return $false
}

Write-Host ''
Write-Host ('  Multibox Launcher - ' + $GameLabel) -ForegroundColor Yellow
Write-Host '  -------------------------' -ForegroundColor DarkYellow

switch ($Mode) {
  'Info' {
    Show-Plan
    $open = Get-WindowsOldestFirst
    Write-Step ("WoW windows open right now: " + $open.Count)
    foreach ($h in $open) { Write-Step ("  window " + $h + "  " + [FmWin]::Rect($h)) }
  }

  'Diagnose' {
    Write-Step ('Time: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '   PowerShell ' + $PSVersionTable.PSVersion + '   ' + [Environment]::OSVersion.VersionString)
    Write-Step ('Tool runs as administrator: ' + $isAdmin + '   language mode: ' + $ExecutionContext.SessionState.LanguageMode)
    Write-Step ('Script: ' + $PSCommandPath)
    try { Write-Step ('DPI: ' + [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width + ' px wide primary screen as seen by the tool') } catch { }
    foreach ($s in [System.Windows.Forms.Screen]::AllScreens) {
      Write-Step ('Screen ' + $s.DeviceName + '  primary=' + $s.Primary + '  bounds=' + $s.Bounds.X + ',' + $s.Bounds.Y + ' ' + $s.Bounds.Width + 'x' + $s.Bounds.Height + '  work=' + $s.WorkingArea.X + ',' + $s.WorkingArea.Y + ' ' + $s.WorkingArea.Width + 'x' + $s.WorkingArea.Height) 'Cyan'
    }
    Show-Plan
    Write-Host ''
    Write-Step 'Processes (Battle.net / WoW):' 'White'
    foreach ($p in @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Battle.net*' -or $_.ProcessName -like 'Wow*' -or $_.ProcessName -like 'Agent*' })) {
      $path = ''; try { $path = $p.Path } catch { }
      Write-Step ('  ' + $p.ProcessName + '  pid ' + $p.Id + '  elevated ' + [FmWin]::Elevation([uint32]$p.Id) + '  ' + $path)
    }
    Write-Host ''
    Write-Step 'Windows of those processes (class / title / size):' 'White'
    foreach ($h in [FmWin]::TopWindows()) {
      $pn = [FmWin]::ProcName([FmWin]::Pid($h)); $cl = [FmWin]::ClassOf($h)
      if ($pn -like 'Wow*' -or $pn -like 'Battle.net*' -or $cl -like 'GxWindow*' -or [FmWin]::IsWowPath([FmWin]::Pid($h))) {
        Write-Step ('  ' + $pn + '  [' + [FmWin]::ImagePath([FmWin]::Pid($h)) + ']  class "' + $cl + '"  title "' + [FmWin]::TitleOf($h) + '"  ' + [FmWin]::Rect($h) + '  style 0x' + ('{0:X8}' -f [FmWin]::GetWindowLong($h, -16)))
      }
    }
    Write-Host ''
    $open = Get-WindowsOldestFirst
    Write-Step ('Game windows the tool recognises: ' + $open.Count) 'White'
    $n = [math]::Min($open.Count, $Slots.Count)
    for ($i = 0; $i -lt $n; $i++) {
      $h = $open[$i]; $before = [FmWin]::Rect($h); $r = Get-SlotRect $i
      [void](Set-Borderless $h)
      $err = [FmWin]::TryMove($h, $r.X, $r.Y, $r.W, $r.H)
      Start-Sleep -Milliseconds 700
      Write-Step ('  ' + $Slots[$i].Name + ': before ' + $before + '  wanted ' + $r.X + ',' + $r.Y + ' ' + $r.W + 'x' + $r.H + '  after ' + [FmWin]::Rect($h) + '  move result ' + $err + '  (0 = ok, 5 = access denied)')
    }
    Write-Host ''
    Write-Step ('Saved to ' + (Join-Path $here 'diagnose.txt')) 'Green'
  }

  'Shortcut' {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $lnkPath = Join-Path $desktop ($ShortcutName + '.lnk')
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut($lnkPath)
    $lnk.TargetPath = Join-Path $here $StartCmd          # the .cmd file keeps the window open if something goes wrong
    $lnk.WorkingDirectory = $here
    $bn = Find-BattleNet
    if ($bn) { $lnk.IconLocation = $bn + ',0' }
    $lnk.Description = 'Start Battle.net and arrange the World of Warcraft (' + $GameLabel + ') windows'
    $lnk.Save()
    Write-Step ("Shortcut created: " + $lnkPath) 'Green'
    Write-Step 'Tip: right-click it and choose "Pin to taskbar" or "Pin to Start".'
  }

  'Arrange' {
    $open = Get-WindowsOldestFirst
    if ($open.Count -eq 0) { Write-Step 'No World of Warcraft windows found. Start the games first (or run Diagnose.cmd).' 'Yellow'; try { Stop-Transcript | Out-Null } catch { }; exit 1 }
    $n = [math]::Min($open.Count, $Slots.Count)
    $before = @{}
    for ($i = 0; $i -lt $n; $i++) { $before[$i] = [FmWin]::Rect($open[$i]); Set-GameWindow $open[$i] $i; Write-Step ('Placing ' + $Slots[$i].Name) 'Green' }
    Start-Sleep -Milliseconds 1500
    for ($i = 0; $i -lt $n; $i++) { Set-GameWindow $open[$i] $i }   # second pass in case the game resized itself
    Start-Sleep -Milliseconds 500
    for ($i = 0; $i -lt $n; $i++) { Invoke-ResizeNudge $open[$i] $i }   # make WoW redraw at the new size (fixes cut-off action bars)
    Start-Sleep -Milliseconds 1000
    $bad = $false
    for ($i = 0; $i -lt $n; $i++) { if (Test-Placed $open[$i] $i) { Set-GameTitle $open[$i] $i } }
    for ($i = 0; $i -lt $n; $i++) {
      if (-not (Test-Placed $open[$i] $i)) {
        $bad = $true
        if (Resolve-Blocked $open[$i] $i $before[$i]) { try { Stop-Transcript | Out-Null } catch { }; exit 0 }
        break
      }
    }
    if ($open.Count -gt $Slots.Count) { Write-Step ('' + ($open.Count - $Slots.Count) + ' extra WoW window(s) left as they are.') 'Yellow' }
    if ($bad) { try { Stop-Transcript | Out-Null } catch { }; exit 1 }
    Write-Step 'Done.' 'Green'
  }

  'Start' {
    Show-Plan
    if ($StartBattleNet) {
      if (-not (Get-Process -Name 'Battle.net' -ErrorAction SilentlyContinue)) {
        $bn = Find-BattleNet
        if ($bn) { Write-Step 'Starting Battle.net ...'; Start-Process -FilePath $bn } else { Write-Step 'Battle.net.exe not found - start it yourself (or set $BattleNetExe).' 'Yellow' }
      } else { Write-Step 'Battle.net is already running.' }
    }
    Write-Host ''
    Write-Step ('In Battle.net: ' + $PickGame) 'White'
    Write-Step 'Then choose the account and press Play - in this order:' 'White'
    for ($i = 0; $i -lt $Slots.Count; $i++) { Write-Step ('   ' + ($i + 1) + '. ' + $Slots[$i].Name + '   (' + (Get-AccountName $i) + ')') 'White' }
    Write-Step 'Each game window snaps into place as soon as it appears. Leave this window open.' 'White'
    Write-Host ''

    $assigned = @{}          # handle -> slot index
    $placedAt = @{}          # handle -> time placed (re-applied for 30 s, because WoW resizes itself while loading)
    $before   = @{}          # handle -> rectangle before the first move
    $nudged   = @{}          # handle -> resize nudge done
    $misses   = @{}          # handle -> checks in a row where the window was not in place
    $reported = $false
    $deadline = (Get-Date).AddMinutes($WatchMinutes)
    $lastPlaced = Get-Date
    $firstRound = $true
    while ((Get-Date) -lt $deadline) {
      # forget windows that were closed, so a restarted client fills the free slot again
      foreach ($h in @($assigned.Keys)) { if (-not [FmWin]::IsWindow($h)) { Write-Step ($Slots[$assigned[$h]].Name + ' window closed - slot is free again.') 'Yellow'; $assigned.Remove($h); $placedAt.Remove($h) } }
      foreach ($h in @(Get-WindowsOldestFirst)) {
        if ($assigned.ContainsKey($h)) { continue }
        $free = @(0..($Slots.Count - 1) | Where-Object { $assigned.Values -notcontains $_ })
        if ($free.Count -eq 0) { continue }
        $assigned[$h] = $free[0]; $placedAt[$h] = Get-Date; $lastPlaced = Get-Date
        $before[$h] = [FmWin]::Rect($h); $misses[$h] = 0
        $what = if ($firstRound) { 'Found an open window -> ' } else { 'New WoW window -> ' }
        Write-Step ($what + $Slots[$free[0]].Name + '   (class ' + [FmWin]::ClassOf($h) + ', ' + [FmWin]::ProcName([FmWin]::Pid($h)) + '.exe)') 'Green'
      }
      $firstRound = $false
      foreach ($h in @($assigned.Keys)) {
        $age = ((Get-Date) - $placedAt[$h]).TotalSeconds
        if ($age -ge 30) {
          if (-not $nudged.ContainsKey($h)) { $nudged[$h] = $true; Invoke-ResizeNudge $h $assigned[$h] }   # once, after loading settled
          continue
        }
        $ok = Test-Placed $h $assigned[$h]          # checked before this pass's move: the last move had 1 s to land
        if ($ok) { Set-GameTitle $h $assigned[$h] }
        Set-GameWindow $h $assigned[$h]
        if ($age -ge 3 -and -not $reported) {
          if ($ok) { $misses[$h] = 0 } else { $misses[$h]++ }
          if ($misses[$h] -ge 8) {
            $reported = $true
            if (Resolve-Blocked $h $assigned[$h] $before[$h]) { try { Stop-Transcript | Out-Null } catch { }; exit 0 }
          }
        }
      }
      if ($assigned.Count -ge $Slots.Count -and ((Get-Date) - $lastPlaced).TotalSeconds -gt 30) { break }
      Start-Sleep -Seconds 1
    }
    Write-Host ''
    $allOk = $assigned.Count -ge $Slots.Count
    foreach ($h in @($assigned.Keys)) { if (-not (Test-Placed $h $assigned[$h])) { $allOk = $false; Write-Step ($Slots[$assigned[$h]].Name + ' is not in place: ' + [FmWin]::Rect($h)) 'Yellow' } }
    if ($allOk) { Write-Step 'All windows are in place. Have fun!' 'Green'; Start-Sleep -Seconds 4 }
    else {
      if ($assigned.Count -lt $Slots.Count) { Write-Step ('Stopped watching (' + $assigned.Count + ' of ' + $Slots.Count + ' windows found). If WoW is open, run Diagnose.cmd.') 'Yellow' }
      try { Stop-Transcript | Out-Null } catch { }
      exit 1
    }
  }
}
try { Stop-Transcript | Out-Null } catch { }
