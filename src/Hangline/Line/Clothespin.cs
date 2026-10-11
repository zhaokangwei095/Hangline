using System.Windows;
using System.Windows.Media;
using Hangline.Core;

namespace Hangline.Line;

/// <summary>
/// A minimal aluminium clip: a brushed metal pill with a slot where it grips
/// the line, and a soft shadow so it reads on any background.
/// </summary>
public static class Clothespin
{
    public const double Width = Layout.PinWidth;
    public const double Height = Layout.PinHeight;

    private static readonly Brush Metal = Frozen(new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromRgb(179, 179, 179), 0),
            new(Color.FromRgb(237, 237, 237), 0.35),
            new(Color.FromRgb(209, 209, 209), 0.65),
            new(Color.FromRgb(158, 158, 158), 1),
        },
        new Point(0, 0.5), new Point(1, 0.5)));

    private static readonly Brush EdgeBrush = Frozen(new LinearGradientBrush(
        Color.FromArgb(230, 255, 255, 255), Color.FromArgb(46, 0, 0, 0),
        new Point(0.5, 0), new Point(0.5, 1)));

    private static readonly Pen Edge = Frozen(new Pen(EdgeBrush, 0.6));
    private static readonly Brush Slot = Frozen(new SolidColorBrush(Color.FromArgb(82, 0, 0, 0)));
    private static readonly Brush Shadow = Frozen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)));

    private static Brush Frozen(Brush brush) { brush.Freeze(); return brush; }
    private static Pen Frozen(Pen pen) { pen.Freeze(); return pen; }

    /// <param name="top">Where the clip starts; it hangs down from the rope.</param>
    public static void Draw(DrawingContext dc, Point topCenter)
    {
        var r = new Rect(topCenter.X - Width / 2, topCenter.Y, Width, Height);
        double radius = Layout.PinRadius;

        dc.DrawRoundedRectangle(Shadow, null, new Rect(r.X - 0.6, r.Y + 1.5, r.Width + 1.2, r.Height + 1.2),
                                radius, radius);
        dc.DrawRoundedRectangle(Metal, Edge, r, radius, radius);

        // The slot the line passes through.
        var slot = new Rect(topCenter.X - 2.5, r.Y + 8.5, 5, 1.4);
        dc.DrawRoundedRectangle(Slot, null, slot, 0.7, 0.7);
    }
}
