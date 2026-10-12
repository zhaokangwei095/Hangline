using System.Windows.Interop;

namespace Hangline.Interop;

/// <summary>
/// A message-only window, so the app can listen to Win32 messages (clipboard
/// updates, hotkeys) without owning a visible window for them.
/// </summary>
public sealed class MessageSink : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<uint, List<Action<IntPtr, IntPtr>>> _handlers = new();

    public IntPtr Handle { get; }

    public MessageSink()
    {
        // A zero-sized off-screen window rather than a true message-only one:
        // HwndSourceParameters exposes no parent, and RegisterHotKey and the
        // clipboard listener only need *a* window to post to.
        _source = new HwndSource(new HwndSourceParameters("Hangline.MessageSink", 0, 0)
        {
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = unchecked((int)0x94000000), // WS_POPUP | WS_VISIBLE | WS_DISABLED
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
