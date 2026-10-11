using System.Windows;
using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using Hangline.Core;
using Hangline.Input;
using Hangline.Interop;
using Hangline.Shell;

namespace Hangline.Line;

/// <summary>
/// The strip along the top of the screen that the line hangs from.
/// <para>
/// It is transparent, topmost, never activated and never in Alt-Tab, and it is
/// <b>not present at all</b> while the line is tucked away: an invisible window
/// sitting across the top of every screen shows up in screen readers and
/// automation tools, and would be found sooner or later.
/// </para>
/// <para>
/// Placement goes through SetWindowPos in physical pixels rather than WPF's
/// Left/Top, because on a mixed-DPI desktop those are in the DIPs of whichever
/// monitor the window last touched, which is how a line ends up a pixel below
/// the edge it is supposed to hang from.
/// </para>
/// </summary>
public sealed class LineWindow : Window, ILineHost
{
    private readonly LineModel _model;
    private readonly MotionController _motion = new();
    private readonly LineSurface _surface;
    private readonly CursorPoller _poller = new();
    private readonly RevealController _reveal = new();
    private readonly StopwatchTimer _frames = new();
    private readonly DispatcherTimer _breeze = new();

    private Passthrough? _passthrough;
    private IntPtr _hwnd;
    private MonitorInfo _monitor;
    private double _scale = 1;
    private bool _placed;

    public LineWindow(LineModel model)
    {
        _model = model;
        _surface = new LineSurface(model, _motion);

        Content = _surface;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        IsTabStop = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        SizeToContent = SizeToContent.Manual;

        _reveal.Apply = ShowLine;
        _reveal.Busy = () => _model.PressedId is not null || _model.DraggingId is not null || MenuOpen;
        _reveal.LiveCount = () => _model.LiveCount;
        _poller.Ticked += OnPoll;

        _model.Falling += item => _motion.Forget(item.Id);

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            WindowStyles.MakeToolStrip(_hwnd);
            _passthrough = new Passthrough(_hwnd);
            _passthrough.ForceThrough();
            Place(MonitorAt(Dpi.CursorPhysical()));
        };

        // Hover is the only pointer state the line has before gestures exist.
        _surface.NeedsFrames += StartFrames;

        // A WPF window has no handle until it is shown, but the line must be
        // placed — and therefore measurable — long before the first reveal.
        _hwnd = new WindowInteropHelper(this).EnsureHandle();

        RescheduleBreeze();
        _breeze.Tick += (_, _) =>
        {
            Gust();
            RescheduleBreeze();
        };
        _breeze.Start();
    }

    /// Set while a context menu is up, so the line does not slide away under it.
    public bool MenuOpen { get; set; }

    public bool Revealed => _model.Revealed;
    public int LiveCount => _model.LiveCount;
    public LineModel Model => _model;

    public void ShowAt(double x, double y) => Place(MonitorAt(new Pt(x, y)));

    // MARK: - ILineHost

    public void Toggle() => _reveal.Toggle();

    public void Reveal() => _reveal.Force(true);

    public void Retract() => _reveal.Force(false);

    public void Peek() => _reveal.Peek(_poller.Now);

    public void TakeEverythingDown() => _model.TakeEverythingDown();

    /// A development handle: hold the line down so it can be looked at.
    public void PinLine(bool on)
    {
        _reveal.Pinned = on;
        if (on) ShowLine(true);
    }

    // MARK: - breeze

    private void RescheduleBreeze() =>
        _breeze.Interval = TimeSpan.FromSeconds(7 + Random.Shared.NextDouble() * 9);

    /// Every so often a little wind moves the line. It is the detail that makes
    /// it feel like an object and not a widget.
    private void Gust()
    {
        if (_model.LiveCount == 0 || _model.DraggingId is not null) return;
        _motion.Gust(0, 0.35, 1.6, 3.4, Random.Shared);
        StartFrames();
    }

    public void ApplyLayoutChanges()
    {
        _model.ReloadThumbnails();
        Place(_monitor.Handle == IntPtr.Zero ? MonitorAt(Dpi.CursorPhysical()) : _monitor);
        _model.TrimOverCapacity();
    }

    // MARK: - placement

    private MonitorInfo MonitorAt(Pt physical) => Dpi.MonitorAt(physical) ?? Dpi.Monitors().FirstOrDefault();

    private void Place(MonitorInfo monitor)
    {
        if (monitor.Handle == IntPtr.Zero) return;
        _monitor = monitor;
        _scale = monitor.Scale;

        double panelHeight = Layout.PanelHeight(_model.Size);
        int physicalHeight = (int)Math.Round(panelHeight * _scale);

        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST,
            (int)monitor.Bounds.X, (int)monitor.Bounds.Y,
            (int)monitor.Bounds.Width, physicalHeight,
            NativeMethods.SWP_NOACTIVATE);

        _surface.Resize(monitor.Bounds.Width / _scale, panelHeight);
        _motion.PanelHeight = panelHeight;
        _placed = true;

        if (!_model.Revealed) _surface.SetRevealOffset(_motion.RetractedY);
    }

    private RectD WindowRectPhysical() => new(_monitor.Bounds.X, _monitor.Bounds.Y,
        _monitor.Bounds.Width, Layout.PanelHeight(_model.Size) * _scale);

    // MARK: - reveal

    private void ShowLine(bool reveal)
    {
        _model.Revealed = reveal;

        if (reveal)
        {
            if (!_placed) Place(MonitorAt(Dpi.CursorPhysical()));
            if (!IsVisible) Show();
            // WPF moves and sizes a window itself the first time it is shown,
            // which throws away the placement above. Ours is authoritative.
            Place(_monitor);
            _motion.Reveal();
            StartFrames();
        }
        else
        {
            _motion.Retract();
            StartFrames();
        }
    }

    // MARK: - per-frame

    private void OnPoll(double dt, Pt cursorPhysical)
    {
        RectD window = WindowRectPhysical();
        bool inBand = cursorPhysical.Y <= _monitor.Bounds.Y + RevealController.BandDepth
                      && cursorPhysical.X >= window.Left && cursorPhysical.X < window.Right;
        bool inside = window.Contains(cursorPhysical);

        _reveal.Tick(_poller.Now, dt, inBand, inside);

        if (!_model.Revealed)
        {
            _passthrough?.ForceThrough();
            return;
        }

        // While the line is moving there is nothing reliable to click, and
        // flipping the window style every frame would flicker.
        bool settled = !_motion.RevealActive && !_motion.Retracting;
        _passthrough?.Apply(settled ? _surface.CardRects() : new Dictionary<Guid, Rect>(),
                           cursorPhysical, _scale,
                           new Pt(window.Left, window.Top), forced: !settled);
    }

    private void StartFrames()
    {
        if (_frames.Running) return;
        _frames.Start(OnFrame);
    }

    private void OnFrame(double dt)
    {
        bool busy = _motion.Tick(dt);

        _surface.SetRevealOffset(_motion.RevealY);
        _surface.RelayoutVertical();
        _surface.ApplyMotions();

        if (!busy)
        {
            if (!_model.Revealed) Hide();
            _frames.Stop();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _poller.Stop();
        _frames.Stop();
        base.OnClosed(e);
    }

    /// The poller runs always; the frame loop only while something is moving.
    public void StartPolling() => _poller.Start();
}

/// <summary>A frame loop that stops itself when nothing is moving.</summary>
internal sealed class StopwatchTimer
{
    private TimeSpan _last;
    private double _lastSeconds;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    public bool Running { get; private set; }

    public void Start(Action<double> onFrame)
    {
        if (Running) return;
        Running = true;
        _last = TimeSpan.Zero;
        CompositionTarget.Rendering += Handler;

        void Handler(object? sender, EventArgs e)
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = _last == TimeSpan.Zero ? 1.0 / 60 : Math.Min(0.05, now - _lastSeconds);
            _last = _clock.Elapsed;
            _lastSeconds = now;
            onFrame(dt);
            if (!Running) CompositionTarget.Rendering -= Handler;
        }
    }

    public void Stop() => Running = false;
}
