using Hangline.Capture;
using Hangline.Shell;

namespace Hangline.Watchers;

/// <summary>
/// Assembles the folders the line hangs from.
/// <para>
/// Hangline's own inbox is the one that accepts recordings and any image; the
/// folders Windows writes to are watched as a courtesy, so a screenshot taken
/// with the system tool still hangs.
/// </para>
/// </summary>
public sealed class WatchManager : IWatchers, IDisposable
{
    private readonly AppServices _app;
    private readonly List<FolderWatcher> _watchers = [];

    public WatchManager(AppServices app)
    {
        _app = app;
        Apply();
    }

    public void Apply()
    {
        foreach (FolderWatcher watcher in _watchers) watcher.Dispose();
        _watchers.Clear();

        Add(Inbox.Folder, anyImage: true, recordings: true);

        if (_app.Settings.WatchPicturesFolder)
        {
            Add(Special(Environment.SpecialFolder.MyPictures, "Screenshots"), anyImage: false, recordings: false);
            // Windows writes some captures straight into Pictures.
            Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)), anyImage: false, recordings: false);
        }

        if (_app.Settings.WatchCaptures)
            Add(Special(Environment.SpecialFolder.MyVideos, "Captures"), anyImage: false, recordings: true);

        if (_app.Settings.WatchDesktop)
            Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), anyImage: false, recordings: false);

        foreach (string extra in _app.Settings.ExtraFolders)
            Add(extra, anyImage: true, recordings: true);

        foreach (FolderWatcher watcher in _watchers) watcher.Start();
        Log.Write($"watching {_watchers.Count} folders");
    }

    public void RescanAll()
    {
        foreach (FolderWatcher watcher in _watchers) watcher.Rescan();
    }

    private void Add(string folder, bool anyImage, bool recordings)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
        if (_watchers.Any(w => w.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))) return;

        var watcher = new FolderWatcher(folder, anyImage, recordings);
        watcher.NewFile += path => _app.OnWatchedFileAppeared(path);
        watcher.Vanished += () => _app.OnWatchedFolderChanged();
        _watchers.Add(watcher);
    }

    private static string Special(Environment.SpecialFolder folder, string child) =>
        Path.Combine(Environment.GetFolderPath(folder), child);

    public void Dispose()
    {
        foreach (FolderWatcher watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
    }
}
