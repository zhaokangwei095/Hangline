using Hangline.Core;

namespace Hangline.Interop;

/// <summary>
/// All global hotkeys, in one place.
/// <para>
/// It has to be one place: <c>RegisterHotKey</c> identifies a hotkey by a number
/// that must be unique per window, and <c>WM_HOTKEY</c> says only which number
/// fired. Give two bindings the same number and the second silently fails; let
/// every listener react to every message and one keypress triggers everything.
/// Both of those happened when each caller owned its own registration.
/// </para>
/// </summary>
public sealed class HotKeyManager : IDisposable
{
    private readonly MessageSink _sink;
    private readonly Dictionary<int, Action> _handlers = [];
    private readonly Dictionary<string, int> _ids = [];
    private readonly Dictionary<string, Shortcut> _current = [];
    private int _next = 1;

    public HotKeyManager(MessageSink sink)
    {
        _sink = sink;
        _sink.On(NativeMethods.WM_HOTKEY, (wParam, _) => Dispatch(wParam));
    }

    /// <summary>Bind a named shortcut. False means Windows refused — the keys are
    /// already someone else's — and any previous binding under this name is kept.</summary>
    public bool TryBind(string name, Shortcut shortcut, Action handler)
    {
        if (shortcut.IsEmpty) { Unbind(name); return true; }
        if (!shortcut.IsValid) return false;

        int id = _next++;
        uint modifiers = (uint)shortcut.Modifiers | NativeMethods.MOD_NOREPEAT;
        if (!NativeMethods.RegisterHotKey(_sink.Handle, id, modifiers, (uint)shortcut.VirtualKey))
        {
            Shell.Log.Write($"hotkey {shortcut.Display} is taken");
            return false;
        }

        Unbind(name);
        _handlers[id] = handler;
        _ids[name] = id;
        _current[name] = shortcut;
        Shell.Log.Write($"hotkey {shortcut.Display} -> id {id} on hwnd 0x{_sink.Handle.ToInt64():x}");
        return true;
    }

    public void Unbind(string name)
    {
        if (!_ids.TryGetValue(name, out int id)) return;
        NativeMethods.UnregisterHotKey(_sink.Handle, id);
        _ids.Remove(name);
        _handlers.Remove(id);
        _current.Remove(name);
    }

    public Shortcut Current(string name) => _current.TryGetValue(name, out var s) ? s : Shortcut.None;

    private void Dispatch(IntPtr wParam)
    {
        // One message, one handler: the id in wParam is the only thing that says
        // which shortcut the user actually pressed.
        int id = wParam.ToInt32();
        if (!_handlers.TryGetValue(id, out Action? handler))
        {
            Shell.Log.Write($"hotkey id={id} has no handler");
            return;
        }
        Shell.Log.Write($"hotkey id={id} fired");
        handler();
    }

    public void Dispose()
    {
        foreach (string name in _ids.Keys.ToList()) Unbind(name);
        _handlers.Clear();
    }
}
