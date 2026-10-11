using Hangline.Core;

namespace Hangline.Interop;

/// <summary>
/// A system-wide hotkey. Registration can fail because another app already has
/// it — the caller keeps the old one in that case rather than leaving the user
/// with a shortcut that silently does nothing.
/// </summary>
public sealed class HotKey : IDisposable
{
    private const int Id = 0x5445;
    private readonly MessageSink _sink;
    private Shortcut _current;

    public event Action? Pressed;

    public HotKey(MessageSink sink)
    {
        _sink = sink;
        _sink.On(NativeMethods.WM_HOTKEY, (_, _) => Pressed?.Invoke());
    }

    public Shortcut Current => _current;

    public bool TryRegister(Shortcut shortcut)
    {
        if (shortcut.IsEmpty) { Unregister(); return true; }
        if (!shortcut.IsValid) return false;

        uint modifiers = (uint)shortcut.Modifiers | NativeMethods.MOD_NOREPEAT;
        bool ok = NativeMethods.RegisterHotKey(_sink.Handle, Id, modifiers, (uint)shortcut.VirtualKey);
        if (!ok)
        {
            Shell.Log.Write($"hotkey {shortcut.Display} is taken");
            return false;
        }

        _current = shortcut;
        return true;
    }

    public void Unregister()
    {
        if (_current.IsEmpty) return;
        NativeMethods.UnregisterHotKey(_sink.Handle, Id);
        _current = Shortcut.None;
    }

    public void Dispose() => Unregister();
}
