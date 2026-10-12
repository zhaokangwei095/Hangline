using Hangline.Core;

namespace Hangline.Line;

/// <summary>
/// The line itself: what hangs on it and what may be done to each item. The
/// files never move — the line is only a view onto them. Anything that touches
/// the disk or the shell arrives through an injected delegate, so the rules can
/// be tested without a screen.
/// </summary>
public sealed class LineModel
{
    private readonly List<PeggedItem> _items = [];

    public IReadOnlyList<PeggedItem> Items => _items;

    public LineSize Size { get; set; } = LineSize.Medium;
    public int? KeepOnLine { get; set; }

    /// How many photos fit across the screen.
    public int Visible { get; set; } = Layout.DefaultMaxItems;

    /// What the line holds, which may be more than fits when Keep on line is set.
    public int MaxItems { get; set; } = Layout.DefaultMaxItems;

    /// How far the line is scrolled towards older photos; 0 shows the newest.
    public double Scroll { get; set; }

    public bool Revealed { get; set; }
    public bool SoundOn { get; set; } = true;
    public bool QuietHang { get; set; }

    public Guid? CopiedId { get; set; }
    public Guid? DraggingId { get; set; }
    public Guid? PressedId { get; set; }

    /// Structure changed: cards were added, removed or moved.
    public event Action? Changed;

    /// A card is about to fall; the overlay draws it across the whole screen.
    public event Action<PeggedItem>? Falling;

    public Action<string, double>? PlaySound { get; set; }

    /// Run something some seconds later, on the UI thread.
    public Action<double, Action>? Delay { get; set; }

    public Func<string, bool> Exists { get; set; } = System.IO.File.Exists;
    public Func<string, bool> InInbox { get; set; } = _ => false;

    /// Returns false if the file could not be moved to the Recycle Bin.
    public Func<string, bool> Recycle { get; set; } = _ => false;

    public int LiveCount => LineRules.LiveCount(_items);

    public PeggedItem? Find(Guid id) => _items.FirstOrDefault(i => i.Id == id);

    public PeggedItem? FindByPath(string path) => _items.FirstOrDefault(i => FileKind.SamePath(i.Path, path));

    public bool IsInInbox(Guid id)
    {
        PeggedItem? item = Find(id);
        return item is not null && InInbox(item.Path);
    }

    /// <summary>Put a file on the line. Returns null if it was already there or
    /// its pixels could not be decoded.</summary>
    /// <param name="captured">Where it was captured on screen. A capture that
    /// knows its origin flies in from there; anything else simply appears.</param>
    public PeggedItem? Hang(string path, bool flying = false, bool quietly = false, RectD? captured = null)
    {
        if (LineRules.AlreadyHung(_items, path)) return null;

        var item = new PeggedItem
        {
            Path = path,
            IsFlying = flying && captured is not null,
            CaptureRect = captured,
        };
        if (!Read(item)) return null;

        _items.Add(item);
        Scroll = 0;

        // A full line lets the oldest photo fall off the far end. Only one: a
        // line hung on a wider screen keeps its length here instead of losing
        // several photos to a single capture.
        if (LineRules.IsFull(_items, MaxItems) && LineRules.OldestLive(_items) is { } oldest)
        {
            LetGo(oldest.Id);
        }

        if (!quietly) PlaySound?.Invoke("Tink", 0.35);
        Changed?.Invoke();
        return item;
    }

    /// The capture has reached the line: the real card takes over.
    public void Land(Guid id)
    {
        if (Find(id) is { } item) item.IsFlying = false;
        Changed?.Invoke();
    }

    /// Take a card off the line, leaving its file alone.
    public void Drop(Guid id, bool quietly = false)
    {
        if (Find(id) is not { IsFalling: false } item) return;

        Falling?.Invoke(item);
        item.IsFalling = true;
        Changed?.Invoke();
        if (!quietly) PlaySound?.Invoke("Pop", 0.25);

        // The card is drawn by the fall for a moment, then it is gone.
        Delay?.Invoke(0.6, () =>
        {
            _items.RemoveAll(i => i.Id == id);
            Changed?.Invoke();
        });
    }

    /// The oldest photo falling off a full line. Like the cross, a file in
    /// Hangline's folder goes to the Recycle Bin, or nothing would ever take it
    /// out of that folder. Elsewhere it only leaves the line.
    public void LetGo(Guid id)
    {
        string? path = PathOf(id);
        if (IsInInbox(id) && path is not null && Recycle(path))
        {
            Drop(id, quietly: true);
            return;
        }
        Drop(id, quietly: true);
    }

    /// The corner cross and "Take down" both end up here.
    public void Discard(Guid id, bool quietly = false)
    {
        if (!IsInInbox(id))
        {
            Drop(id, quietly);
            return;
        }
        string? path = PathOf(id);
        if (path is not null && Recycle(path))
        {
            if (SoundOn && !quietly) PlaySound?.Invoke("Trash", 1);
            Drop(id, quietly: true);
            return;
        }
        Drop(id, quietly);
    }

    /// One after another, and only the first makes a sound.
    public void TakeDown(IReadOnlyList<PeggedItem> live)
    {
        for (int n = 0; n < live.Count; n++)
        {
            PeggedItem item = live[n];
            bool quiet = n > 0;
            double at = 0.06 * n;
            Delay?.Invoke(at, () => Discard(item.Id, quietly: quiet));
        }
    }

    public void TakeEverythingDown() => TakeDown(_items.Where(i => !i.IsFalling).ToList());

    public void TakeDownNeighbours(Guid id, bool toTheLeft) =>
        TakeDown(LineRules.Neighbours(_items, id, toTheLeft));

    /// Photos whose file was deleted or moved away fall off by themselves.
    public void Prune()
    {
        foreach (PeggedItem item in LineRules.MissingFiles(_items, Exists))
        {
            Drop(item.Id, quietly: true);
        }
    }

    /// After Keep on line is lowered, the oldest photos past it go the way of a
    /// full line.
    public void TrimOverCapacity()
    {
        foreach (PeggedItem item in LineRules.OverCapacity(_items, MaxItems)) LetGo(item.Id);
        Scroll = 0;
        Changed?.Invoke();
    }

    public void ScrollBy(double delta)
    {
        double next = LineRules.ClampScroll(Scroll + delta, _items.Count, Visible, Layout.Spacing(Size));
        if (Math.Abs(next - Scroll) < 0.01) return;
        Scroll = next;
        Changed?.Invoke();
    }

    /// Everything comes back as it was. This runs before the line knows its
    /// screen, so capacity must not be applied: photos would fall off a wide
    /// screen's line at every launch.
    public void Restore(IEnumerable<string> paths)
    {
        foreach (string path in LineRules.RestorablePaths(paths.ToList(), Exists))
        {
            var item = new PeggedItem { Path = path };
            if (Read(item)) _items.Add(item);
        }
        Changed?.Invoke();
    }

    public IReadOnlyList<string> PathsToSave() => LineRules.PathsToSave(_items);

    /// After a change of size, every photo is redrawn sharp at the new one.
    public void ReloadThumbnails()
    {
        foreach (PeggedItem item in _items)
        {
            if (item.IsFalling) continue;
            Read(item);
        }
        Changed?.Invoke();
    }

    /// After editing, the photo on the line shows the new version, in place:
    /// the card keeps its id and does not re-hang.
    public bool ReloadThumbnailFor(string path)
    {
        PeggedItem? item = _items.FirstOrDefault(i => FileKind.SamePath(i.Path, path) && !i.IsFalling);
        if (item is null) return false;
        if (!Read(item)) return false;
        Changed?.Invoke();
        return true;
    }

    public void ClearCopiedFlag()
    {
        Guid? id = CopiedId;
        if (id is null) return;
        Delay?.Invoke(1.2, () =>
        {
            if (CopiedId == id) { CopiedId = null; Changed?.Invoke(); }
        });
    }

    public IReadOnlyList<PeggedItem> Live() => _items.Where(i => !i.IsFalling).ToList();

    public bool NeighboursLeft(Guid id) => LineRules.Neighbours(_items, id, toTheLeft: true).Count > 0;

    public bool NeighboursRight(Guid id) => LineRules.Neighbours(_items, id, toTheLeft: false).Count > 0;

    private string? PathOf(Guid id) => Find(id)?.Path;

    /// Decode a card's image and take its size from the same object, so layout
    /// and pixels cannot disagree.
    private bool Read(PeggedItem item)
    {
        ThumbLoader.Decoded? decoded = ThumbLoader.Load(item.Path, Size);
        if (decoded is null) return false;
        item.Thumb = decoded.Value.Image;
        item.NaturalSize = decoded.Value.Size;
        return true;
    }
}
