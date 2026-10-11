using System.Windows;
using System.Windows.Media;
using Hangline.Core;

namespace Hangline.Line;

/// <summary>
/// The rope: a thin neutral line with a mid-grey core, a faint highlight and a
/// soft shadow, so it reads on light and dark backgrounds alike. It fades out at
/// both ends so it seems to come from beyond the screen.
/// </summary>
public static class RopeLayer
{
    /// The end-fade is baked into each stroke's brush rather than applied as an
    /// opacity mask: <c>DrawingContext</c> has no masked-brush overload, and a
    /// stroke's bounding box is a fragile thing to hang a gradient on.
    private static Pen Stroke(byte alpha, byte grey, double thickness, double width)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(width, 0),
        };
        Color solid = Color.FromArgb(alpha, grey, grey, grey);
        Color clear = Color.FromArgb(0, grey, grey, grey);
        brush.GradientStops.Add(new GradientStop(clear, 0));
        brush.GradientStops.Add(new GradientStop(solid, 0.08));
        brush.GradientStops.Add(new GradientStop(solid, 0.92));
        brush.GradientStops.Add(new GradientStop(clear, 1));
        brush.Freeze();

        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        pen.Freeze();
        return pen;
    }

    /// The quadratic curve the original draws, converted to the cubic WPF
    /// speaks: each control point moves two thirds of the way toward the quad
    /// control, which is exact rather than an approximation.
    private static PathGeometry Curve(double width)
    {
        double top = Layout.RopeTop;
        double overhang = Layout.RopeOverhang;
        var p0 = new Point(-overhang, top);
        var p2 = new Point(width + overhang, top);
        Pt q = Layout.RopeControl(width);
        var control = new Point(q.X, q.Y);

        var c1 = new Point(p0.X + (2.0 / 3) * (control.X - p0.X), p0.Y + (2.0 / 3) * (control.Y - p0.Y));
        var c2 = new Point(p2.X + (2.0 / 3) * (control.X - p2.X), p2.Y + (2.0 / 3) * (control.Y - p2.Y));

        var figure = new PathFigure { StartPoint = p0, IsClosed = false };
        figure.Segments.Add(new BezierSegment(c1, c2, p2, true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    public static void Draw(DrawingContext dc, double width)
    {
        if (width <= 0) return;
        PathGeometry geo = Curve(width);

        // The shadow, offset downward and a little wider. A blur would need an
        // Effect, and an Effect on a layered window costs CPU on every frame
        // for a 1.2pt halo; this reads the same at a glance.
        dc.PushTransform(new TranslateTransform(0, 1.2));
        dc.DrawGeometry(null, Stroke(46, 0, 2.4, width), geo);
        dc.Pop();

        dc.DrawGeometry(null, Stroke(255, 140, 1.2, width), geo);

        dc.PushTransform(new TranslateTransform(0, -0.35));
        dc.DrawGeometry(null, Stroke(115, 255, 0.4, width), geo);
        dc.Pop();
    }
}
