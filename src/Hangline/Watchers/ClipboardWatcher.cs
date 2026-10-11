using System.Windows;
using System.Windows.Media.Imaging;
using Hangline.Capture;
using Hangline.Core;
using Hangline.Interop;
using Hangline.Shell;

namespace Hangline.Watchers;

/// <summary>
/// Hangs images the user copies.
/// <para>
/// Windows notifies rather than being polled, so this reacts instantly. What it
/// refuses matters more than what it takes: an image copied alongside text or
/// files is almost always a side effect of a copy the user did not mean for the
/// line, and anything a password manager put on the clipboard must never become
/// a file on disk.
/// </para>
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private const long MaxBytes = 64L * 1024 * 1024;
    private const long MaxPixels = 80_000_000;

    private readonly MessageSink _sink;
    private byte[]? _lastBytes;
    private bool _watching;

    public event Action<string>? ImageCopied;

    public ClipboardWatcher(MessageSink sink)
    {
        _sink = sink;
        _sink.On(NativeMethods.WM_CLIPBOARDUPDATE, (_, _) => OnUpdate());
    }

    public bool IsEnabled
    {
        get => _watching;
        set
        {
            if (value == _watching) return;
            _watching = value;
            if (value) NativeMethods.AddClipboardFormatListener(_sink.Handle);
            else NativeMethods.RemoveClipboardFormatListener(_sink.Handle);
        }
    }

    private void OnUpdate()
    {
        if (!_watching) return;

        try
        {
            // Text or files riding along means the copy was not "this image".
            if (Clipboard.ContainsFileDropList()) return;
            if (Clipboard.ContainsText()) return;
            if (!Clipboard.ContainsImage()) return;

            BitmapSource? image = Clipboard.GetImage();
            if (image is null) return;

            long pixels = (long)image.PixelWidth * image.PixelHeight;
            if (pixels is <= 0 or > MaxPixels) return;

            byte[] png = Png(image);
            if (png.LongLength > MaxBytes) return;
            if (_lastBytes is not null && _lastBytes.AsSpan().SequenceEqual(png)) return;
            _lastBytes = png;

            Inbox.Ensure();
            NameStyle style = NameStyle.For(Shell.Localization.IsChinese);
            string name = Naming.ClipboardName(DateTime.Now, style);
            string path = Naming.UniquePath(Inbox.Folder, name);
            File.WriteAllBytes(path, png);

            ImageCopied?.Invoke(path);
        }
        catch (Exception ex)
        {
            // The clipboard is often held open by whatever copied; failing here
            // must not look like a crash to the user.
            Log.Write($"clipboard: {ex.GetType().Name} {ex.Message}");
        }
    }

    private static byte[] Png(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_watching) NativeMethods.RemoveClipboardFormatListener(_sink.Handle);
        _watching = false;
    }
}
