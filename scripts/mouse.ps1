<#
.SYNOPSIS
  Move the real cursor, so the reveal band can be tested without a hand on the mouse.

.DESCRIPTION
  This takes the mouse away from whoever is using the machine. It exists for the
  automated check that resting the pointer at the top of the screen drops the
  line and leaving it picks the line back up.
#>
param(
    [Parameter(Position = 0)] [int] $X = 0,
    [Parameter(Position = 1)] [int] $Y = 0,
    [switch] $Top,
    [switch] $Away,
    [switch] $Click
)

$code = @'
using System;using System.Runtime.InteropServices;
public static class Cursor {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint d, uint e, uint ex, IntPtr extra);
    public static void Click() {
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);  // LEFTDOWN
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);  // LEFTUP
    }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out P p);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    public static void Aware() { try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } }
    [StructLayout(LayoutKind.Sequential)] public struct P { public int X, Y; }
    public static void To(int x, int y) { SetCursorPos(x, y); }
    public static string Here() { P p; GetCursorPos(out p); return p.X + "," + p.Y; }
}
'@
Add-Type -TypeDefinition $code

if ($Top) { $X = 1280; $Y = 0 }
if ($Away) { $X = 1280; $Y = 1200 }

# Without this, the coordinates below are virtualized by the scale factor and
# the click lands somewhere else entirely.
[Cursor]::Aware()
[Cursor]::To($X, $Y)
Start-Sleep -Milliseconds 120
if ($Click) { [Cursor]::Click() }
Write-Output ("cursor now at " + [Cursor]::Here() + $(if ($Click) { " (clicked)" } else { "" }))
