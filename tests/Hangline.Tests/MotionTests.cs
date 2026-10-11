using Hangline.Core;
using Xunit;

namespace Hangline.Tests;

public class MotionTests
{
    [Fact]
    public void A_response_of_0_42_is_a_natural_frequency_of_2pi_over_it()
    {
        var (k, c) = Spring.FromResponse(0.42, 0.82);
        double w0 = 2 * Math.PI / 0.42;
        Assert.Equal(w0 * w0, k, 2);
        Assert.Equal(2 * 0.82 * w0, c, 2);
    }

    [Fact]
    public void A_damping_fraction_of_one_does_not_overshoot()
    {
        List<double> curve = Spring.Curve(from: -222, to: 0, response: 0.42, dampingFraction: 1.0, duration: 1.5);
        Assert.All(curve, y => Assert.True(y <= 0.001, $"critically damped spring overshot to {y}"));
    }

    [Fact]
    public void A_softly_damped_spring_bounces_past_its_target_once()
    {
        List<double> curve = Spring.Curve(from: -46, to: 0, response: 0.42, dampingFraction: 0.5, duration: 2);
        Assert.Contains(curve, y => y > 0.5);
    }

    [Fact]
    public void The_reveal_settles_inside_its_animation_window()
    {
        List<double> curve = Spring.Curve(from: -222, to: 0, response: 0.42, dampingFraction: 0.82, duration: 1.2);
        Assert.True(Spring.IsSettled(curve[^1], 0, 0), "the reveal had not settled after 1.2s");
    }

    [Fact]
    public void An_over_damped_spring_is_not_settled_while_it_is_still_crawling() =>
        Assert.False(Spring.IsSettled(-100, 0.001, 0));

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.125)]
    [InlineData(1.0, 1.0)]
    public void Ease_in_cubic(double k, double expected) =>
        Assert.Equal(expected, Ease.InCubic(k), 5);

    [Fact]
    public void Easings_are_monotonic_and_meet_at_the_ends()
    {
        foreach (Func<double, double> ease in new[] { Ease.InCubic, Ease.OutCubic, Ease.InOutCubic, Ease.InQuad, Ease.OutQuad })
        {
            Assert.Equal(0, ease(0), 5);
            Assert.Equal(1, ease(1), 5);
            double previous = double.NegativeInfinity;
            for (double k = 0; k <= 1.0001; k += 0.01)
            {
                double value = ease(k);
                Assert.True(value >= previous - 1e-9, $"{nameof(Ease.InCubic)} went backwards at {k}");
                previous = value;
            }
        }
    }

    [Fact]
    public void The_flight_chrome_stays_invisible_until_thirty_five_percent()
    {
        Assert.Equal(0, Ease.SmoothStep(0.35, 1, 0), 5);
        Assert.Equal(0, Ease.SmoothStep(0.35, 1, 0.34), 5);
        Assert.True(Ease.SmoothStep(0.35, 1, 0.36) > 0);
        Assert.Equal(1, Ease.SmoothStep(0.35, 1, 1), 5);
    }

    [Fact]
    public void The_arc_lifts_then_returns()
    {
        Assert.Equal(0, Ease.ArcLift(0, 30), 5);
        Assert.Equal(30, Ease.ArcLift(0.5, 30), 5);
        Assert.Equal(0, Ease.ArcLift(1, 30), 5);
    }
}

public class ShortcutTests
{
    [Fact]
    public void The_default_is_control_alt_t() =>
        Assert.Equal("Ctrl+Alt+T", Shortcut.Standard.Display);

    [Fact]
    public void A_shortcut_with_no_modifier_would_swallow_typing()
    {
        Assert.False(new Shortcut(0x41, HotKeyModifiers.None, "A").IsValid);
        Assert.True(new Shortcut(0x41, HotKeyModifiers.Control, "A").IsValid);
    }

    [Fact]
    public void An_empty_shortcut_displays_a_dash_and_registers_nothing()
    {
        Assert.True(Shortcut.None.IsEmpty);
        Assert.False(Shortcut.None.IsValid);
        Assert.Equal("—", Shortcut.None.Display);
    }
}
