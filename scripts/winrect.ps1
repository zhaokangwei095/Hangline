<#
.SYNOPSIS
  Print the line window's visibility and rectangle, so reveal and retract can be
  checked without reading a screenshot.

.EXAMPLE
  powershell -File scripts/winrect.ps1
#>
$code = @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public static class WinProbe {
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);

    public static List<string> Scan() {
        var found = new List<string>();
        EnumWindows((h, l) => {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            string cn = sb.ToString();
            if (cn.IndexOf("HwndWrapper[Hangline", StringComparison.Ordinal) < 0) return true;
            R r; GetWindowRect(h, out r);
            uint pid; GetWindowThreadProcessId(h, out pid);
            found.Add(string.Format("pid={0} vis={1} rect={2},{3} {4}x{5} ex=0x{6:x8}",
                pid, IsWindowVisible(h), r.L, r.T, r.Rt - r.L, r.B - r.T,
                GetWindowLong(h, -20) & 0xFFFFFFFFu));
            var cls = new StringBuilder(256); GetClassName(h, cls, 256);
            var txt = new StringBuilder(256); GetWindowTextW(h, txt, 256);
            found.Add(string.Format("    class={0} text=\"{1}\"", cls, txt));
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@

Add-Type -TypeDefinition $code

# A DPI-unaware observer gets virtualized coordinates back from GetWindowRect,
# which would make a correctly placed 2560-wide strip look like 1707.
$aware = @'
using System;using System.Runtime.InteropServices;
public static class Aware {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    public static void Now() { try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } }
}
'@
Add-Type -TypeDefinition $aware
[Aware]::Now()

$rows = [WinProbe]::Scan()
if (-not $rows) { Write-Output 'no Hangline window'; exit 1 }
$rows | ForEach-Object { Write-Output $_ }
