using System.Windows;
using Hangline.Core;

namespace Hangline.Interop;

/// <summary>A monitor in physical pixels plus its scale.</summary>
public readonly record struct MonitorInfo(IntPtr Handle, RectD Bounds, RectD WorkArea, double Scale,
                                          string Device, bool IsPrimary)
{
    public double LogicalWidth => Bounds.Width / Scale;
    public double LogicalHeight => Bounds.Height / Scale;
}

/// <summary>
/// Every number that crosses between WPF's device-independent units and the
/// physical pixels SetWindowPos and GetCursorPos speak.
/// <para>
/// The rule this file exists to enforce: <b>the outside world is physical</b>.
/// Cursor positions, window placement and monitor bounds are all converted here
/// and nowhere else, because WPF's <c>Window.Left</c> is in the DIPs of whichever
/// monitor the window happens to be on, which is exactly the kind of moving
/// reference that leaves a one-pixel gap above a topmost strip.
/// </para>
/// </summary>
public static class Dpi
{
    public const double Base = 96;

    public static double ScaleOf(IntPtr hwnd)
    {
        int dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi <= 0 ? 1 : dpi / Base;
    }

    public static Pt CursorPhysical()
    {
        NativeMethods.GetCursorPos(out NativeMethods.POINT p);
        return new Pt(p.X, p.Y);
    }

    internal static RectD ToRect(NativeMethods.RECT r) =>
        new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    public static List<MonitorInfo> Monitors()
    {
        var list = new List<MonitorInfo>();
        bool ok = NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hmon, IntPtr _, IntPtr _, IntPtr _) =>
        {
            var info = new NativeMethods.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
            bool got = NativeMethods.GetMonitorInfo(hmon, ref info);
            if (got)
            {
                NativeMethods.GetDpiForMonitor(hmon, NativeMethods.DPI_EFFECTIVE, out uint dx, out _);
                list.Add(new MonitorInfo(hmon, ToRect(info.rcMonitor), ToRect(info.rcWork),
                                         dx == 0 ? 1 : dx / Base, info.szDevice,
                                         (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static MonitorInfo? MonitorAt(Pt physical)
    {
        var point = new NativeMethods.POINT { X = (int)physical.X, Y = (int)physical.Y };
        IntPtr handle = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (handle == IntPtr.Zero) return null;
        foreach (MonitorInfo m in Monitors())
        {
            if (m.Handle == handle) return m;
        }
        return null;
    }

    public static MonitorInfo? MonitorOfWindow(IntPtr hwnd)
    {
        IntPtr handle = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        foreach (MonitorInfo m in Monitors())
        {
            if (m.Handle == handle) return m;
        }
        return null;
    }
}
