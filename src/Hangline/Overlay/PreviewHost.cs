using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hangline.Core;
using Hangline.Interop;
using Hangline.Shell;

namespace Hangline.Overlay;

/// <summary>
/// Quick Look: a full-screen look at a photo without opening anything.
/// <para>
/// macOS has <c>QLPreviewPanel</c> behind a force click. Windows has no
/// equivalent for a file that is not selected in Explorer, and PowerToys' Peek
/// cannot be summoned from another program, so the line carries its own: the
/// photo at its real aspect, <kbd>←</kbd> / <kbd>→</kbd> along the line,
/// <kbd>Esc</kbd> to put it back.
/// </para>
/// </summary>
public sealed class PreviewHost : Window
{
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _caption = new()
    {
        Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
        FontSize = 13,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new Thickness(0, 0, 0, 28),
    };

    private readonly IReadOnlyList<PeggedItem> _items;
    private int _index;

    private PreviewHost(IReadOnlyList<PeggedItem> items, int index)
    {
        _items = items;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = new SolidColorBrush(Color.FromArgb(232, 12, 12, 14));
        WindowStartupLocation = WindowStartupLocation.Manual;

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

        var grid = new Grid { Margin = new Thickness(48, 40, 48, 64) };
        grid.Children.Add(_image);
        grid.Children.Add(_caption);
        Content = grid;

        KeyDown += OnKey;
        MouseLeftButtonDown += (_, _) => Close();
        Show();
        Activate();
        Focus();
        ShowAt(index);
    }

    public static void Show(IReadOnlyList<PeggedItem> items, int index)
    {
        if (items.Count == 0) return;
        _ = new PreviewHost(items, Math.Clamp(index, 0, items.Count - 1));
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape or Key.Space or Key.Enter:
                Close();
                break;
            case Key.Left or Key.Up:
                ShowAt(_index - 1);
                break;
            case Key.Right or Key.Down:
                ShowAt(_index + 1);
                break;
        }
    }

    private void ShowAt(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        _index = index;
        PeggedItem item = _items[index];

        try
        {
            _image.Source = Load(item.Path);
        }
        catch (Exception ex)
        {
            Log.Write($"preview {item.Path}: {ex.Message}");
        }

        string name = Path.GetFileName(item.Path);
        _caption.Text = _items.Count > 1 ? $"{name}    {index + 1} / {_items.Count}" : name;
    }

    /// Decoded lazily and only as large as the screen needs: a 4K screenshot
    /// does not have to sit in memory at full size to be glanced at.
    private static BitmapSource Load(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
