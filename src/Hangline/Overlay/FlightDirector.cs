using Hangline.Core;
using Hangline.Interop;
using Hangline.Line;

namespace Hangline.Overlay;

/// <summary>
/// Runs the captures flying in and the photos falling out.
/// <para>
/// At most two fly at once: a burst of twenty screenshots should not put twenty
/// animations on screen, and the original lets the rest land in place. The
/// overlay window only exists while something is actually moving across it.
/// </para>
/// </summary>
public sealed class FlightDirector
{
    private const int ConcurrentLimit = 2;

    /// HANGLINE_SLOWMO=6 makes an animation six times longer, so a 0.65 s flight
    /// can actually be caught in a screenshot while it is still in the air.
    private static readonly double Slowmo =
        double.TryParse(Environment.GetEnvironmentVariable("HANGLINE_SLOWMO"), out double v) && v > 1 ? v : 1;

    private readonly List<Flight> _flights = [];
    private readonly StopwatchTimer _frames = new();
    private OverlayWindow? _overlay;

    public bool CanFly => _flights.Count < ConcurrentLimit;

    /// <param name="fromPhysical">Where the photo is now, on screen.</param>
    /// <param name="toPhysical">Where it belongs on the line.</param>
    public void Start(MonitorInfo monitor, RectD fromPhysical, RectD toPhysical, PeggedItem? item,
                      double tilt, bool falling, Action? onLanded = null)
    {
        OverlayWindow overlay = EnsureOverlay(monitor);

        _flights.Add(new Flight
        {
            Item = item,
            From = overlay.ToOwn(fromPhysical),
            To = overlay.ToOwn(toPhysical),
            Tilt = tilt,
            Falling = falling,
            Duration = (falling ? 0.55 : 0.65) * Slowmo,
            OnLanded = onLanded,
        });

        _frames.Start(Tick);
    }

    private OverlayWindow EnsureOverlay(MonitorInfo monitor)
    {
        if (_overlay is not null && _overlay.Monitor.Handle == monitor.Handle) return _overlay;

        _overlay?.Close();
        _overlay = new OverlayWindow(monitor);
        _overlay.Present();
        return _overlay;
    }

    private void Tick(double dt)
    {
        bool busy = false;

        foreach (Flight flight in _flights.ToList())
        {
            flight.Elapsed += dt;
            if (flight.Elapsed >= flight.Duration)
            {
                _flights.Remove(flight);
                flight.Done = true;
                flight.OnLanded?.Invoke();
            }
            else
            {
                busy = true;
            }
        }

        if (_overlay is not null)
        {
            _overlay.Layer.Flights = _flights;
            _overlay.Layer.InvalidateVisual();
        }

        if (!busy)
        {
            _frames.Stop();
            _overlay?.Close();
            _overlay = null;
        }
    }

    public void Dispose()
    {
        _frames.Stop();
        _overlay?.Close();
        _overlay = null;
    }
}
