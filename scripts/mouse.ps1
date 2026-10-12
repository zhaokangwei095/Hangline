<#
.SYNOPSIS
  Move the real cursor, click, or press a key combination, so the line's
  interactions can be checked without a hand on the mouse.

.DESCRIPTION
  This takes the mouse and keyboard away from whoever is using the machine.

  The process declares itself PerMonitorV2 first: a DPI-unaware caller has its
  coordinates virtualized by the scale factor, so a click aimed at a card lands
  somewhere else entirely and looks like a pass-through bug that is not one.

  Injected keyboard input can be swallowed by security software or a low-level
  hook on some machines. If a hotkey never fires from here while registration
  reported success, press the key by hand before concluding the feature is broken.
#>
param(
    [Parameter(Position = 0)] [int] $X = 0,
    [Parameter(Position = 1)] [int] $Y = 0,
    [switch] $Top,
    [switch] $Away,
    [switch] $Click,
    [string] $Chord = ''
)

$code = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Cursor
{
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out PointApi p);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint data, uint extra, uint data2, IntPtr extraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extraInfo);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint vk, uint mapType);

    [StructLayout(LayoutKind.Sequential)] public struct PointApi { public int X, Y; }

    public static void Aware()
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
    }

    public static void To(int x, int y) { SetCursorPos(x, y); }

    public static string Here()
    {
        PointApi p;
        GetCursorPos(out p);
        return p.X + "," + p.Y;
    }

    public static void Click()
    {
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);   // LEFTDOWN
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);   // LEFTUP
    }

    /// Press a comma-separated list of hex vk codes as a chord, then release in
    /// reverse order. Scan codes are filled in because hotkey matching can
    /// depend on them, and a bare keybd_event(vk, 0, ...) is not a fair test.
    public static string Chord(string csv)
    {
        var keys = new List<byte>();
        foreach (string part in csv.Split(','))
            keys.Add(Convert.ToByte(part.Trim(), 16));

        var report = new StringBuilder();
        foreach (byte vk in keys)
        {
            uint scan = MapVirtualKey(vk, 1);
            keybd_event(vk, (byte)scan, 0, IntPtr.Zero);
            report.Append("down " + vk.ToString("x2") + "/scan" + scan + " ");
        }

        keys.Reverse();
        foreach (byte vk in keys)
        {
            keybd_event(vk, (byte)MapVirtualKey(vk, 1), 2, IntPtr.Zero);
            report.Append("up " + vk.ToString("x2") + " ");
        }
        return report.ToString();
    }
}
'@

Add-Type -TypeDefinition $code
[Cursor]::Aware()

if ($Top)  { $X = 1280; $Y = 0 }
if ($Away) { $X = 1280; $Y = 1200 }

[Cursor]::To($X, $Y)
Start-Sleep -Milliseconds 120
if ($Click) { [Cursor]::Click() }
if ($Chord) { Write-Output ("sent: " + [Cursor]::Chord($Chord)) }
Write-Output ("cursor now at " + [Cursor]::Here())
