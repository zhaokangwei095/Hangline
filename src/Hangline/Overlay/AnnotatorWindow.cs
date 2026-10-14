using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hangline.Core;
using Hangline.Interop;
using Hangline.Shell;
using Localization = Hangline.Shell.Localization;

namespace Hangline.Overlay;

/// <summary>
/// The in-app markup editor, opened by holding a photo.
/// <para>
/// macOS hands the file to the system Markup extension and is told when the
/// user saves. Windows offers no such partner, so the line draws its own: five
/// tools, four colours, undo, and a save that overwrites the file atomically so
/// the card on the line can swap its picture in place — the same closed loop the
/// original has, built here instead of borrowed.
/// </para>
/// </summary>
public sealed class AnnotatorWindow : Window
{
    private static readonly uint[] Colors = [0xFFFF3B30, 0xFFFFCC00, 0xFF34C759, 0xFF1E90FF];

    private readonly AnnotationDoc _doc = new();
    private readonly BitmapSource _image;
    private readonly BitmapSource? _pixelated;
    private readonly AnnotationSurface _surface;
    private readonly TextBox _typing = NewTyping();

    private AnnotationTool _tool = AnnotationTool.Rectangle;
    private uint _color = Colors[0];
    private Stroke? _live;

    public AnnotatorWindow(string path)
    {
        Path = path;
        _image = Load(path);
        _pixelated = Pixelate(_image);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = new SolidColorBrush(Color.FromArgb(240, 18, 18, 20));
        Topmost = true;

        var monitor = Dpi.MonitorAt(Dpi.CursorPhysical()) ?? Dpi.Monitors().FirstOrDefault();
        if (monitor.Handle != IntPtr.Zero)
        {
            Left = monitor.Bounds.X / monitor.Scale;
            Top = monitor.Bounds.Y / monitor.Scale;
            Width = monitor.Bounds.Width / monitor.Scale;
            Height = monitor.Bounds.Height / monitor.Scale;
        }
        else
        {
            WindowState = WindowState.Maximized;
        }

        _surface = new AnnotationSurface(_doc, _image, _pixelated);
        _surface.HorizontalAlignment = HorizontalAlignment.Center;
        _surface.VerticalAlignment = VerticalAlignment.Center;
        _surface.Margin = new Thickness(40, 32, 40, 96);

        var root = new Grid();
        root.Children.Add(_surface);
        root.Children.Add(BuildToolbar());
        root.Children.Add(_typing);
        Content = root;

        _surface.MouseLeftButtonDown += OnDown;
        _surface.MouseMove += OnMove;
        _surface.MouseLeftButtonUp += OnUp;
        KeyDown += OnKey;
        _typing.KeyDown += OnTypingKey;
        _typing.LostKeyboardFocus += (_, _) => CommitText();

        Loaded += (_, _) => LayoutSurface();
        SizeChanged += (_, _) => LayoutSurface();
    }

    public string Path { get; }
    public bool Saved { get; private set; }

    // MARK: - tools

    private static TextBox NewTyping() => new()
    {
        Visibility = Visibility.Collapsed,
        Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontSize = 18,
        MinWidth = 120,
    };

    private FrameworkElement BuildToolbar()
    {
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 26),
        };

        void Tool(AnnotationTool tool, string label)
        {
            var button = Chip(label);
            button.Click += (_, _) =>
            {
                _tool = tool;
                _surface.Cursor = Cursors.Cross;
            };
            bar.Children.Add(button);
        }

        Tool(AnnotationTool.Rectangle, Localization.T("Rectangle"));
        Tool(AnnotationTool.Arrow, Localization.T("Arrow"));
        Tool(AnnotationTool.Pen, Localization.T("Pen"));
        Tool(AnnotationTool.Text, Localization.T("Text"));
        Tool(AnnotationTool.Mosaic, Localization.T("Mosaic"));

        bar.Children.Add(new Border { Width = 14 });

        foreach (uint argb in Colors)
        {
            uint captured = argb;
            var swatch = new Button
            {
                Content = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb((byte)(captured >> 16), (byte)(captured >> 8), (byte)captured)),
                },
                Style = ChipStyle(),
                Margin = new Thickness(3, 0, 3, 0),
            };
            swatch.Click += (_, _) => _color = captured;
            bar.Children.Add(swatch);
        }

        bar.Children.Add(new Border { Width = 14 });

        Button undo = Chip(Localization.T("Undo"));
        undo.Click += (_, _) => { if (_doc.Undo()) _surface.InvalidateVisual(); };
        bar.Children.Add(undo);

        Button redo = Chip(Localization.T("Redo"));
        redo.Click += (_, _) => { if (_doc.Redo()) _surface.InvalidateVisual(); };
        bar.Children.Add(redo);

        bar.Children.Add(new Border { Width = 14 });

        Button cancel = Chip(Localization.T("Cancel"));
        cancel.Click += (_, _) => Close();
        bar.Children.Add(cancel);

        Button save = Chip(Localization.T("Save"));
        save.Click += (_, _) => Save();
        bar.Children.Add(save);

        return bar;
    }

    private static Button Chip(string text) => new()
    {
        Content = text,
        Style = ChipStyle(),
        Margin = new Thickness(3, 0, 3, 0),
    };

    private static Style ChipStyle()
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(150, 60, 62, 68))));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(11, 6, 11, 6)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        return style;
    }

    // MARK: - drawing

    private void LayoutSurface()
    {
        double availW = Math.Max(80, ActualWidth - 80);
        double availH = Math.Max(80, ActualHeight - 128);
        double fit = Math.Min(availW / _image.PixelWidth, availH / _image.PixelHeight);
        _surface.Fit = fit;
        _surface.Width = _image.PixelWidth * fit;
        _surface.Height = _image.PixelHeight * fit;
        _surface.InvalidateVisual();
    }

    private Pt ToPixels(MouseEventArgs e)
    {
        Point p = e.GetPosition(_surface);
        double fit = Math.Max(0.0001, _surface.Fit);
        return new Pt(p.X / fit, p.Y / fit);
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _surface.Focus();
        Pt at = ToPixels(e);

        if (_tool == AnnotationTool.Text)
        {
            BeginText(at);
            return;
        }

        _doc.PushState();
        _live = new Stroke { Tool = _tool, Color = _color, Thickness = _tool == AnnotationTool.Mosaic ? 10 : 6 };
        _live.Points.Add(at);
        // A rectangle and an arrow are two corners, so the press already has
        // both: OnMove then replaces the trailing one. With a single point
        // neither DrawRect nor DrawArrow would ever draw anything.
        if (_tool is AnnotationTool.Rectangle or AnnotationTool.Arrow)
            _live.Points.Add(at);
        _doc.Add(_live);
        _surface.CaptureMouse();
        _surface.InvalidateVisual();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_live is null) return;
        Pt at = ToPixels(e);

        // A rectangle or arrow is two corners; a freehand band accumulates.
        if (_live.Tool is AnnotationTool.Pen or AnnotationTool.Mosaic)
            _live.Points.Add(at);
        else
            _live.Points[^1] = at;

        _surface.InvalidateVisual();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_live is null) return;
        _live = null;
        _surface.ReleaseMouseCapture();
        _surface.InvalidateVisual();
    }

    private void BeginText(Pt at)
    {
        _typing.Tag = at;
        _typing.Text = string.Empty;
        _typing.Visibility = Visibility.Visible;
        Canvas.SetLeft(_typing, _surface.Margin.Left + at.X * _surface.Fit);
        Canvas.SetTop(_typing, _surface.Margin.Top + at.Y * _surface.Fit);
        _typing.Focus();
    }

    private void OnTypingKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitText(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _typing.Visibility = Visibility.Collapsed; e.Handled = true; }
    }

    private void CommitText()
    {
        if (_typing.Visibility != Visibility.Visible) return;
        _typing.Visibility = Visibility.Collapsed;
        string text = _typing.Text.Trim();
        if (text.Length == 0 || _typing.Tag is not Pt at) return;

        _doc.PushState();
        var stroke = new Stroke { Tool = AnnotationTool.Text, Color = _color, Text = text, FontSize = 44 };
        stroke.Points.Add(at);
        _doc.Add(stroke);
        _surface.InvalidateVisual();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Enter:
                Save();
                break;
            case Key.Z when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                if (_doc.Undo()) _surface.InvalidateVisual();
                break;
            case Key.Y when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                if (_doc.Redo()) _surface.InvalidateVisual();
                break;
        }
    }

    /// Used by the command-line self-test: one rectangle, one arrow, one band of
    /// mosaic, so the save path is exercised with every kind of mark.
    public void SelfTest()
    {
        double w = _image.PixelWidth, h = _image.PixelHeight;

        var box = new Stroke { Tool = AnnotationTool.Rectangle, Color = Colors[0], Thickness = Math.Max(4, w / 240) };
        box.Points.Add(new Pt(w * 0.1, h * 0.1));
        box.Points.Add(new Pt(w * 0.45, h * 0.4));
        _doc.Add(box);

        var arrow = new Stroke { Tool = AnnotationTool.Arrow, Color = Colors[1], Thickness = Math.Max(4, w / 240) };
        arrow.Points.Add(new Pt(w * 0.5, h * 0.45));
        arrow.Points.Add(new Pt(w * 0.85, h * 0.8));
        _doc.Add(arrow);

        var mosaic = new Stroke { Tool = AnnotationTool.Mosaic, Color = Colors[3], Thickness = Math.Max(4, w / 240) };
        for (int i = 0; i <= 12; i++)
            mosaic.Points.Add(new Pt(w * (0.12 + 0.05 * i), h * 0.62));
        _doc.Add(mosaic);

        _surface.InvalidateVisual();
    }

    // MARK: - saving

    /// Render at the file's own pixel size, so an annotated 4K screenshot stays 4K.
    public void Save()
    {
        int width = _image.PixelWidth;
        int height = _image.PixelHeight;

        try
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
                _doc.Render(dc, _image, width, height, _pixelated);

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var ms = new MemoryStream();
            encoder.Save(ms);

            if (!FileOps.WriteAtomic(Path, ms.ToArray())) return;
            Saved = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error("annotate save", ex);
        }
    }

    private static BitmapSource Load(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// Down then up with nearest neighbour: one pixelated copy per editing
    /// session, reused by every mosaic stroke.
    private static BitmapSource? Pixelate(BitmapSource source)
    {
        try
        {
            int blocks = 90;
            double sx = (double)blocks / source.PixelWidth;
            var small = new TransformedBitmap(source, new ScaleTransform(sx, sx));
            var back = new TransformedBitmap(small, new ScaleTransform(1 / sx, 1 / sx));
            RenderOptions.SetBitmapScalingMode(back, BitmapScalingMode.NearestNeighbor);
            back.Freeze();
            return back;
        }
        catch (Exception ex)
        {
            Log.Write($"pixelate: {ex.Message}");
            return null;
        }
    }
}

/// <summary>The picture plus its marks, drawn in image pixels and scaled to fit.</summary>
public sealed class AnnotationSurface(AnnotationDoc doc, BitmapSource image, BitmapSource? pixelated) : FrameworkElement
{
    public double Fit { get; set; } = 1;

    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(Fit, Fit));
        doc.Render(dc, image, image.PixelWidth, image.PixelHeight, pixelated);
        dc.Pop();
    }
}
