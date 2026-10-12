namespace Hangline.Core;

/// <summary>Normalised easing curves, k in 0..1.</summary>
public static class Ease
{
    public static double InCubic(double k) => k * k * k;

    public static double InOutCubic(double k) => k < 0.5 ? 4 * k * k * k : 1 - Math.Pow(-2 * k + 2, 3) / 2;

    public static double InQuad(double k) => k * k;

    public static double OutQuad(double k) => 1 - (1 - k) * (1 - k);

    public static double SmoothStep(double x)
    {
        double t = Num.Clamp(x, 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static double SmoothStep(double edge0, double edge1, double x)
    {
        if (edge1 <= edge0) return x >= edge1 ? 1 : 0;
        return SmoothStep((x - edge0) / (edge1 - edge0));
    }

    /// The arc a hanging photo lifts on its way to the line.
    public static double ArcLift(double k, double height) => Math.Sin(Math.PI * k) * height;
}
