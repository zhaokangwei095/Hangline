using Hangline.Core;

namespace Hangline.Watchers;

/// <summary>
/// Notices files appearing in one folder.
/// <para>
/// It does not trust the event stream to say what happened — a screenshot is
/// written as a temporary file and renamed, and <c>FileSystemWatcher</c> can
/// also overflow its buffer and simply stop. So every event only schedules a
/// rescan 0.2 s later, and the rescan diffs the folder against what was there
/// before. That is also what makes a burst of twenty copied files behave: one
/// scan, then twenty hangs.
/// </para>
/// </summary>
public sealed class FolderWatcher : IDisposable
{
    public string Folder => _folder;

    private readonly string _folder;
    private readonly bool _anyImage;
    private readonly bool _acceptsRecordings;
    private readonly Debouncer _debounce;
    private readonly FileSystemWatcher _watcher;
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly DateTime _launched = DateTime.Now;
    private bool _primed;

    public event Action<string>? NewFile;
    public event Action? Vanished;

    public FolderWatcher(string folder, bool anyImage = false, bool acceptsRecordings = false)
    {
        _folder = folder;
        _anyImage = anyImage;
        _acceptsRecordings = acceptsRecordings;
        _debounce = new Debouncer(TimeSpan.FromMilliseconds(200), Scan);

        _watcher = new FileSystemWatcher(folder)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 32 * 1024,
        };
        _watcher.Created += (_, _) => _debounce.Trigger();
        _watcher.Changed += (_, _) => _debounce.Trigger();
        _watcher.Deleted += (_, _) => _debounce.Trigger();
        _watcher.Renamed += (_, _) => _debounce.Trigger();
        // An overflow means events were dropped; rescanning is the only cure.
        _watcher.Error += (_, _) => _debounce.Trigger();
    }

    public void Start()
    {
        Scan();
        try { _watcher.EnableRaisingEvents = true; }
        catch (Exception ex) { Shell.Log.Write($"watch start failed {_folder}: {ex.Message}"); }
    }

    private void Scan()
    {
        try
        {
            if (!System.IO.Directory.Exists(_folder)) return;

            var found = new List<(string Path, DateTime Created)>();
            foreach (string file in System.IO.Directory.EnumerateFiles(_folder))
            {
                string name = System.IO.Path.GetFileName(file);
                if (name.StartsWith('.') || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                if (!FileKind.IsCandidate(file, _anyImage, _acceptsRecordings)) continue;

                DateTime created;
                try { created = System.IO.File.GetCreationTime(file); }
                catch (IOException) { continue; }
                found.Add((file, created));
            }

            found.Sort((a, b) => a.Created.CompareTo(b.Created));

            if (!_primed)
            {
                // Whatever was already there is scenery, not a new capture —
                // except files made since Hangline started, which the watcher
                // may have missed while it was coming up.
                foreach ((string path, DateTime created) in found)
                {
                    _known.Add(path);
                    if (created >= _launched) NewFile?.Invoke(path);
                }
                _primed = true;
            }
            else
            {
                bool missing = false;
                foreach ((string path, _) in found)
                {
                    if (_known.Contains(path)) continue;
                    _known.Add(path);
                    NewFile?.Invoke(path);
                }
                foreach (string path in _known)
                {
                    if (!found.Exists(f => FileKind.SamePath(f.Path, path))) { missing = true; break; }
                }
                if (missing) Vanished?.Invoke();

                _known.Clear();
                foreach ((string path, _) in found) _known.Add(path);
            }
        }
        catch (Exception ex)
        {
            Shell.Log.Write($"scan failed {_folder}: {ex.Message}");
        }
    }

    public void Rescan() => _debounce.Trigger();

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
