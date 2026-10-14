<#
.SYNOPSIS
  Every top-level window a process owns, with owner and taskbar-relevant styles.

.DESCRIPTION
  winrect.ps1 only looks at HwndWrapper[Hangline* classes, so it can miss the
  window that actually holds the taskbar button. This lists everything the
  process owns, which is what says whether a window is really on the taskbar.
#>
param([Parameter(Position = 0)] [int] $ProcessId = 0)

$code = @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class WndScan
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);

    public static List<string> Scan(int want)
    {
        var found = new List<string>();
        EnumWindows((h, l) => {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (want != 0 && pid != (uint)want) return true;
            if (want == 0) return true;

            var cls = new StringBuilder(256); GetClassName(h, cls, 256);
            var txt = new StringBuilder(256); GetWindowTextW(h, txt, 256);
            R r; GetWindowRect(h, out r);
            long ex = GetWindowLongPtr(h, -20).ToInt64();
            long style = GetWindowLongPtr(h, -16).ToInt64();
            IntPtr owner = GetWindow(h, 4);              // GW_OWNER
            IntPtr parent = GetWindow(h, 3);             // GW_ENABLEDPOPUP-ish

            // A window holds a taskbar button when it is a visible top-level
            // window with no owner and no WS_EX_TOOLWINDOW.
            bool topLevel = parent == IntPtr.Zero;
            bool taskbar = IsWindowVisible(h) && owner == IntPtr.Zero
                           && (ex & 0x80) == 0 && (ex & 0x40000) == 0;
            found.Add(string.Format(
                "hwnd=0x{0:x} vis={1} taskbar={2} owner=0x{3:x} rect={4},{5} {6}x{7} style=0x{8:x} ex=0x{9:x} class={10} text=\"{11}\"",
                h.ToInt64(), IsWindowVisible(h), taskbar, owner.ToInt64(),
                r.L, r.T, r.Rt - r.L, r.B - r.T, style, ex, cls, txt));
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@

Add-Type -TypeDefinition $code
$rows = [WndScan]::Scan($ProcessId)
if (-not $rows) { Write-Output "no windows for pid $ProcessId"; exit 0 }
foreach ($row in $rows) { Write-Output $row }
