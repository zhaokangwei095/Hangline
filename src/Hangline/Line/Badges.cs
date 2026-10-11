using Hangline.Core;
using System.Windows;
using System.Windows.Media;

namespace Hangline.Line;

/// <summary>
/// The small marks a card wears: a play button when it is a recording, and the
/// corner cross that sends a file in Hangline's folder to the Recycle Bin — or
/// an eye with a slash when the file lives elsewhere and can only be taken off
/// the line, never deleted.
/// </summary>
public static class Badges
{
    private static readonly Brush Ink = Frozen(new SolidColorBrush(Color.FromArgb(225, 28, 30, 36)));
    private static readonly Brush PhotoEdge = Frozen(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)));
    private static readonly Pen InkPen = Frozen(new Pen(Ink, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly Pen PhotoEdgePen = Frozen(new Pen(PhotoEdge, 0.5));
    private static readonly Pen ThinInkPen = Frozen(new Pen(Ink, 1.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });

    public const double CrossDiameter = 20;
    public const double PlayDiameter = 28;
    public const double CrossHit = 26;

    private static Brush Frozen(Brush brush) { brush.Freeze(); return brush; }
    private static Pen Frozen(Pen pen) { pen.Freeze(); return pen; }

    /// The hairline the photo itself wears, inside the glass.
    public static void StrokePhoto(DrawingContext dc, Rect photo)
    {
        dc.DrawRoundedRectangle(null, PhotoEdgePen, photo, Layout.PhotoRadius, Layout.PhotoRadius);
    }

    public static void DrawCross(DrawingContext dc, Point center, double alpha)
    {
        GlassFrame.DrawCircle(dc, center, CrossDiameter, alpha);
        if (alpha <= 0) return;
        const double g = 3.4;
        dc.DrawGeometry(null, InkPen, new LineGeometry(
            new Point(center.X - g, center.Y - g), new Point(center.X + g, center.Y + g)));
        dc.DrawGeometry(null, InkPen, new LineGeometry(
            new Point(center.X + g, center.Y - g), new Point(center.X - g, center.Y + g)));
    }

    public static void DrawEyeSlash(DrawingContext dc, Point center, double alpha)
    {
        GlassFrame.DrawCircle(dc, center, CrossDiameter, alpha);
        if (alpha <= 0) return;

        // An eye: two arcs meeting at the corners, with the pupil between them.
        var left = new Point(center.X - 4.2, center.Y);
        var right = new Point(center.X + 4.2, center.Y);
        var lid = new PathFigure { StartPoint = left, IsClosed = false };
        lid.Segments.Add(new QuadraticBezierSegment(new Point(center.X, center.Y - 3.2), right, true));
        var bottom = new PathFigure { StartPoint = right, IsClosed = false };
        bottom.Segments.Add(new QuadraticBezierSegment(new Point(center.X, center.Y + 3.2), left, true));
        var geo = new PathGeometry();
        geo.Figures.Add(lid);
        geo.Figures.Add(bottom);
        dc.DrawGeometry(null, ThinInkPen, geo);
        dc.DrawEllipse(Ink, null, center, 1.3, 1.3);

        dc.DrawGeometry(null, InkPen, new LineGeometry(
            new Point(center.X - 4.6, center.Y + 4.6), new Point(center.X + 4.6, center.Y - 4.6)));
    }

    public static void DrawPlay(DrawingContext dc, Point center, double alpha)
    {
        GlassFrame.DrawCircle(dc, center, PlayDiameter, alpha);
        if (alpha <= 0) return;

        var figure = new PathFigure { StartPoint = new Point(center.X - 3.2, center.Y - 4.6), IsClosed = true };
        figure.Segments.Add(new LineSegment(new Point(center.X - 3.2, center.Y + 4.6), true));
        figure.Segments.Add(new LineSegment(new Point(center.X + 5.0, center.Y), true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        dc.DrawGeometry(Ink, null, geo);
    }
}
