<# Guarded drag for testing the app's own window with the real mouse.
   Usage: tools\guarded-drag.ps1 -AppPid (Get-Process WoWBoxer.Layouts).Id -X1 .. -Y1 .. -X2 .. -Y2 .. [-StepMs 50 -Steps 30]
   Coordinates are physical screen pixels. #>
# Drags with the real mouse INSIDE our own app only. Before every step the window under the cursor must belong
# to the given process; otherwise it stops at once (so nothing can ever reach a WoW window).
param([int]$AppPid, [int]$X1, [int]$Y1, [int]$X2, [int]$Y2, [int]$StepMs = 20, [int]$Steps = 20)
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class M {
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
  public static uint PidAt(int x, int y) { var p = new POINT { X = x, Y = y }; var h = GetAncestor(WindowFromPoint(p), 2); uint pid; GetWindowThreadProcessId(h, out pid); return pid; }
}
'@
[void][M]::SetProcessDPIAware()
function Guard([int]$x, [int]$y) { $p = [M]::PidAt($x, $y); if ($p -ne $AppPid) { throw "ABORT: point $x,$y belongs to pid $p, not our app $AppPid" } }
Guard $X1 $Y1; Guard $X2 $Y2
[void][M]::SetCursorPos($X1, $Y1); Start-Sleep -Milliseconds 150
Guard $X1 $Y1; [M]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 300   # left down
$steps = $Steps
for ($i = 1; $i -le $steps; $i++) {
  $x = [int]($X1 + ($X2 - $X1) * $i / $steps); $y = [int]($Y1 + ($Y2 - $Y1) * $i / $steps)
  Guard $x $y; [void][M]::SetCursorPos($x, $y); Start-Sleep -Milliseconds $StepMs
}
Start-Sleep -Milliseconds 400; [M]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)   # left up
"dragged $X1,$Y1 -> $X2,$Y2"
