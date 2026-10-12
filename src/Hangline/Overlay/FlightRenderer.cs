using System.Windows;
using System.Windows.Media;
using Hangline.Core;
using Hangline.Line;

namespace Hangline.Overlay;

/// <summary>
/// One animated photo: either flying from where it was captured onto the line,
/// or falling off the line. Both are drawn here rather than by the card itself,
/// because the card lives inside the strip and would be clipped by it.
/// </summary>
public sealed class Flight
{
    public required PeggedItem? Item { get; init; }

    /// Both rectangles are already in the overlay's own units.
    public required RectD From { get; init; }
    public required RectD To { get; init; }

    public required double Tilt { get; init; }
    public bool Falling { get; init; }
    public double Duration { get; init; } = 0.65;

    public double Elapsed { get; set; }
    public bool Done { get; set; }
    public Action? OnLanded { get; init; }

    public double Progress => Math.Clamp(Elapsed / Duration, 0, 1);
}

public static class FlightRenderer
{
    private static readonly Pen PhotoEdge = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), 0.5));

    private static Pen Frozen(Pen pen) { pen.Freeze(); return pen; }

    public static void Draw(DrawingContext dc, Flight flight)
    {
        double k = flight.Falling ? Ease.InCubic(flight.Progress) : Ease.InOutCubic(flight.Progress);

        if (flight.Falling)
        {
            DrawFall(dc, flight, k);
            return;
        }

        // The original anchors the flying photo at its top centre, so it grows
        // downwards out of the capture area rather than expanding around a middle.
        double width = Num.Lerp(flight.From.Width, flight.To.Width, k);
        double height = Num.Lerp(flight.From.Height, flight.To.Height, k);
        double cx = Num.Lerp(flight.From.CenterX, flight.To.CenterX, k);
        double top = Num.Lerp(flight.From.Y, flight.To.Y, k) + Ease.ArcLift(k, 30);

        var rect = new Rect(cx - width / 2, top, width, height);
        double radius = Num.Lerp(0, Layout.FrameRadius, k);
        double chrome = Ease.SmoothStep(0.35, 1, k);

        dc.PushTransform(new RotateTransform(-flight.Tilt * k, cx, top));

        if (flight.Item?.Thumb is ImageSource image)
        {
            dc.PushClip(new RectangleGeometry(rect, radius, radius));
            dc.DrawImage(image, rect);
            dc.Pop();
        }
        else
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(60, 62, 66)), null, rect, radius, radius);
        }

        if (chrome > 0)
        {
            double inset = Num.Lerp(0, Layout.FrameInset, chrome);
            var frame = new Rect(rect.X - inset, rect.Y - inset, rect.Width + inset * 2, rect.Height + inset * 2);
            GlassFrame.Draw(dc, frame, Layout.FrameRadius, chrome);
            dc.DrawRoundedRectangle(null, PhotoEdge, rect, radius, radius);

            if (chrome > 0.5)
                Clothespin.Draw(dc, new Point(frame.X + frame.Width / 2, frame.Y + Layout.CardOffsetBelowTop - Layout.PinHeight));
        }

        dc.Pop();
    }

    /// The fall: it drops 520 points, turns faster than it falls, and fades out.
    private static void DrawFall(DrawingContext dc, Flight flight, double k)
    {
        double drop = 520 * k;
        double alpha = 1 - k;
        var rect = new Rect(flight.To.X, flight.To.Y + drop, flight.To.Width, flight.To.Height);
        double angle = flight.Tilt + (flight.Tilt * 7 + 20) * k;

        dc.PushOpacity(alpha);
        dc.PushTransform(new RotateTransform(angle, rect.X + rect.Width / 2, rect.Y));

        if (flight.Item?.Thumb is ImageSource image)
        {
            GlassFrame.Draw(dc, rect, Layout.FrameRadius);
            Rect photo = GlassFrame.PhotoRect(rect);
            dc.PushClip(new RectangleGeometry(photo, Layout.PhotoRadius, Layout.PhotoRadius));
            dc.DrawImage(image, photo);
            dc.Pop();
        }

        Clothespin.Draw(dc, new Point(rect.X + rect.Width / 2, rect.Y - Layout.PinAbove));

        dc.Pop();
        dc.Pop();
    }
}
