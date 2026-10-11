using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hangline.Core;
using Hangline.Shell;

namespace Hangline.Line;

/// <summary>
/// The rope and the cards hanging off it. Lays the cards out from
/// <see cref="Layout"/>, keeps one <see cref="CardView"/> per item, and slides
/// the whole thing down from under the top edge as one transform.
/// </summary>
public sealed class LineSurface : Canvas
{
    private readonly LineModel _model;
    private readonly MotionController _motion;
    private readonly Dictionary<Guid, CardView> _views = [];
    private readonly TranslateTransform _shift = new();

    /// Set HANGLINE_LAYOUT=1 to have the first card's geometry written to the log.
    private static readonly bool LayoutTrace =
        Environment.GetEnvironmentVariable("HANGLINE_LAYOUT") is "1" or "true";

    private double _width = 1280;
    private double _height = 210;

    /// Something started moving that only a frame loop will finish.
    public event Action? NeedsFrames;

    public LineSurface(LineModel model, MotionController motion)
    {
        _model = model;
        _motion = motion;
        RenderTransform = _shift;
        _model.Changed += Reconcile;
    }

    public double PanelWidth => _width;

    /// The line spans the screen, so its width decides the rope's sag.
    public void Resize(double width, double height)
    {
        if (Math.Abs(_width - width) < 0.01 && Math.Abs(_height - height) < 0.01) return;
        _width = width;
        _height = height;
        _motion.PanelHeight = height;
        Reconcile();
        InvalidateVisual();
    }

    public void SetRevealOffset(double y) => _shift.Y = y;

    /// The clickable area of every card, in surface coordinates.
    public IReadOnlyDictionary<Guid, Rect> CardRects()
    {
        var map = new Dictionary<Guid, Rect>();
        foreach ((Guid id, CardView view) in _views)
        {
            if (_model.Find(id) is { IsFalling: true }) continue;
            map[id] = view.HitRect(_model.Size);
        }
        return map;
    }

    public Rect? CardRect(Guid id) => _views.TryGetValue(id, out var v) ? v.HitRect(_model.Size) : null;

    public void ApplyMotions()
    {
        foreach ((Guid id, CardView view) in _views)
        {
            if (_model.Find(id) is null) continue;
            view.ApplyMotion(_model.Size);
        }
    }

    // MARK: - layout

    private void Reconcile()
    {
        var live = new HashSet<Guid>();

        foreach (PeggedItem item in _model.Items)
        {
            live.Add(item.Id);
            if (!_views.TryGetValue(item.Id, out CardView? view))
            {
                view = new CardView(item)
                {
                    LineSize = _model.Size,
                    InInbox = _model.IsInInbox(item.Id),
                };
                WireHover(view, item.Id);
                _views[item.Id] = view;
                Children.Add(view);
                _motion.Arrive(item.Id);
            }
            view.LineSize = _model.Size;
        }

        foreach (Guid id in _views.Keys.ToList())
        {
            if (live.Contains(id)) continue;
            Children.Remove(_views[id]);
            _views.Remove(id);
            _motion.Forget(id);
        }

        LayoutCards();
    }

    private void WireHover(CardView view, Guid id)
    {
        view.MouseEnter += (_, _) =>
        {
            _motion.Hover(id, true);
            NeedsFrames?.Invoke();
        };
        view.MouseLeave += (_, _) =>
        {
            _motion.Hover(id, false);
            NeedsFrames?.Invoke();
        };
    }

    private void LayoutCards()
    {
        double spacing = Layout.Spacing(_model.Size);
        IReadOnlyList<PeggedItem> items = _model.Items;
        int count = items.Count;

        for (int index = 0; index < count; index++)
        {
            PeggedItem item = items[index];
            if (!_views.TryGetValue(item.Id, out CardView? view) || view is null) continue;

            double x = Layout.CardX(index, count, _width, spacing, _model.Visible, _model.Scroll, _model.KeepOnLine);
            double ropeY = Layout.RopeY(x, _width);
            double height = _height - ropeY;

            view.RopeY = ropeY;
            view.EdgeFade = Layout.EdgeFade(x, _width);
            view.Width = Layout.CardWidth(_model.Size);
            view.Height = Math.Max(1, height);

            SetLeft(view, x - view.Width / 2);
            SetTop(view, Layout.PinTopY(ropeY) + _motion.Get(item.Id).ArrivalY);

            if (LayoutTrace && index == 0)
            {
                Shell.Log.Write($"layout count={count} width={_width:F1} h={_height:F1} " +
                    $"x={x:F1} ropeY={ropeY:F1} left={Canvas.GetLeft(view):F1} top={Canvas.GetTop(view):F1} " +
                    $"size={view.Width}x{view.Height} desired={view.DesiredSize.Width:F0}x{view.DesiredSize.Height:F0} " +
                    $"op={view.Opacity:F2} vis={view.IsVisible} natural={item.NaturalSize}");
            }
        }

        InvalidateVisual();
    }

    /// Called once per frame while the cards are moving.
    public void RelayoutVertical() => LayoutCards();

    protected override void OnRender(DrawingContext dc)
    {
        RopeLayer.Draw(dc, _width);
        if (_model.LiveCount == 0) DrawHint(dc);
    }

    /// An empty line still says what it is for.
    private void DrawHint(DrawingContext dc)
    {
        string text = Shell.Localization.T("Take a screenshot and it will hang here");
        var typeface = new Typeface(new FontFamily("Microsoft YaHei UI, Segoe UI"),
            FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

        var formatted = new FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 12,
            new SolidColorBrush(Color.FromArgb(190, 60, 62, 68)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double cx = _width / 2;
        double cy = Layout.RopeY(cx, _width) + 34;
        var size = new Size(formatted.Width + 24, formatted.Height + 12);
        var pill = new Rect(cx - size.Width / 2, cy - size.Height / 2, size.Width, size.Height);

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(150, 246, 247, 249)), null,
            pill, size.Height / 2, size.Height / 2);
        dc.DrawText(formatted, new Point(pill.X + 12, pill.Y + 6));
    }
}
