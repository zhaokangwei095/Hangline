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

    /// Print Screen, with no modifier: taking this key over is what "handle
    /// screenshots" means on Windows, where there is no screenshot preference
    /// an app is allowed to rewrite.
    public static Shortcut PrintScreen { get; } = new(0x2C, HotKeyModifiers.None, "PrtSc");

    public static readonly Shortcut None = new(0, HotKeyModifiers.None, null);

    public bool IsEmpty => VirtualKey == 0;

    /// A bare letter or digit would swallow typing, so it needs a modifier.
    /// Keys that are not characters at all — Print Screen and the function row
    /// — are fair game alone, and Print Screen is the whole point of taking
    /// screenshots over.
    public bool IsValid =>
        VirtualKey != 0 && (Modifiers is not HotKeyModifiers.None || IsStandaloneKey);

    public bool IsStandaloneKey =>
        VirtualKey == 0x2C || (VirtualKey >= 0x70 && VirtualKey <= 0x87);

    /// The name a key shows as in a shortcut readout. Only what the recorder can
    /// actually produce, so a bound key never displays as a bare number.
    public static string LabelFor(int virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x7B => "F" + (virtualKey - 0x70 + 1),
        0x2C => "PrtSc",
        0x2D => "Insert",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PgUp",
        0x22 => "PgDn",
        0x09 => "Tab",
        0x20 => "Space",
        _ => virtualKey.ToString(),
    };

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
