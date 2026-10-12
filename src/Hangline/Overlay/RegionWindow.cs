using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Hangline.Core;
using Hangline.Interop;

namespace Hangline.Overlay;

/// <summary>
/// The rubber band that picks a part of the screen to capture.
/// <para>
/// It dims everything except the rectangle being dragged, which is the one
/// behaviour that makes a region selection trustworthy: the user can see exactly
/// what will and will not be in the file.
/// </para>
/// </summary>
public sealed class RegionWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Rectangle _top = Shade(), _bottom = Shade(), _left = Shade(), _right = Shade();
    private readonly Rectangle _band = new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(120, 190, 255)),
        StrokeThickness = 1.5,
        Fill = Brushes.Transparent,
        StrokeDashArray = [4, 3],
    };
    private readonly TextBlock _readout = new()
    {
        Foreground = Brushes.White,
        FontSize = 12,
        Background = new SolidColorBrush(Color.FromArgb(190, 30, 30, 30)),
        Padding = new Thickness(5, 2, 5, 2),
        IsHitTestVisible = false,
    };

    private readonly double _scale;
    private readonly RectD _origin;
    private Pt _start;
    private bool _dragging;

    private RectD _selection;

    private RegionWindow(MonitorInfo monitor)
    {
        _scale = monitor.Scale;
        _origin = monitor.Bounds;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = Cursors.Cross;
        Background = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0));
        AllowsTransparency = false;
        Focusable = true;

        Left = monitor.Bounds.X / monitor.Scale;
        Top = monitor.Bounds.Y / monitor.Scale;
        Width = monitor.Bounds.Width / monitor.Scale;
        Height = monitor.Bounds.Height / monitor.Scale;

        _canvas.Children.Add(_top);
        _canvas.Children.Add(_bottom);
        _canvas.Children.Add(_left);
        _canvas.Children.Add(_right);
        _canvas.Children.Add(_band);
        _canvas.Children.Add(_readout);
        Content = _canvas;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key is Key.Enter or Key.Space && _dragging is false) { DialogResult = true; }
        };
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
    }

    private static Rectangle Shade() => new() { Fill = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)) };

    /// WPF measures the pointer in DIPs; the capture wants physical pixels.
    private Pt At(MouseEventArgs e)
    {
        System.Windows.Point p = e.GetPosition(_canvas);
        return new Pt(p.X, p.Y);
    }

    /// The selected rectangle in physical screen pixels, or null if cancelled.
    public static RectD? Select(MonitorInfo monitor)
    {
        var window = new RegionWindow(monitor);
        Shell.Log.Write($"region: want {window.Left},{window.Top} {window.Width}x{window.Height}");
        bool? ok = window.ShowDialog();
        Shell.Log.Write($"region closed: result={ok}");
        return ok == true && window._selection.Width > 2 && window._selection.Height > 2
            ? window._selection
            : null;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        Shell.Log.Write($"region rendered: actual {ActualWidth}x{ActualHeight} at {Left},{Top}");
        base.OnContentRendered(e);
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = At(e);
        _dragging = true;
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Draw(RectD.FromPoints(_start, At(e)));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        RectD dip = RectD.FromPoints(_start, At(e));
        Draw(dip);
        _selection = ToPhysical(dip);
        DialogResult = true;
    }

    private RectD ToPhysical(RectD dip) => new(
        Math.Round(_origin.X + dip.X * _scale), Math.Round(_origin.Y + dip.Y * _scale),
        Math.Round(dip.Width * _scale), Math.Round(dip.Height * _scale));

    private void Draw(RectD dip)
    {
        double w = _canvas.ActualWidth, h = _canvas.ActualHeight;

        Place(_top, 0, 0, w, dip.Y);
        Place(_bottom, 0, dip.Bottom, w, Math.Max(0, h - dip.Bottom));
        Place(_left, 0, dip.Y, dip.X, dip.Height);
        Place(_right, dip.Right, dip.Y, Math.Max(0, w - dip.Right), dip.Height);

        Canvas.SetLeft(_band, dip.X);
        Canvas.SetTop(_band, dip.Y);
        _band.Width = Math.Max(0, dip.Width);
        _band.Height = Math.Max(0, dip.Height);

        // The readout speaks in pixels, because that is what the file will hold.
        RectD real = ToPhysical(dip);
        _readout.Text = $"{(int)real.Width} × {(int)real.Height}";
        Canvas.SetLeft(_readout, Math.Max(0, dip.X - 2));
        Canvas.SetTop(_readout, Math.Max(0, dip.Y - 22));
        _readout.Visibility = dip.Width > 0 ? Visibility.Visible : Visibility.Hidden;
    }

    private static void Place(Rectangle r, double x, double y, double width, double height)
    {
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        r.Width = Math.Max(0, width);
        r.Height = Math.Max(0, height);
    }
}
