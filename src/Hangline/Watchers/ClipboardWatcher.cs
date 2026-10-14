using System.Windows;
using System.Windows.Media;
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
/// Windows notifies rather than being polled, so this reacts instantly. Two
/// kinds of copy arrive: a bitmap (a screenshot tool, an image in a browser) and
/// a list of files from Explorer. A bitmap is written to the inbox and hung;
/// files are hung as they are, because they are already pictures on disk — and
/// the thumbnail bitmap Explorer hands out next to them is ignored, or every
/// file copy would also write a duplicate. Text is never a reason to refuse:
/// copying an image out of a browser, Word or a PDF always carries source text.
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
        bool files = Clipboard.ContainsFileDropList();
        bool text = Clipboard.ContainsText();
        bool hasImage = Clipboard.ContainsImage();

        // Only which formats arrived, never what was on the clipboard.
        Log.Write($"clipboard: files={files} text={text} image={hasImage}");

        if (files)
        {
            HangCopiedFiles();
            return;
        }

        if (!hasImage) return;

        byte[]? raw = ClipboardDibV5() ?? ClipboardPng() ?? DecodeToPng();
        if (raw is null || raw.LongLength == 0) return;
        Log.Write($"clipboard image: {raw.LongLength} bytes via " +
                  $"{(NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_DIBV5) ? "DIBV5" : Clipboard.ContainsData("PNG") ? "PNG" : "GetImage")}");

        // A screenshot tool that hands over a bitmap (WeChat's Alt+A) gives a
        // PNG with a transparent background. Keeping the transparency would
        // hang a picture of nothing, so it is composited onto white: that is
        // what the desktop behind the window looked like.
        byte[] png = FlattenOntoWhite(raw);
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

    /// <summary>
    /// Explorer's copy carries the files. The pictures among them hang as they
    /// are; anything else in the list is ignored, and the thumbnail bitmap that
    /// comes with the list is never read, so no duplicate is written.
    /// </summary>
    private void HangCopiedFiles()
    {
        int hung = 0;
        foreach (string path in Clipboard.GetFileDropList())
        {
            if (!FileKind.IsImage(path) || !File.Exists(path)) continue;
            ImageCopied?.Invoke(path);
            hung++;
        }
        Log.Write($"clipboard: hung {hung} copied file{(hung == 1 ? "" : "s")}");
    }

    /// <summary>
    /// The clipboard's own PNG bytes, when the copying app put a PNG there.
    /// WPF's <c>Clipboard.GetImage()</c> goes through CF_DIB, a 24-bit format
    /// with no transparency, so a screenshot with a transparent background
    /// arrives with its margins already flattened to black. CF_DIBV5 is the
    /// DIB flavour that does carry an alpha channel, so it is read first.
    /// </summary>
    private static byte[]? ClipboardDibV5()
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_DIBV5)) return null;
            if (!NativeMethods.OpenClipboard(IntPtr.Zero)) return null;

            handle = NativeMethods.GetClipboardData(NativeMethods.CF_DIBV5);
            if (handle == IntPtr.Zero) return null;

            IntPtr data = NativeMethods.GlobalLock(handle);
            if (data == IntPtr.Zero) return null;
            try
            {
                ulong size = NativeMethods.GlobalSize(handle).ToUInt64();
                if (size < 124) return null;         // smaller than a v5 header

                var buffer = new byte[size];
                System.Runtime.InteropServices.Marshal.Copy(data, buffer, 0, (int)size);
                return DibV5ToPng(buffer);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"clipboard dibv5: {ex.GetType().Name} {ex.Message}");
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero) NativeMethods.CloseClipboard();
        }
    }

    /// <summary>
    /// Turns a CF_DIBV5 blob into PNG bytes. The header is a BITMAPV5HEADER;
    /// only the fields that matter for a screenshot are read: width, height,
    /// bit count and, when present, the alpha mask that says which pixels are
    /// transparent. Rows are bottom-up and padded to four bytes.
    /// </summary>
    private static byte[]? DibV5ToPng(byte[] dib)
    {
        int width = BitConverter.ToInt32(dib, 4);
        int height = BitConverter.ToInt32(dib, 8);
        short bits = BitConverter.ToInt16(dib, 14);
        uint compression = BitConverter.ToUInt32(dib, 16);
        // BITMAPV5HEADER: the colour and alpha masks sit at 40/44/48/52.
        uint redMask = BitConverter.ToUInt32(dib, 40);
        uint greenMask = BitConverter.ToUInt32(dib, 44);
        uint blueMask = BitConverter.ToUInt32(dib, 48);
        uint alphaMask = BitConverter.ToUInt32(dib, 52);

        // BI_RGB is the only compression a screenshot tool writes; anything
        // else means this reader does not apply.
        if (width <= 0 || height <= 0 || bits != 32 || compression != 0) return null;

        int absH = Math.Abs(height);
        int stride = width * 4;
        int header = 124;                            // sizeof(BITMAPV5HEADER)
        int expected = header + stride * absH;
        if (dib.Length < expected) return null;

        var pixels = new byte[stride * absH];
        // A positive height means the rows are bottom-up.
        bool bottomUp = height > 0;
        for (int row = 0; row < absH; row++)
        {
            int from = header + row * stride;
            int to = (bottomUp ? absH - 1 - row : row) * stride;
            System.Buffer.BlockCopy(dib, from, pixels, to, stride);
        }

        // An alpha mask of zero means the high byte is unused padding rather
        // than opacity, and there is nothing to preserve.
        PixelFormat format = alphaMask != 0 ? PixelFormats.Bgra32 : PixelFormats.Bgr32;

        var image = BitmapSource.Create(width, absH, 96, 96, format, null, pixels, stride);
        image.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
    /// <summary>
    /// <c>Clipboard.GetImage()</c> goes through CF_DIB, which is a 24-bit
    /// format with no transparency, so a screenshot with a transparent
    /// background (WeChat's Alt+A is the one that does this) would arrive
    /// with its margins already flattened to black.
    /// </summary>
    private static byte[]? ClipboardPng()
    {
        try
        {
            // WPF's DataFormats has no constant for it; the format is
            // registered under the plain name "PNG".
            const string Png = "PNG";
            if (!Clipboard.ContainsData(Png)) return null;
            object? raw = Clipboard.GetData(Png);
            if (raw is MemoryStream ms && ms.Length > 0) return ms.ToArray();
            if (raw is byte[] bytes && bytes.Length > 0) return bytes;
            return null;
        }
        catch (Exception ex)
        {
            Log.Write($"clipboard png: {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Composites a PNG's transparency onto white, returning the PNG bytes.
    /// A screenshot with no transparency is returned untouched.
    /// </summary>
    private static byte[] FlattenOntoWhite(byte[] png)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(png);
            image.EndInit();

            if (image.Format == PixelFormats.Bgr24 || image.Format == PixelFormats.Rgb24)
                return png;                     // already opaque

            var material = new WriteableBitmap(image);
            int w = material.PixelWidth, h = material.PixelHeight;
            int stride = w * 4;
            var pixels = new byte[stride * h];
            material.CopyPixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);

            bool any = false;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] == 255) continue;
                any = true;
                break;
            }
            if (!any) return png;                // nothing to flatten

            // PBGRA is premultiplied, so a transparent pixel stores 0 in every
            // channel: what was the desktop has already been multiplied away.
            // Undoing the premultiply and compositing over white puts it back.
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte a = pixels[i + 3];
                if (a == 255) continue;
                double un = a == 0 ? 0 : 255.0 / a;
                double back = 255.0 - a;
                pixels[i] = (byte)System.Math.Min(255, pixels[i] * un + back);
                pixels[i + 1] = (byte)System.Math.Min(255, pixels[i + 1] * un + back);
                pixels[i + 2] = (byte)System.Math.Min(255, pixels[i + 2] * un + back);
                pixels[i + 3] = 255;
            }

            var opaque = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(opaque));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            // A picture that cannot be decoded is better shown as it is than
            // not hung at all.
            Log.Write($"clipboard flatten: {ex.GetType().Name} {ex.Message}");
            return png;
        }
    }

    /// <summary>
    /// The fallback for a clipboard that carries no PNG: decode whatever image
    /// WPF can get out of it and encode that. There is no alpha to lose, because
    /// a DIB never had any.
    /// </summary>
    private static byte[]? DecodeToPng()
    {
        try
        {
            BitmapSource? image = Clipboard.GetImage();
            if (image is null) return null;

            long pixels = (long)image.PixelWidth * image.PixelHeight;
            if (pixels is <= 0 or > MaxPixels) return null;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Log.Write($"clipboard decode: {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _settle.Stop();
        if (_watching) NativeMethods.RemoveClipboardFormatListener(_sink.Handle);
        _watching = false;
    }
}
