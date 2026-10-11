namespace Hangline.Shell;

/// <summary>
/// What the tray and the gestures need from the line, so this layer can be
/// built and tested before the rope exists.
/// </summary>
public interface ILineHost
{
    bool Revealed { get; }
    int LiveCount { get; }
    void Toggle();
    void TakeEverythingDown();

    /// Called after a menu change that moves or redraws cards.
    void ApplyLayoutChanges();
}
