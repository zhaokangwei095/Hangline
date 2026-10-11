using System.Windows;
using Hangline.Core;
using Hangline.Interop;

namespace Hangline.Input;

/// <summary>
/// The Windows twin of macOS's <c>ignoresMouseEvents</c>: the line's window
/// lets every click fall through to whatever is behind it, except where a photo
/// actually hangs.
/// <para>
/// The style is only flipped when the answer changes, because each flip costs a
/// SetWindowPos and a window that flickers on a 30 Hz timer is worse than a
/// slightly late hover.
/// </para>
/// </summary>
public sealed class Passthrough(IntPtr hwnd)
{
    private bool? _through;

    public bool IsThrough => _through ?? true;

    /// Cards lose four points of hit area on each side, so the edge of a photo
    /// does not steal a click meant for the window behind it.
    public const double Shrink = 4;

    public void ForceThrough() => Apply(new Dictionary<Guid, Rect>(), Pt.Zero, 1, Pt.Zero, forced: true);

    public bool Apply(IReadOnlyDictionary<Guid, Rect> cardRects, Pt cursorPhysical, double scale,
                      Pt originPhysical, bool forced = false)
    {
        bool over = false;
        if (!forced && scale > 0)
        {
            var local = new Pt((cursorPhysical.X - originPhysical.X) / scale,
                               (cursorPhysical.Y - originPhysical.Y) / scale);
            foreach (Rect rect in cardRects.Values)
            {
                var shrunk = new Rect(rect.X + Shrink, rect.Y + Shrink,
                                      Math.Max(0, rect.Width - 2 * Shrink),
                                      Math.Max(0, rect.Height - 2 * Shrink));
                if (local.X >= shrunk.X && local.X < shrunk.Right &&
                    local.Y >= shrunk.Y && local.Y < shrunk.Bottom) { over = true; break; }
            }
        }

        bool want = !over;
        if (_through == want) return false;
        _through = want;
        WindowStyles.SetClickThrough(hwnd, want);
        return true;
    }
}
