namespace Hangline.Core;

/// <summary>
/// The rules of the line, with no IO and no UI: what is already hanging, what
/// falls off a full line, which files vanished, how far the line may scroll.
/// Callers apply the effects (animations, trash, sounds), which keeps all of
/// this unit-testable.
/// </summary>
public static class LineRules
{
    public static bool AlreadyHung(IReadOnlyList<PeggedItem> items, string path) =>
        items.Any(i => FileKind.SamePath(i.Path, path) && !i.IsFalling);

    public static int LiveCount(IReadOnlyList<PeggedItem> items)
    {
        int n = 0;
        foreach (PeggedItem i in items) if (!i.IsFalling) n++;
        return n;
    }

    public static PeggedItem? OldestLive(IReadOnlyList<PeggedItem> items)
    {
        foreach (PeggedItem i in items) if (!i.IsFalling) return i;
        return null;
    }

    /// A full line lets exactly one photo fall off the far end per capture:
    /// a line hung on a wider screen keeps its length instead of losing
    /// several photos to a single capture.
    public static bool IsFull(IReadOnlyList<PeggedItem> items, int maxItems) =>
        LiveCount(items) > maxItems;

    /// After Keep on line is lowered, the oldest photos past it go the way of a full line.
    public static IReadOnlyList<PeggedItem> OverCapacity(IReadOnlyList<PeggedItem> items, int maxItems)
    {
        var list = new List<PeggedItem>();
        int live = LiveCount(items);
        foreach (PeggedItem i in items)
        {
            if (i.IsFalling) continue;
            if (live <= maxItems) break;
            list.Add(i);
            live--;
        }
        return list;
    }

    /// Photos whose file was deleted or moved away fall off by themselves.
    public static IReadOnlyList<PeggedItem> MissingFiles(IReadOnlyList<PeggedItem> items, Func<string, bool> exists)
    {
        var list = new List<PeggedItem>();
        foreach (PeggedItem i in items)
        {
            if (i.IsFalling) continue;
            if (!exists(i.Path)) list.Add(i);
        }
        return list;
    }

    /// The photos hung before (left) or after (right) this one.
    public static IReadOnlyList<PeggedItem> Neighbours(IReadOnlyList<PeggedItem> items, Guid id, bool toTheLeft)
    {
        var live = new List<PeggedItem>();
        foreach (PeggedItem i in items) if (!i.IsFalling) live.Add(i);
        int at = live.FindIndex(i => i.Id == id);
        if (at < 0) return live;
        return toTheLeft ? live.GetRange(0, at) : live.GetRange(at + 1, live.Count - at - 1);
    }

    /// How far the line may scroll towards older photos; 0 shows the newest.
    public static double ClampScroll(double scroll, int count, int visible, double spacing)
    {
        double most = Math.Max(0, count - visible) * spacing;
        return Math.Min(most, Math.Max(0, scroll));
    }

    public static bool ShouldScroll(IReadOnlyList<PeggedItem> items, int visible) =>
        items.Count(i => !i.IsFalling) > visible;

    /// Everything comes back as it was. This runs before the line knows its
    /// screen, so capacity must not be applied here — otherwise photos fall
    /// off a wide screen's line at every launch.
    public static IReadOnlyList<string> RestorablePaths(IReadOnlyList<string> stored, Func<string, bool> exists)
    {
        var kept = new List<string>();
        foreach (string path in stored)
        {
            if (!exists(path)) continue;
            bool dupe = false;
            foreach (string seen in kept)
            {
                if (FileKind.SamePath(seen, path)) { dupe = true; break; }
            }
            if (!dupe) kept.Add(path);
        }
        return kept;
    }

    /// What survives a save: falling photos are already gone from the line.
    public static IReadOnlyList<string> PathsToSave(IReadOnlyList<PeggedItem> items)
    {
        var list = new List<string>();
        foreach (PeggedItem i in items) if (!i.IsFalling) list.Add(i.Path);
        return list;
    }
}
