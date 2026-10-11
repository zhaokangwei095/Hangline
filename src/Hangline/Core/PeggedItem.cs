namespace Hangline.Core;

/// <summary>
/// One screenshot hanging on the line. The files never move — the line is
/// only a view onto them.
/// </summary>
public sealed class PeggedItem
{
    public Guid Id { get; } = Guid.NewGuid();

    public required string Path { get; init; }

    /// Pixel size of the decoded thumbnail; the render layer needs it for the
    /// aspect-fit, the Core layer needs it for nothing.
    public SizeD NaturalSize { get; set; }

    /// Set by the render layer (a WPF BitmapSource). Kept opaque here so Core
    /// stays free of UI types.
    public object? Thumb { get; set; }

    /// Every photo hangs a little crooked, like on a real line.
    public double Tilt { get; init; } = Random.Shared.NextDouble() * 5 - 2.5;

    /// Being dropped off the line: still listed, but no longer counted,
    /// no longer hit-testable and not persisted.
    public bool IsFalling { get; set; }

    /// Still flying in from where it was captured; the card waits hidden.
    public bool IsFlying { get; set; }

    /// Where it was captured, in screen-logical coordinates, when known.
    public RectD? CaptureRect { get; set; }

    public bool IsRecording => FileKind.IsRecording(Path);
}
