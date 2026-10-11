using Hangline.Core;
using Hangline.Interop;
using Hangline.Shell;

namespace Hangline.Capture;

/// <summary>
/// Hangline's own screenshots. This is what makes "拍下的瞬间即挂上绳" true on
/// Windows: the file is written straight into the inbox, so it never appears on
/// the desktop and there is no floating thumbnail to wait out.
/// </summary>
public static class Capturer
{
    public readonly record struct Result(string Path, RectD Captured);

    public static Result? Fullscreen()
    {
        RectD all = Gdi.VirtualScreen();
        return Save(Gdi.Capture(all), all);
    }

    public static Result? Region(RectD physical) => Save(Gdi.Capture(physical), physical);

    /// The front window's visible bounds, not its drag-resize border, which
    /// <c>GetWindowRect</c> includes and which would put a frame of desktop in
    /// every window capture.
    public static Result? ActiveWindow()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out NativeMethods.RECT bounds, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) != 0)
        {
            if (!NativeMethods.GetWindowRect(hwnd, out bounds)) return null;
        }

        var rect = new RectD(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        return Save(Gdi.Capture(rect), rect);
    }

    private static Result? Save(byte[]? png, RectD captured)
    {
        if (png is null || png.Length == 0) return null;

        Inbox.Ensure();
        NameStyle style = NameStyle.For(Localization.IsChinese);
        string path = Naming.UniquePath(Inbox.Folder, Naming.ScreenshotName(DateTime.Now, style));

        if (!FileOps.WriteAtomic(path, png)) return null;
        return new Result(path, captured);
    }
}
