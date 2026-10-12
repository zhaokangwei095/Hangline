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
        foreach (Func<double, double> ease in new[] { Ease.InCubic, Ease.InOutCubic, Ease.InQuad, Ease.OutQuad })
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

    [Fact]
    public void The_first_reveal_of_a_session_slides_down_instead_of_snaping()
    {
        // Placing the window parks the controller above the edge. Without that
        // the spring starts at its target and the line appears with no motion.
        var motion = new Line.MotionController { PanelHeight = 210 };
        motion.SnapTo(motion.RetractedY);
        motion.Reveal();
        motion.Tick(1.0 / 60);
        Assert.True(motion.RevealY < -1, $"reveal started at {motion.RevealY}");
    }

    [Fact]
    public void The_corner_badge_fades_in_over_its_clock_and_keeps_asking_for_frames()
    {
        var motion = new Line.MotionController();
        var id = Guid.NewGuid();
        motion.Hover(id, true);

        int frames = 0;
        while (frames < 120 && motion.Tick(1.0 / 60)) frames++;

        Assert.Equal(1, motion.Get(id).Badge, 3);
        // 0.26 s at 60 Hz is ~16 frames; anything much shorter means the fade
        // is not what keeps the loop alive.
        Assert.InRange(frames, 12, 40);
    }

    [Fact]
    public void The_corner_badge_fades_back_out_and_then_the_loop_stops()
    {
        var motion = new Line.MotionController();
        var id = Guid.NewGuid();
        motion.Hover(id, true);
        for (int i = 0; i < 300 && motion.Tick(1.0 / 60); i++) { }
        Assert.Equal(1, motion.Get(id).Badge, 3);

        motion.Hover(id, false);
        int frames = 0;
        while (frames < 120 && motion.Tick(1.0 / 60)) frames++;

        Assert.Equal(0, motion.Get(id).Badge, 3);
        Assert.True(frames > 1, "淡出也要走完自己的时长，不能直接消失");
    }

    [Fact]
    public void Hiding_the_line_clears_hover_so_no_badge_is_left_burning()
    {
        var motion = new Line.MotionController();
        var id = Guid.NewGuid();
        motion.Hover(id, true);
        motion.ClearHovers();
        Assert.False(motion.Get(id).Hovered);
    }
}

public class RevealTests
{
    [Fact]
    public void Resting_in_the_band_brings_the_line_down()
    {
        var reveal = new Input.RevealController();
        bool shown = false;
        reveal.Apply = on => shown = on;
        for (double t = 0; t < Input.RevealController.HoverDelay + 0.05; t += 0.05)
        {
            reveal.Tick(t, 0.05, inBand: true, insidePanel: false);
        }
        Assert.True(shown);
    }

    [Fact]
    public void A_click_in_the_band_takes_the_line_up_and_keeps_it_up()
    {
        var reveal = new Input.RevealController();
        var visits = new List<bool>();
        reveal.Apply = shown => visits.Add(shown);

        reveal.Force(true);
        Assert.True(reveal.Wanted);

        reveal.ClickedInBand();
        Assert.False(reveal.Wanted);

        // Still resting in the band, but a reveal was just clicked away: the
        // line stays up until the pointer leaves and comes back.
        reveal.Tick(1, 0.05, inBand: true, insidePanel: false);
        reveal.Tick(1.05, 0.05, inBand: true, insidePanel: false);
        Assert.DoesNotContain(true, visits.Skip(1));

        reveal.Tick(2, 0.05, inBand: false, insidePanel: false);
        for (double t = 2; t < 2.4; t += 0.05) reveal.Tick(t, 0.05, inBand: true, insidePanel: false);
        Assert.True(reveal.Wanted);
    }

    [Fact]
    public void A_held_line_stays_down_and_returns_to_normal_rules_when_released()
    {
        var reveal = new Input.RevealController();
        bool shown = false;
        reveal.Apply = on => shown = on;

        reveal.Force(true);              // what bringing the line down does
        reveal.HoldOpen = true;
        for (double t = 0; t < 1.2; t += 0.05) reveal.Tick(t, 0.05, inBand: false, insidePanel: false);
        Assert.True(shown);              // pointer far away, nothing hanging: still held

        reveal.Release();
        for (double t = 2; t < 3; t += 0.05) reveal.Tick(t, 0.05, inBand: false, insidePanel: false);
        Assert.False(shown);             // the manual flag went with the hold
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
    public void PrintScreen_is_a_valid_lone_key_because_it_is_not_typing()
    {
        Assert.True(Shortcut.PrintScreen.IsValid);
        Assert.Equal("PrtSc", Shortcut.PrintScreen.Display);
    }

    [Fact]
    public void Function_keys_may_also_stand_alone() =>
        Assert.True(new Shortcut(0x70, HotKeyModifiers.None, "F1").IsValid);

    [Fact]
    public void A_bare_letter_still_needs_a_modifier() =>
        Assert.False(new Shortcut(0x41, HotKeyModifiers.None, "A").IsValid);

    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x54, "T")]
    [InlineData(0x37, "7")]
    [InlineData(0x70, "F1")]
    [InlineData(0x7B, "F12")]
    [InlineData(0x2C, "PrtSc")]
    [InlineData(0x20, "Space")]
    public void Keys_get_the_names_a_user_would_recognise(int vk, string expected) =>
        Assert.Equal(expected, Shortcut.LabelFor(vk));

    [Fact]
    public void An_empty_shortcut_displays_a_dash_and_registers_nothing()
    {
        Assert.True(Shortcut.None.IsEmpty);
        Assert.False(Shortcut.None.IsValid);
        Assert.Equal("—", Shortcut.None.Display);
    }
}
