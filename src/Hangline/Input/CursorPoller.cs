using System.Diagnostics;
using System.Windows.Threading;
using Hangline.Core;
using Hangline.Interop;

namespace Hangline.Input;

/// <summary>
/// Where the pointer is, sampled on a timer rather than from mouse events —
/// the line's window is click-through most of the time, so it receives no
/// events at all. 30 Hz is what the original uses and it is enough: the dwell
/// that reveals the line is a quarter of a second.
/// </summary>
public sealed class CursorPoller
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _last;

    /// Seconds since the poller started, and the cursor in physical pixels.
    public event Action<double, Pt>? Ticked;

    public CursorPoller(double hertz = 30)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromSeconds(1 / Math.Max(1, hertz)),
        };
        _timer.Tick += (_, _) => Fire();
    }

    public bool IsRunning => _timer.IsEnabled;

    public double Now => _clock.Elapsed.TotalSeconds;

    public void Start()
    {
        if (_timer.IsEnabled) return;
        _last = _clock.Elapsed.TotalSeconds;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void Fire()
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = Math.Min(0.25, now - _last);
        _last = now;
        Ticked?.Invoke(dt, Dpi.CursorPhysical());
    }
}
