namespace Hangline.Core;

/// <summary>Small / medium / large — how big the photos hang.</summary>
public enum LineSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// Every number that decides where the rope is and where each photo hangs.
/// Ported 1:1 from the original Layout so the line looks the same.
/// The clip, the rope and the glass frame keep their size across tiers; only
/// the photos and the room they take grow or shrink.
/// </summary>
public static class Layout
{
    // Rope.
    public const double RopeTop = 10;
    public const double MaxSag = 30;
    public const double SagPerWidth = 0.018;
    public const double RopeOverhang = 20;

    // Clothespin.
    public const double PinWidth = 9;
    public const double PinHeight = 26;
    public const double PinRadius = 3.5;
    public const double PinAbove = 9.5;
    public const double PinOverlap = 12;
    public const double CardOffsetBelowTop = PinHeight - PinOverlap;

    // Glass frame (does not scale with the size tier).
    public const double FrameRadius = 16;
    public const double FrameInset = 4;
    public const double PhotoRadius = FrameRadius - FrameInset;
    public const double PhotoWidthPad = 14;

    // Card grid.
    public const double SpacingPad = 24;
    public const double PanelBottomPad = 106;
    public const double CapacityMargin = 200;
    public const double EdgeFadeBand = 90;
    public const double RetractExtra = 12;
    public const int MinVisible = 3;
    public const int MaxVisible = 12;
    public const int DefaultMaxItems = 8;

    public static double ScaleOf(LineSize size) => size switch
    {
        LineSize.Small => 0.8,
        LineSize.Large => 1.35,
        _ => 1.0,
    };

    public static LineSize ParseSize(string? name) => name switch
    {
        "small" => LineSize.Small,
        "large" => LineSize.Large,
        _ => LineSize.Medium,
    };

    /// The original rounds with <c>.rounded()</c>, which sends a .5 away from
    /// zero; .NET's default rounds to even, which would make a large card 202
    /// points wide instead of 203.
    private static double Rounded(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    public static double PhotoMaxHeight(LineSize size) => Rounded(104 * ScaleOf(size));

    public static double CardWidth(LineSize size) => Rounded(150 * ScaleOf(size));

    public static double Spacing(LineSize size) => CardWidth(size) + SpacingPad;

    public static double PanelHeight(LineSize size) => PhotoMaxHeight(size) + PanelBottomPad;

    /// The rope hangs as a parabola from edge to edge of the screen.
    public static double Sag(double width) => Math.Min(MaxSag, width * SagPerWidth);

    public static double RopeY(double x, double width)
    {
        if (width <= 0) return RopeTop;
        double f = x / width;
        return RopeTop + 4 * Sag(width) * f * (1 - f);
    }

    /// The single control point of the quadratic Bézier the original draws:
    /// from (-20, ropeTop) to (width+20, ropeTop), control (width/2, ropeTop+2*sag).
    public static Pt RopeControl(double width) => new(width / 2, RopeTop + 2 * Sag(width));

    /// How many photos fit across the screen.
    public static int VisibleFits(double width, LineSize size)
    {
        double spacing = Spacing(size);
        if (spacing <= 0) return MinVisible;
        return Num.Clamp((int)Math.Floor((width - CapacityMargin) / spacing), MinVisible, MaxVisible);
    }

    /// With Keep on line the line can hold more than fit; the rest is reached by scrolling.
    public static int MaxItems(int fits, int? keepOnLine) => Math.Max(fits, keepOnLine ?? fits);

    /// Photos hang `spacing` apart, centred. When there are more than `visible`:
    /// with Keep on line set they keep their spacing and the newest stays where
    /// the last one would hang on a full line, `scroll` bringing older ones in
    /// from the left. Otherwise they move closer together so none is past the edge.
    public static double CardX(int index, int count, double width, double spacing,
                               int visible, double scroll, int? keepOnLine)
    {
        if (count <= 0) return width / 2;

        if (keepOnLine is not null && count > visible)
        {
            double newest = width / 2 + (visible - 1) * spacing / 2;
            return newest - (count - 1 - index) * spacing + scroll;
        }

        double gaps = Math.Max(count - 1, 0);
        double step = gaps > 0 ? Math.Min(spacing, Math.Max(0, width - CapacityMargin) / gaps) : spacing;
        return width / 2 - gaps * step / 2 + index * step;
    }

    /// Photos scrolled towards an edge fade out before they reach it.
    public static double EdgeFade(double x, double width) =>
        Math.Min(1, Math.Max(0, Math.Min(x, width - x) / EdgeFadeBand));

    public static double PinTopY(double ropeY) => ropeY - PinAbove;

    /// The photo fits inside the card area keeping its proportions, so the
    /// white border hugs it whether the screenshot is wide or tall.
    public static SizeD PhotoSize(SizeD source, LineSize size)
    {
        double maxW = CardWidth(size) - PhotoWidthPad;
        double maxH = PhotoMaxHeight(size);
        if (source.Width <= 0 || source.Height <= 0) return new SizeD(maxW, maxH);
        double scale = Math.Min(maxW / source.Width, maxH / source.Height);
        return new SizeD(source.Width * scale, source.Height * scale);
    }

    /// The card around the photo: the photo plus the glass inset.
    public static SizeD CardSize(SizeD source, LineSize size)
    {
        SizeD photo = PhotoSize(source, size);
        return new SizeD(photo.Width + FrameInset * 2, photo.Height + FrameInset * 2);
    }

    /// Tucked away, the line waits above the top edge and slides out from under it.
    public static double RetractOffset(LineSize size) => -(PanelHeight(size) + RetractExtra);
}
