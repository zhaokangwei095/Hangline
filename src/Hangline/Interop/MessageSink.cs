using System.Windows.Interop;

namespace Hangline.Interop;

/// <summary>
/// A message-only window, so the app can listen to Win32 messages (clipboard
/// updates, hotkeys) without owning a visible window for them.
/// </summary>
public sealed class MessageSink : IDisposable
{
    private const int WM_CLOSE = 0x0010;

    private readonly HwndSource _source;
    private readonly Dictionary<uint, List<Action<IntPtr, IntPtr>>> _handlers = new();

    public IntPtr Handle { get; }

    public MessageSink()
    {
        // A zero-sized off-screen window rather than a true message-only one:
        // HwndSourceParameters exposes no parent, and RegisterHotKey and the
        // clipboard listener only need *a* window to post to.
        //
        // It must not be a message-only (HWND_MESSAGE) window, because this is
        // the sink the second-instance toggle arrives on: a broadcast reaches
        // top-level windows and skips message-only ones entirely.
        //
        // WS_EX_TOOLWINDOW keeps it off the taskbar. Without it Explorer gives
        // this window a button, and closing that button destroys it — which
        // takes the clipboard listener and every registered hotkey with it,
        // because both are bound to this handle. See WndProc: WM_CLOSE is
        // swallowed for the same reason.
        _source = new HwndSource(new HwndSourceParameters("Hangline.MessageSink", 0, 0)
        {
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = unchecked((int)0x94000000), // WS_POPUP | WS_VISIBLE | WS_DISABLED
            // Only WS_EX_TOOLWINDOW: it takes the window off the taskbar, which
            // is the whole point. WS_EX_NOACTIVATE was tried here too and
            // stopped WM_HOTKEY from arriving at all, so it is left out.
            ExtendedWindowStyle = unchecked((int)NativeMethods.WS_EX_TOOLWINDOW),
        });
        Handle = _source.Handle;
        _source.AddHook(WndProc);
    }

    public void On(uint message, Action<IntPtr, IntPtr> handler)
    {
        if (!_handlers.TryGetValue(message, out List<Action<IntPtr, IntPtr>>? list))
            _handlers[message] = list = [];
        list.Add(handler);
    }

    private static readonly bool Trace = Environment.GetEnvironmentVariable("HANGLINE_MSGS") == "1";

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (Trace) Shell.Log.Write($"msg 0x{msg:x4} wParam=0x{wParam.ToInt64():x} hwnd=0x{hwnd.ToInt64():x}");

        // Closing this window takes the clipboard listener and every hotkey
        // with it, so a close is refused rather than obeyed. The style above
        // should keep it off the taskbar, but a stray Alt+F4 or a shell that
        // offers a close for it would otherwise kill the feature silently.
        if (msg == WM_CLOSE)
        {
            Shell.Log.Write("message sink was asked to close; refusing");
            handled = true;
            return IntPtr.Zero;
        }

        if (_handlers.TryGetValue((uint)msg, out List<Action<IntPtr, IntPtr>>? list))
        {
            foreach (Action<IntPtr, IntPtr> handler in list)
            {
                try { handler(wParam, lParam); }
                catch (Exception ex) { Shell.Log.Write($"message {msg:x} handler: {ex.Message}"); }
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _handlers.Clear();
        _source.Dispose();
    }
}
