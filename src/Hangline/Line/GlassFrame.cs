using System.Windows;
using System.Windows.Media;
using Hangline.Core;

namespace Hangline.Line;

/// <summary>
/// The glass the cards and badges are cut from: a light frosted fill with a
/// thin specular edge lit from above.
/// <para>
/// This is the one deliberate visual downgrade from macOS. The original uses
/// <c>.ultraThinMaterial</c>, a real backdrop blur; Windows exposes no
/// supported way to blur what another process drew behind us, so the material
/// is approximated with a translucent gradient. Geometry, borders and shadows
/// stay exact — see docs/parity.md.
/// </para>
/// </summary>
public static class GlassFrame
{
    private static readonly Brush Fill = Frozen(new LinearGradientBrush(
        Color.FromArgb(188, 250, 250, 252), Color.FromArgb(160, 236, 238, 243),
        new Point(0.5, 0), new Point(0.5, 1)));

    private static readonly Brush Specular = Frozen(new LinearGradientBrush(
        Color.FromArgb(140, 255, 255, 255), Color.FromArgb(31, 255, 255, 255),
        new Point(0.5, 0), new Point(0.5, 1)));

    private static readonly Pen OuterPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(26, 0, 0, 0)), 0.5));
    private static readonly Pen SpecularPen = Frozen(new Pen(Specular, 0.75));

    private static readonly Brush ShadowFar = Frozen(new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)));
    private static readonly Brush ShadowNear = Frozen(new SolidColorBrush(Color.FromArgb(26, 0, 0, 0)));

    private static Brush Frozen(Brush brush) { brush.Freeze(); return brush; }
    private static Pen Frozen(Pen pen) { pen.Freeze(); return pen; }

    /// Soft shadow under a card. Two offset rounded rects stand in for a blur:
    /// an Effect on a layered window costs CPU on every frame, and the line is
    /// on screen for hours at a time.
    public static void DrawShadow(DrawingContext dc, Rect r, bool hovered)
    {
        double radius = Layout.FrameRadius;
        double spread = hovered ? 3 : 2;
        double y = hovered ? 8 : 5;

        dc.DrawRoundedRectangle(ShadowFar, null,
            Moved(Inflated(r, spread * 2), 0, y * 1.4), radius + spread * 2, radius + spread * 2);
        dc.DrawRoundedRectangle(ShadowNear, null,
            Moved(Inflated(r, spread), 0, y), radius + spread, radius + spread);
    }

    public static void Draw(DrawingContext dc, Rect r, double radius, double alpha = 1)
    {
        if (alpha < 1) dc.PushOpacity(alpha);

        dc.DrawRoundedRectangle(Fill, null, r, radius, radius);
        dc.DrawRoundedRectangle(null, SpecularPen, r, radius, radius);
        // The outer hairline sits half a point outside the shape, as the
        // original's padding(-0.5) puts it.
        dc.DrawRoundedRectangle(null, OuterPen, Inflated(r, 0.5), radius + 0.5, radius + 0.5);

        if (alpha < 1) dc.Pop();
    }

    /// Badges are circles cut from the same glass.
    public static void DrawCircle(DrawingContext dc, Point center, double diameter, double alpha = 1)
    {
        var r = new Rect(center.X - diameter / 2, center.Y - diameter / 2, diameter, diameter);
        if (alpha < 1) dc.PushOpacity(alpha);
        dc.DrawEllipse(Fill, null, center, diameter / 2, diameter / 2);
        dc.DrawEllipse(null, SpecularPen, center, diameter / 2, diameter / 2);
        dc.DrawEllipse(null, OuterPen, center, diameter / 2 + 0.5, diameter / 2 + 0.5);
        if (alpha < 1) dc.Pop();
    }

    /// Concentric corners: the photo's radius is the frame's minus the inset,
    /// the way macOS rounds nested shapes.
    public static Rect PhotoRect(Rect card) => Inflated(card, -Layout.FrameInset);

    public static void PushPhotoClip(DrawingContext dc, Rect photo) =>
        dc.PushClip(new RectangleGeometry(photo, Layout.PhotoRadius, Layout.PhotoRadius));

    // WPF's Rect.Inflate mutates in place and returns void, so the expression
    // form has to be written out.
    internal static Rect Inflated(Rect r, double d) => new(r.X - d, r.Y - d, r.Width + 2 * d, r.Height + 2 * d);

    internal static Rect Moved(Rect r, double dx, double dy) => new(r.X + dx, r.Y + dy, r.Width, r.Height);
}
