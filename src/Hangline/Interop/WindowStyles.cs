namespace Hangline.Interop;

/// <summary>
/// Extended window styles, applied the way Win32 actually requires: changing
/// GWL_EXSTYLE on a live window does nothing until SetWindowPos is called with
/// SWP_FRAMECHANGED, which is why every setter here ends with a no-op move.
/// </summary>
public static class WindowStyles
{
    public static long Get(IntPtr hwnd) =>
        NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();

    public static void Set(IntPtr hwnd, long add, long remove = 0)
    {
        long current = Get(hwnd);
        long next = (current | add) & ~remove;
        if (next == current) return;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(next));
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
    }

    /// A strip that must never take focus from the app the user is typing into,
    /// never appear in Alt-Tab, and never be a window the user can accidentally
    /// activate by clicking through it.
    public static void MakeToolStrip(IntPtr hwnd) => Set(hwnd,
        NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_LAYERED);

    /// The Windows twin of macOS's <c>ignoresMouseEvents</c>: every click, hover
    /// and wheel event falls through to whatever is behind the window.
    public static void SetClickThrough(IntPtr hwnd, bool through) =>
        Set(hwnd, through ? NativeMethods.WS_EX_TRANSPARENT : 0,
            through ? 0 : NativeMethods.WS_EX_TRANSPARENT);
}
