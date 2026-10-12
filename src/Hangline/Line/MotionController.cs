using Hangline.Core;

namespace Hangline.Line;

/// <summary>Everything that can be in motion about one hanging photo.</summary>
public sealed class CardMotion
{
    /// Degrees of swing around the clip.
    public double Swing;
    public double SwingVel;

    /// Vertical offset of the arrival drop, 0 once hung.
    public double ArrivalY = -46;
    public double ArrivalVel;
    public bool Arriving = true;

    /// The card's own scale: hover lifts it, holding presses it in.
    public double Scale = 1;
    public double ScaleVel;

    /// 0..1 fade of the whole card, used by the landing cross-fade.
    public double Opacity = 1;

    public bool Hovered;
    public bool Pressed;

    /// 0..1 progress of the corner badge's fade, advanced over BadgeFade and
    /// eased by the view. It is a number here rather than a WPF animation so
    /// the frame loop can tell that it still has work to do.
    public double Badge;

    /// A nudge runs in two beats, as in the original: ease out to the angle,
    /// then a spring home.
    public double NudgeTarget;
    public double NudgeFrom;
    public double NudgeElapsed = -1;
    public bool Nudging;

    /// The breeze staggers each photo by a fraction of a second.
    public double PendingDelay = -1;
    public double PendingAmplitude;

    /// Hover lifts the card, holding presses it in.
    public double TargetScale() => Pressed ? 0.95 : Hovered ? 1.035 : 1;
}

/// <summary>
/// All motion in one place, driven by a single frame callback so the breeze,
/// an arrival, a press and a hover compose on the same card instead of
/// fighting over it. Nothing here knows about WPF; it only advances numbers.
/// </summary>
public sealed class MotionController
{
    private const double NudgeRise = 0.3;

    /// How long the corner badge takes to fade and pop in. The original uses
    /// 0.18 s; a real pointer on this screen read that as a flicker, so the
    /// prototype settled on 0.26 s.
    private const double BadgeFade = 0.26;

    private double _revealY;
    private double _revealVel;

    /// Where the whole line sits: 0 is down, negative is tucked above the edge.
    public double RevealY => _revealY;
    public double RevealVel => _revealVel;
    public bool RevealActive { get; private set; }
    public bool Retracting { get; private set; }
    public double RetractElapsed { get; private set; } = -1;

    private double _revealFrom;
    private readonly Dictionary<Guid, CardMotion> _cards = [];

    public double PanelHeight { get; set; } = 210;

    public CardMotion Get(Guid id) =>
        _cards.TryGetValue(id, out var m) ? m : (_cards[id] = new CardMotion());

    public CardMotion this[PeggedItem item] => Get(item.Id);

    public void Forget(Guid id) => _cards.Remove(id);

    public IReadOnlyDictionary<Guid, CardMotion> Cards => _cards;

    /// The line slides down from under the top edge, the way an auto-hiding Dock does.
    public void Reveal()
    {
        Retracting = false;
        RetractElapsed = -1;
        RevealActive = true;
    }

    public void Retract()
    {
        Retracting = true;
        RetractElapsed = 0;
        _revealFrom = _revealY;
        RevealActive = false;
        _revealVel = 0;
    }

    public void SnapTo(double y)
    {
        _revealY = y;
        _revealVel = 0;
        RevealActive = false;
    }

    /// The whole line is off the top of the screen when tucked away.
    public double RetractedY => -(PanelHeight + Layout.RetractExtra);

    public void Arrive(Guid id)
    {
        CardMotion m = Get(id);
        m.ArrivalY = -46;
        m.ArrivalVel = 0;
        m.Arriving = true;
        m.Swing = 16;
        m.SwingVel = 0;
    }

    /// A capture that flew in is already in place; the flight did the arriving.
    public void SkipArrival(Guid id)
    {
        CardMotion m = Get(id);
        m.Arriving = false;
        m.ArrivalY = 0;
        m.ArrivalVel = 0;
    }

    public void Nudge(Guid id, double degrees)
    {
        CardMotion m = Get(id);
        m.NudgeFrom = m.Swing;
        m.NudgeTarget = degrees;
        m.NudgeElapsed = 0;
        m.Nudging = true;
    }

    public void Gust(double minDelay, double maxDelay, double minAmp, double maxAmp, Random rng)
    {
        foreach (CardMotion m in _cards.Values)
        {
            m.PendingDelay = minDelay + rng.NextDouble() * (maxDelay - minDelay);
            m.PendingAmplitude = minAmp + rng.NextDouble() * (maxAmp - minAmp);
        }
    }

    public void Hover(Guid id, bool on) => Get(id).Hovered = on;

    public void Press(Guid id, bool on)
    {
        CardMotion m = Get(id);
        m.Pressed = on;
        if (!on) m.ScaleVel = 0;
    }

    /// Advance everything. Returns false when nothing moved, so the caller can
    /// stop asking for frames and leave the GPU alone.
    public bool Tick(double dt)
    {
        bool busy = false;

        if (Retracting)
        {
            RetractElapsed += dt;
            double k = System.Math.Min(1, RetractElapsed / 0.22);
            _revealY = Num.Lerp(_revealFrom, RetractedY, Ease.InQuad(k));
            if (k >= 1) { Retracting = false; RetractElapsed = -1; _revealY = RetractedY; }
            else busy = true;
        }
        else if (RevealActive)
        {
            Spring.StepResponse(ref _revealY, ref _revealVel, 0, dt, 0.42, 0.82);
            if (Spring.IsSettled(_revealY, _revealVel, 0, 0.05, 0.5))
            {
                _revealY = 0;
                _revealVel = 0;
                RevealActive = false;
            }
            else busy = true;
        }

        foreach (CardMotion m in _cards.Values)
        {
            if (m.Arriving)
            {
                Spring.StepResponse(ref m.ArrivalY, ref m.ArrivalVel, 0, dt, 0.42, 0.72);
                if (Spring.IsSettled(m.ArrivalY, m.ArrivalVel, 0, 0.05, 0.5))
                {
                    m.ArrivalY = 0;
                    m.Arriving = false;
                }
                else busy = true;
            }

            if (m.PendingDelay >= 0)
            {
                m.PendingDelay -= dt;
                busy = true;
                if (m.PendingDelay < 0)
                {
                    m.PendingDelay = -1;
                    m.NudgeFrom = m.Swing;
                    m.NudgeTarget = m.PendingAmplitude;
                    m.NudgeElapsed = 0;
                    m.Nudging = true;
                }
            }

            if (m.Nudging)
            {
                m.NudgeElapsed += dt;
                double k = System.Math.Min(1, m.NudgeElapsed / NudgeRise);
                m.Swing = Num.Lerp(m.NudgeFrom, m.NudgeTarget, Ease.OutQuad(k));
                m.SwingVel = 0;
                if (k >= 1)
                {
                    m.Nudging = false;
                    m.NudgeElapsed = -1;
                }
                else busy = true;
            }
            else
            {
                Spring.Step(ref m.Swing, ref m.SwingVel, 0, dt, 38, 2.4);
                if (!Spring.IsSettled(m.Swing, m.SwingVel, 0, 0.004, 0.02)) busy = true;
                else { m.Swing = 0; m.SwingVel = 0; }
            }

            double target = m.TargetScale();
            if (m.Pressed)
            {
                // Holding presses the photo in slowly, so a long press feels
                // like it is building up to something.
                m.Scale = Num.Lerp(m.Scale, target, dt <= 0 ? 1 : System.Math.Min(1, dt / 0.45));
                busy = true;
                if (System.Math.Abs(m.Scale - target) < 0.001) m.Scale = target;
            }
            else
            {
                Spring.StepResponse(ref m.Scale, ref m.ScaleVel, target, dt, 0.3, 0.6);
                if (!Spring.IsSettled(m.Scale, m.ScaleVel, target, 0.0005, 0.01)) busy = true;
                else { m.Scale = target; m.ScaleVel = 0; }
            }

            double badgeTo = m.Hovered ? 1 : 0;
            if (m.Badge != badgeTo)
            {
                double step = dt / BadgeFade;
                m.Badge = badgeTo > m.Badge ? System.Math.Min(1, m.Badge + step) : System.Math.Max(0, m.Badge - step);
                busy = true;
            }
        }

        return busy;
    }

    /// The pointer can leave without a MouseLeave when the line hides itself,
    /// so hover has to be dropped explicitly or a badge keeps fading in on a
    /// card nobody is pointing at.
    public void ClearHovers()
    {
        foreach (CardMotion m in _cards.Values) m.Hovered = false;
    }
}
