using Hangline.Core;
using Xunit;

namespace Hangline.Tests;

/// <summary>
/// Every expected number here is taken from the upstream Swift, not from this
/// port's own behaviour — a test that agrees with a bug is worse than none.
/// </summary>
public class LayoutTests
{
    public static TheoryData<double, double> SagData() => new()
    {
        { 500, 9 },
        { 1000, 18 },
        { 1667, 30 },      // 1667 * 0.018 = 30.0
        { 1707, 30 },      // 30.7, capped
        { 3000, 30 },
    };

    [Theory]
    [MemberData(nameof(SagData))]
    public void Sag_grows_with_width_then_stops(double width, double expected) =>
        Assert.Equal(expected, Layout.Sag(width), 3);

    [Fact]
    public void Rope_is_flat_at_the_ends_and_sags_in_the_middle()
    {
        const double width = 1000;
        Assert.Equal(Layout.RopeTop, Layout.RopeY(0, width), 3);
        Assert.Equal(Layout.RopeTop + Layout.Sag(width), Layout.RopeY(width / 2, width), 3);
        Assert.Equal(Layout.RopeTop, Layout.RopeY(width, width), 3);
    }

    [Fact]
    public void Rope_with_no_width_is_the_top_line() =>
        Assert.Equal(Layout.RopeTop, Layout.RopeY(0, 0));

    [Fact]
    public void Three_photos_centre_on_the_screen()
    {
        const double spacing = 174;
        double x0 = Layout.CardX(0, 3, 1200, spacing, 8, 0, null);
        double x1 = Layout.CardX(1, 3, 1200, spacing, 8, 0, null);
        double x2 = Layout.CardX(2, 3, 1200, spacing, 8, 0, null);

        Assert.Equal(426, x0, 1);
        Assert.Equal(600, x1, 1);
        Assert.Equal(774, x2, 1);
    }

    [Fact]
    public void A_single_photo_sits_dead_centre() =>
        Assert.Equal(600, Layout.CardX(0, 1, 1200, 174, 8, 0, null), 1);

    [Fact]
    public void Photos_pull_closer_rather_than_hanging_off_the_edge()
    {
        // 12 photos on a 1200 wide screen: step shrinks below the normal spacing
        // so the outermost still lands inside the 200pt margin.
        double first = Layout.CardX(0, 12, 1200, 174, 8, 0, null);
        double last = Layout.CardX(11, 12, 1200, 174, 8, 0, null);
        Assert.True(first >= 100, $"first photo at {first} is past the margin");
        Assert.True(last <= 1100, $"last photo at {last} is past the margin");
        Assert.Equal(1200, first + last, 1);
    }

    [Fact]
    public void With_keep_on_line_the_newest_hangs_where_a_full_line_ends()
    {
        const double spacing = 174;
        double newest = Layout.CardX(29, 30, 1200, spacing, 8, 0, 25);
        Assert.Equal(1200 / 2 + 7 * spacing / 2, newest, 1);

        // Older ones march left at the full spacing instead of squeezing.
        double previous = Layout.CardX(28, 30, 1200, spacing, 8, 0, 25);
        Assert.Equal(spacing, newest - previous, 1);
    }

    public static TheoryData<double, int> VisibleData() => new()
    {
        { 200, 3 },
        { 600, 3 },
        { 1707, 8 },
        { 4000, 12 },
    };

    [Theory]
    [MemberData(nameof(VisibleData))]
    public void How_many_photos_fit(double width, int expected) =>
        Assert.Equal(expected, Layout.VisibleFits(width, LineSize.Medium));

    [Fact]
    public void Size_tiers_scale_the_photos_not_the_clip()
    {
        Assert.Equal(104, Layout.PhotoMaxHeight(LineSize.Medium));
        Assert.Equal(83, Layout.PhotoMaxHeight(LineSize.Small));
        Assert.Equal(140, Layout.PhotoMaxHeight(LineSize.Large));

        Assert.Equal(150, Layout.CardWidth(LineSize.Medium));
        Assert.Equal(203, Layout.CardWidth(LineSize.Large));

        // The clip and the frame are the same object at every size.
        Assert.Equal(9, Layout.PinWidth);
        Assert.Equal(26, Layout.PinHeight);
        Assert.Equal(16, Layout.FrameRadius);
    }

    [Fact]
    public void A_wide_photo_is_limited_by_width_and_a_tall_one_by_height()
    {
        SizeD wide = Layout.PhotoSize(new SizeD(3000, 500), LineSize.Medium);
        Assert.Equal(136, wide.Width, 1);
        Assert.True(wide.Height <= 104.01);

        SizeD tall = Layout.PhotoSize(new SizeD(500, 3000), LineSize.Medium);
        Assert.Equal(104, tall.Height, 1);
        Assert.True(tall.Width <= 136.01);
    }

    [Fact]
    public void The_card_is_the_photo_plus_the_glass_inset()
    {
        SizeD photo = Layout.PhotoSize(new SizeD(1000, 700), LineSize.Medium);
        SizeD card = Layout.CardSize(new SizeD(1000, 700), LineSize.Medium);
        Assert.Equal(photo.Width + 8, card.Width, 1);
        Assert.Equal(photo.Height + 8, card.Height, 1);
    }

    [Fact]
    public void A_degenerate_photo_still_gets_the_full_card() =>
        Assert.Equal(new SizeD(136, 104), Layout.PhotoSize(new SizeD(0, 0), LineSize.Medium));

    public static TheoryData<double, double> FadeData() => new()
    {
        { 0, 0 },
        { 45, 0.5 },
        { 90, 1 },
        { 500, 1 },
        { 910, 1 },
        { 955, 0.5 },
        { 990, 0.11111111 },
        { 1000, 0 },
    };

    [Theory]
    [MemberData(nameof(FadeData))]
    public void The_fade_is_symmetric(double x, double expected) =>
        Assert.Equal(expected, Layout.EdgeFade(x, 1000), 3);

    [Fact]
    public void The_clip_sits_above_the_rope_and_the_card_below_it()
    {
        double rope = 40;
        Assert.Equal(rope - Layout.PinAbove, Layout.PinTopY(rope));
        Assert.Equal(14, Layout.CardOffsetBelowTop);
    }

    [Fact]
    public void Tucked_away_the_line_is_clear_of_the_top_edge() =>
        Assert.Equal(-(Layout.PanelHeight(LineSize.Medium) + 12), Layout.RetractOffset(LineSize.Medium));
}
