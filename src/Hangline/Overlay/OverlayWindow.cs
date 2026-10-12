using System.Windows;
using System.Windows.Media;
using Hangline.Core;
using Hangline.Interop;

namespace Hangline.Overlay;

/// <summary>
/// A full-screen, click-through layer used only while something is moving
/// across the screen — a capture flying to the line, or a photo falling off it.
/// <para>
/// It exists because the line itself is a strip about 210 points tall: a photo
/// that fell 520 points would be clipped by the bottom of that strip long
/// before the fall read as a fall. The strip stays small for the ninety per
/// cent of the time nothing is moving; this window carries the cost only during
/// the three quarters of a second it is needed.
/// </para>
/// </summary>
public sealed class OverlayWindow : Window
{
    public MonitorInfo Monitor { get; }
    public FlightLayer Layer { get; } = new();

    public OverlayWindow(MonitorInfo monitor)
    {
        Monitor = monitor;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        Content = Layer;

        Left = monitor.Bounds.X / monitor.Scale;
        Top = monitor.Bounds.Y / monitor.Scale;
        Width = monitor.Bounds.Width / monitor.Scale;
        Height = monitor.Bounds.Height / monitor.Scale;

        // Styles go on once the handle exists; showing is the caller's job,
        // because Show() called from inside SourceInitialized re-enters WPF's
        // own window creation and leaves the window built but never displayed.
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        WindowStyles.MakeToolStrip(hwnd);
        WindowStyles.SetClickThrough(hwnd, true);
    }

    public void Present()
    {
        Show();
        // WPF repositions on first show; the monitor rect is authoritative.
        Left = Monitor.Bounds.X / Monitor.Scale;
        Top = Monitor.Bounds.Y / Monitor.Scale;
        Width = Monitor.Bounds.Width / Monitor.Scale;
        Height = Monitor.Bounds.Height / Monitor.Scale;
    }

    /// Screen pixels to this window's drawing units.
    public RectD ToOwn(RectD physical) => new(
        (physical.X - Monitor.Bounds.X) / Monitor.Scale,
        (physical.Y - Monitor.Bounds.Y) / Monitor.Scale,
        physical.Width / Monitor.Scale,
        physical.Height / Monitor.Scale);
}

/// <summary>Holds whatever the director is currently animating and redraws it.</summary>
public sealed class FlightLayer : FrameworkElement
{
    public IReadOnlyList<Flight> Flights = [];

    public FlightLayer()
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        foreach (Flight flight in Flights)
            FlightRenderer.Draw(dc, flight);
    }
}
