namespace Hangline.Core;

[Flags]
public enum HotKeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
}

/// <summary>
/// A global shortcut. Kept as plain integers so it can be stored, compared and
/// tested without a window manager in the room.
/// </summary>
public readonly record struct Shortcut(int VirtualKey, HotKeyModifiers Modifiers, string? Label)
{
    /// Ctrl+Alt+T, the original's default.
    public static Shortcut Standard { get; } = new(0x54 /* 'T' */, HotKeyModifiers.Control | HotKeyModifiers.Alt, "T");

    public static readonly Shortcut None = new(0, HotKeyModifiers.None, null);

    public bool IsEmpty => VirtualKey == 0;

    /// A shortcut with no modifier would swallow a key the user is typing.
    public bool IsValid =>
        VirtualKey != 0 && Modifiers is not HotKeyModifiers.None;

    public string Display
    {
        get
        {
            if (IsEmpty) return "—";
            List<string> parts = [];
            if (Modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
            parts.Add(Label ?? VirtualKey.ToString());
            return string.Join("+", parts);
        }
    }
}
