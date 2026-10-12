using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Hangline.Core;

namespace Hangline.Overlay;

public enum AnnotationTool
{
    Rectangle,
    Arrow,
    Pen,
    Text,
    Mosaic,
}

/// <summary>One mark. Points are in <b>image pixels</b>, not screen units, so
/// what is drawn on screen and what is baked into the saved file cannot drift
/// apart by a scale factor.</summary>
public sealed class Stroke
{
    public AnnotationTool Tool { get; init; }
    public List<Pt> Points { get; } = [];
    public uint Color { get; init; } = 0xFFFF3B30;
    public double Thickness { get; init; } = 6;
    public string? Text { get; init; }
    public double FontSize { get; init; } = 44;
    public double MosaicBlocks { get; init; } = 14;

    public Pt Start => Points.Count > 0 ? Points[0] : Pt.Zero;
    public Pt End => Points.Count > 0 ? Points[^1] : Pt.Zero;
}

/// <summary>
/// The marks on a screenshot, with undo and redo, and the one routine that
/// paints them. Nothing here knows about windows, so the same code is used for
/// the live preview and for the file that gets written back.
/// </summary>
public sealed class AnnotationDoc
{
    private readonly List<List<Stroke>> _undo = [];
    private readonly List<List<Stroke>> _redo = [];

    public List<Stroke> Strokes { get; private set; } = [];

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// Snapshot before a gesture so one drag is one undo step.
    public void PushState()
    {
        _undo.Add(Strokes.ToList());
        _redo.Clear();
        if (_undo.Count > 60) _undo.RemoveAt(0);
    }

    public void Add(Stroke stroke) => Strokes.Add(stroke);

    public bool Undo()
    {
        if (!CanUndo) return false;
        _redo.Add(Strokes);
        Strokes = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        _undo.Add(Strokes);
        Strokes = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        return true;
    }

    public void Render(DrawingContext dc, ImageSource image, int width, int height, ImageSource? pixelated)
    {
        dc.DrawImage(image, new Rect(0, 0, width, height));

        foreach (Stroke stroke in Strokes)
        {
            switch (stroke.Tool)
            {
                case AnnotationTool.Mosaic:
                    DrawMosaic(dc, stroke, pixelated, width, height);
                    break;
                case AnnotationTool.Pen:
                    DrawPath(dc, stroke, frozen: true);
                    break;
                case AnnotationTool.Rectangle:
                    DrawRect(dc, stroke);
                    break;
                case AnnotationTool.Arrow:
                    DrawArrow(dc, stroke);
                    break;
                case AnnotationTool.Text:
                    DrawText(dc, stroke);
                    break;
            }
        }
    }

    private static Brush Brush(Stroke stroke) =>
        new SolidColorBrush(Color.FromRgb((byte)(stroke.Color >> 16), (byte)(stroke.Color >> 8), (byte)stroke.Color));

    private static Pen Pen(Stroke stroke)
    {
        var pen = new Pen(Brush(stroke), stroke.Thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        return pen;
    }

    private static void DrawPath(DrawingContext dc, Stroke stroke, bool frozen)
    {
        if (stroke.Points.Count < 2) return;
        var figure = new PathFigure { StartPoint = ToPoint(stroke.Points[0]), IsClosed = false };
        for (int i = 1; i < stroke.Points.Count; i++)
            figure.Segments.Add(new LineSegment(ToPoint(stroke.Points[i]), true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        if (frozen) geo.Freeze();
        dc.DrawGeometry(null, Pen(stroke), geo);
    }

    private static void DrawRect(DrawingContext dc, Stroke stroke)
    {
        if (stroke.Points.Count < 2) return;
        RectD r = RectD.FromPoints(stroke.Points[0], stroke.Points[^1]);
        dc.DrawRoundedRectangle(null, Pen(stroke), ToRect(r), stroke.Thickness, stroke.Thickness);
    }

    private static void DrawArrow(DrawingContext dc, Stroke stroke)
    {
        if (stroke.Points.Count < 2) return;
        var from = ToPoint(stroke.Points[0]);
        var to = ToPoint(stroke.Points[^1]);
        dc.DrawGeometry(null, Pen(stroke), new LineGeometry(from, to));

        // A head that scales with the stroke, so a thick arrow does not get a
        // dinky point on the end.
        double dx = to.X - from.X, dy = to.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1) return;
        double ux = dx / length, uy = dy / length;
        double size = Math.Max(12, stroke.Thickness * 4.5);
        var left = new Point(to.X - ux * size - uy * size * 0.5, to.Y - uy * size + ux * size * 0.5);
        var right = new Point(to.X - ux * size + uy * size * 0.5, to.Y - uy * size - ux * size * 0.5);

        var figure = new PathFigure { StartPoint = to, IsClosed = true };
        figure.Segments.Add(new LineSegment(left, true));
        figure.Segments.Add(new LineSegment(right, true));
        var head = new PathGeometry();
        head.Figures.Add(figure);
        dc.DrawGeometry(Brush(stroke), null, head);
    }

    private static void DrawText(DrawingContext dc, Stroke stroke)
    {
        if (string.IsNullOrEmpty(stroke.Text)) return;
        var formatted = new FormattedText(
            stroke.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI, Segoe UI"), FontStyles.Normal,
                         FontWeights.SemiBold, FontStretches.Normal),
            stroke.FontSize, Brush(stroke), 1.0);
        dc.DrawText(formatted, ToPoint(stroke.Start));
    }

    /// A pixelated copy of the image, shown only where the gesture went. The
    /// stroke is widened into a filled shape first — that is how a freehand
    /// band becomes something a brush can fill, without a custom Geometry.
    private static void DrawMosaic(DrawingContext dc, Stroke stroke, ImageSource? pixelated, int width, int height)
    {
        if (pixelated is null || stroke.Points.Count < 2) return;

        var brush = new ImageBrush(pixelated)
        {
            Stretch = Stretch.Fill,
            Viewport = new Rect(0, 0, width, height),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();

        Geometry band;
        if (stroke.Points.Count == 2)
        {
            band = new RectangleGeometry(ToRect(RectD.FromPoints(stroke.Points[0], stroke.Points[^1])));
        }
        else
        {
            var figure = new PathFigure { StartPoint = ToPoint(stroke.Points[0]), IsClosed = false };
            for (int i = 1; i < stroke.Points.Count; i++)
                figure.Segments.Add(new LineSegment(ToPoint(stroke.Points[i]), true));
            var path = new PathGeometry();
            path.Figures.Add(figure);

            var widen = new Pen(Brushes.Black, Math.Max(stroke.Thickness * 3, stroke.MosaicBlocks * 4))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            band = path.GetWidenedPathGeometry(widen);
        }

        dc.DrawGeometry(brush, null, band);
    }

    private static Point ToPoint(Pt p) => new(p.X, p.Y);
    private static Rect ToRect(RectD r) => new(r.X, r.Y, r.Width, r.Height);
}
