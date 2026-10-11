using Hangline.Core;

namespace Hangline.Interop;

/// <summary>
/// Whether another app owns the screen right now, in which case the line should
/// not drop in front of it.
/// <para>
/// Windows has no Spaces, so this is two heuristics: the shell's notification
/// state (which reports exclusive fullscreen and presentation mode), and the
/// foreground window's real bounds covering the monitor.
/// </para>
/// </summary>
public static class FullscreenProbe
{
    public static bool CoversScreen(RectD monitorPhysical)
    {
        if (NativeMethods.SHQueryUserNotificationState(out NativeMethods.tagQUNS state) == 0 &&
            state is NativeMethods.tagQUNS.RunningD3dFullScreen or NativeMethods.tagQUNS.PresentationMode)
        {
            return true;
        }

        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        // Never let the line suppress itself.
        if (IsOurs(hwnd)) return false;

        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out NativeMethods.RECT bounds, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) != 0)
        {
            return false;
        }

        var rect = new RectD(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        const int tolerance = 2;
        return rect.X <= monitorPhysical.X + tolerance
               && rect.Y <= monitorPhysical.Y + tolerance
               && rect.Right >= monitorPhysical.Right - tolerance
               && rect.Bottom >= monitorPhysical.Bottom - tolerance;
    }

    private static bool IsOurs(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == Environment.ProcessId;
    }
}
