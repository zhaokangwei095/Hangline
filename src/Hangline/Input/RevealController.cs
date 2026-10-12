using Hangline.Core;

namespace Hangline.Input;

/// <summary>
/// When the line should be down.
/// <para>
/// The original reveals on a dwell in the menu bar. Windows has no menu bar, so
/// the band is the top few pixels of the screen — which means the dwell matters
/// more than it did: passing through the top edge on the way to a window's close
/// button must not drop the line, only resting there may.
/// </para>
/// </summary>
public sealed class RevealController
{
    public const double HoverDelay = 0.25;
    public const double RetractDelay = 0.5;
    public const double PeekSeconds = 2.5;
    public const double EmptyRetract = 0.7;

    /// How deep the band at the top of the screen is, in physical pixels.
    public const int BandDepth = 6;

    private double _dwell;
    private double _away;
    private double _empty;
    private double _peekUntil = double.NegativeInfinity;
    private bool _manual;

    public bool Pinned { get; set; }

    /// A cascade that has to be watched — taking everything down — holds the
    /// line open past every reason it otherwise has to go back up.
    public bool HoldOpen;

    /// Ends a hold. Whatever brought the line down for the cascade was not the
    /// user asking for it to stay, so the manual flag goes with it.
    public void Release()
    {
        HoldOpen = false;
        _manual = false;
    }

    public bool RevealOnEdge { get; set; } = true;
    public bool RevealOnPeek { get; set; } = true;
    public Func<bool> Busy { get; set; } = () => false;

    /// Something else owns the screen; the line does not come down at all.
    public Func<bool> Blocked { get; set; } = () => false;
    public Func<int> LiveCount { get; set; } = () => 0;
    public Action<bool> Apply { get; set; } = _ => { };

    public bool Wanted { get; private set; }

    /// A click in the top band suppresses the reveal until the pointer leaves it,
    /// so dismissing a menu there does not immediately drop a line.
    public bool Suppressed { get; private set; }

    /// The original does this on any click in the menu bar: the line goes away
    /// and stays away until the pointer leaves the band, so closing a menu at
    /// the top of the screen is not answered by a line dropping down again.
    public void ClickedInBand()
    {
        Suppressed = true;
        _dwell = 0;
        _manual = false;
        Want(false);
    }

    public void Toggle()
    {
        _manual = !Wanted;
        Apply(_manual);
    }

    /// A new capture: show the line briefly, whatever else is going on.
    public void Peek(double now)
    {
        if (!RevealOnPeek) return;
        _peekUntil = now + PeekSeconds;
        Want(true);
    }

    public void Force(bool on)
    {
        _manual = on;
        Want(on);
    }

    public void Tick(double now, double dt, bool inBand, bool insidePanel)
    {
        if (Blocked())
        {
            _dwell = 0;
            Want(false);
            return;
        }

        if (Pinned)
        {
            Want(true);
            return;
        }

        if (HoldOpen)
        {
            Want(true);
            return;
        }

        if (!inBand)
        {
            _dwell = 0;
            if (Suppressed) Suppressed = false;
        }
        else if (!Suppressed && RevealOnEdge)
        {
            _dwell += dt;
            if (_dwell >= HoverDelay) { _manual = false; Want(true); }
        }

        bool inZone = inBand || insidePanel;

        if (!Wanted)
        {
            _away = 0;
            _empty = 0;
            return;
        }

        if (now < _peekUntil) return;

        if (Busy())
        {
            _away = 0;
            return;
        }

        if (inZone)
        {
            _away = 0;
            // An empty line does not linger once you stop looking at it.
            if (LiveCount() == 0 && !_manual)
            {
                _empty += dt;
                if (_empty >= EmptyRetract) Want(false);
            }
            else _empty = 0;
            return;
        }

        _empty = 0;
        if (_manual) return;

        _away += dt;
        if (_away >= RetractDelay) Want(false);
    }

    private void Want(bool on)
    {
        if (Wanted == on) return;
        Wanted = on;
        _away = 0;
        _empty = 0;
        Apply(on);
    }
}
