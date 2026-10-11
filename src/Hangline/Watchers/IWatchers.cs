namespace Hangline.Watchers;

/// <summary>
/// The things that notice a new screenshot. Declared as a seam so the tray menu
/// and the settings can be built before the watchers exist, without a stub that
/// pretends to work.
/// </summary>
public interface IWatchers
{
    /// Re-read the settings and start/stop watching accordingly.
    void Apply();

    /// A folder the user added may already be full of screenshots.
    void RescanAll();
}
