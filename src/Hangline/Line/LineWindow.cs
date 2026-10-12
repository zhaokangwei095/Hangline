using System.Windows;
using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using Hangline.Core;
using Hangline.Input;
using Hangline.Interop;
using Hangline.Overlay;
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
    private readonly FlightDirector _flights = new();
    private readonly DispatcherTimer _breeze = new();

    private Passthrough? _passthrough;
    private IntPtr _hwnd;
    private MonitorInfo _monitor;
    private double _scale = 1;
    private bool _placed;
    private double _lastTrace;
    private static readonly bool HitTrace = Environment.GetEnvironmentVariable("HANGLINE_HIT") == "1";

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
        _reveal.Blocked = () => BlockedByFullScreen;
        _reveal.LiveCount = () => _model.LiveCount;
        _poller.Ticked += OnPoll;

        _model.Falling += OnFalling;

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            WindowStyles.MakeToolStrip(_hwnd);
            _passthrough = new Passthrough(_hwnd);
            _passthrough.ForceThrough();
            Place(MonitorAt(Dpi.CursorPhysical()));
        };

        _surface.NeedsFrames += StartFrames;
        _surface.CardCreated += (view, item) => _ = new CardGestures(view, this);
        // A card marks its own wheel events handled, so what reaches the window
        // came from a gap.
        MouseWheel += OnGapWheel;

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

    public bool RevealOnEdge { get => _reveal.RevealOnEdge; set => _reveal.RevealOnEdge = value; }
    public bool RevealOnPeek { get => _reveal.RevealOnPeek; set => _reveal.RevealOnPeek = value; }

    /// A game or a presentation owns the screen; the line keeps out of it unless
    /// the user asked for the opposite.
    public bool ShowOverFullScreen { get; set; }

    private bool BlockedByFullScreen =>
        !ShowOverFullScreen && FullscreenProbe.CoversScreen(_monitor.Bounds);

    public bool Revealed => _model.Revealed;
    public int LiveCount => _model.LiveCount;
    public LineModel Model => _model;
    public MotionController Motion => _motion;

    /// A gesture changed something that only a frame loop will finish.
    public void StartMotion() => StartFrames();

    public void RefreshAfterGesture()
    {
        _surface.ApplyMotions();
        _surface.InvalidateVisual();
        StartFrames();
    }

    /// A capture that knows where it was cut from flies from there to its slot;
    /// anything else simply appears on the line.
    public void FlyIn(PeggedItem item)
    {
        if (item.CaptureRect is not { } from || !_flights.CanFly || !_model.Revealed)
        {
            Land(item);
            return;
        }

        RectD? target = CardRectPhysical(item.Id);
        if (target is null)
        {
            Land(item);
            return;
        }

        _flights.Start(_monitor, from, target.Value, item, item.Tilt, falling: false, () => Land(item));
    }

    private void Land(PeggedItem item)
    {
        _model.Land(item.Id);
        _motion.SkipArrival(item.Id);
        _motion.Nudge(item.Id, 2.2);
        StartFrames();
    }

    /// The fall is drawn across the whole screen: a card dropped inside the strip
    /// would be cut off after 210 points instead of completing its 520.
    private void OnFalling(PeggedItem item)
    {
        _motion.Forget(item.Id);

        if (!_model.Revealed || CardRectPhysical(item.Id) is not { } rect) return;

        var copy = new PeggedItem { Path = item.Path, Thumb = item.Thumb, NaturalSize = item.NaturalSize };
        _flights.Start(_monitor, rect, rect, copy, item.Tilt, falling: true);
    }

    /// A card's clickable area in screen pixels, for the overlay to aim at.
    public RectD? CardRectPhysical(Guid id)
    {
        Rect? dip = _surface.CardRect(id);
        if (dip is null) return null;
        return new RectD(
            _monitor.Bounds.X + dip.Value.X * _scale,
            _monitor.Bounds.Y + dip.Value.Y * _scale,
            dip.Value.Width * _scale,
            dip.Value.Height * _scale);
    }

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

        // WPF keeps its own book of where the window is and rewrites the real
        // window rect from it on the next layout pass — a SetWindowPos here is
        // quietly undone moments later. So placement goes through WPF's units
        // and only the z-order is forced natively.
        Left = monitor.Bounds.X / _scale;
        Top = monitor.Bounds.Y / _scale;
        Width = monitor.Bounds.Width / _scale;
        Height = panelHeight;

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        _surface.Resize(monitor.Bounds.Width / _scale, panelHeight);
        // How many photos fit is a property of this screen, so it is decided
        // here and nowhere else; the layout and the scroll limit both read it.
        _model.Visible = Layout.VisibleFits(monitor.Bounds.Width / _scale, _model.Size);
        _motion.PanelHeight = panelHeight;
        _placed = true;

        if (!_model.Revealed)
        {
            // The controller has to agree that the line is tucked away. It
            // starts at zero, which would make the first reveal of a session
            // snap down instead of sliding.
            _motion.SnapTo(_motion.RetractedY);
            _surface.SetRevealOffset(_motion.RetractedY);
        }
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

        // The band is click-through, so no mouse event ever reaches this window
        // there; sampling the button is the only way to learn about the click.
        if (inBand && ButtonPressed()) _reveal.ClickedInBand();

        _reveal.Tick(_poller.Now, dt, inBand, inside);

        if (!_model.Revealed)
        {
            _passthrough?.ForceThrough();
            return;
        }

        // While the line is moving there is nothing reliable to click, and
        // flipping the window style every frame would flicker.
        bool settled = !_motion.RevealActive && !_motion.Retracting;

        var rects = settled ? _surface.CardRects() : EmptyRects;

        if (HitTrace && _poller.Now - _lastTrace > 2)
        {
            _lastTrace = _poller.Now;
            var first = rects.Count > 0 ? rects.Values.First().ToString() : "none";
            Shell.Log.Write($"hit: revealed={_model.Revealed} settled={settled} rects={rects.Count} " +
                            $"cursor={cursorPhysical.X},{cursorPhysical.Y} scale={_scale} first={first} " +
                            $"through={_passthrough?.IsThrough}");
        }
        _passthrough?.Apply(rects, cursorPhysical, _scale,
                           new Pt(window.Left, window.Top), forced: !settled,
                           scrollBand: _model.Overflows ? Band(rects) : null);
    }

    private static readonly Dictionary<Guid, Rect> EmptyRects = [];

    /// The vertical extent the photos hang in, in surface coordinates. Only the
    /// height of it matters: along an overflowing line every gap takes the wheel.
    private static (double Top, double Bottom)? Band(IReadOnlyDictionary<Guid, Rect> rects)
    {
        if (rects.Count == 0) return null;
        double top = double.MaxValue, bottom = double.MinValue;
        foreach (Rect r in rects.Values)
        {
            top = Math.Min(top, r.Y);
            bottom = Math.Max(bottom, r.Bottom);
        }
        return (top, bottom);
    }

    /// Bit 0 of GetAsyncKeyState means "went down since the previous call", so
    /// a click cannot be missed between two polls.
    private static bool ButtonPressed() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 1) != 0 ||
        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON) & 1) != 0;

    /// Scrolling started in a gap between cards. The cards handle their own
    /// wheel events and mark them handled, so this only sees the gaps.
    private void OnGapWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (!_model.Overflows) return;
        _model.ScrollBy(e.Delta < 0 ? 12 : -12);
        e.Handled = true;
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
        _flights.Dispose();
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
