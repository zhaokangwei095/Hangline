using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hangline.Capture;
using Hangline.Core;
using Hangline.Interop;
using Hangline.Shell;

namespace Hangline.Watchers;

/// <summary>
/// Hangs images the user copies.
/// <para>
/// Windows notifies rather than being polled, so this reacts instantly. What it
/// refuses: a copy that carries <b>files</b> is Explorer moving a file (Explorer
/// also hands out a thumbnail bitmap, so "there is an image" is not enough), and
/// a copy with no image at all is text — which is what a password manager puts
/// there, and that must never become a file on disk. Text riding along with an
/// image is normal and is not a reason to refuse.
/// </para>
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private const long MaxBytes = 64L * 1024 * 1024;
    private const long MaxPixels = 80_000_000;

    /// Retries for the moment when the copying app still owns the clipboard.
    /// Short and few: the whole loop runs on the UI thread.
    private const int Tries = 4;
    private const int RetryMs = 50;

    /// How long to wait after the last notification before reading. The two or
    /// three notifications of one copy land within a few milliseconds of each
    /// other, so this only has to be longer than that gap — any longer and two
    /// deliberate copies in quick succession would collapse into one.
    private const int SettleMs = 100;

    private readonly MessageSink _sink;
    private readonly DispatcherTimer _settle;
    private byte[]? _lastBytes;
    private bool _watching;

    public event Action<string>? ImageCopied;

    public ClipboardWatcher(MessageSink sink)
    {
        _sink = sink;
        _sink.On(NativeMethods.WM_CLIPBOARDUPDATE, (_, _) => Settle());
        _settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SettleMs) };
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            Drain();
        };
    }

    /// <summary>
    /// One copy arrives as several notifications, and the first of them often
    /// lands while the previous owner still holds the data — reading then gets
    /// the last picture back, which the duplicate check throws away. Waiting a
    /// beat after the burst is what makes a copy land.
    /// </summary>
    private void Settle()
    {
        if (!_watching) return;
        _settle.Stop();
        _settle.Start();
    }

    private void Drain()
    {
        if (!_watching) return;

        // The application that copied often still holds the clipboard open when
        // the notification arrives, and opening it then fails outright. Trying
        // once means that copy is lost for good, which looks exactly like the
        // feature not working.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Read();
                return;
            }
            catch (Exception ex)
            {
                Log.Write($"clipboard: {ex.GetType().Name} {ex.Message}");
                if (attempt >= Tries) return;
                Thread.Sleep(RetryMs);
            }
        }
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

    private void Read()
    {
        // Text or files riding along means the copy was not "this image".
        bool files = Clipboard.ContainsFileDropList();
        bool text = Clipboard.ContainsText();
        bool hasImage = Clipboard.ContainsImage();

        // Only which formats arrived, never what was on the clipboard.
        Log.Write($"clipboard: files={files} text={text} image={hasImage}");

        // Files riding along is a copy made in Explorer, which also hands out
        // a thumbnail bitmap — that is a file being moved, not a picture to
        // keep. Text is not a reason to refuse: copying an image out of a
        // browser, Word or a PDF always carries the source text too, so
        // refusing on text threw away the case the feature exists for.
        if (files || !hasImage) return;

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
        _settle.Stop();
        if (_watching) NativeMethods.RemoveClipboardFormatListener(_sink.Handle);
        _watching = false;
    }
}
