namespace Hangline.Core;

/// <summary>Double-precision point/rect sizes. The Core layer never touches
/// WPF or Win32 geometry types so its math stays unit-testable.</summary>
public readonly record struct Pt(double X, double Y)
{
    public static readonly Pt Zero = new(0, 0);

    public Pt Offset(double dx, double dy) => new(X + dx, Y + dy);

    public double DistanceTo(Pt other) => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
}

public readonly record struct SizeD(double Width, double Height)
{
    public static readonly SizeD Zero = new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public static readonly RectD Empty = new(0, 0, 0, 0);

    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public Pt Origin => new(X, Y);
    public Pt Center => new(CenterX, CenterY);
    public SizeD Size => new(Width, Height);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// Negative <paramref name="d"/> shrinks, matching CGRect.insetBy.
    public RectD Inflate(double d) => new(X - d, Y - d, Width + 2 * d, Height + 2 * d);

    public RectD Inflate(double dx, double dy) => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);

    public bool Contains(Pt p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

    public bool Contains(double x, double y) => Contains(new Pt(x, y));

    public RectD WithCenter(double cx, double cy) => new(cx - Width / 2, cy - Height / 2, Width, Height);

    public static RectD Union(RectD a, RectD b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;
        double x = Math.Min(a.Left, b.Left);
        double y = Math.Min(a.Top, b.Top);
        return new RectD(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }

    public static RectD FromPoints(Pt a, Pt b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        return new RectD(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
}

public static class Num
{
    public static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));

    public static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
