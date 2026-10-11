using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hangline.Core;

namespace Hangline.Line;

/// <summary>
/// The one decoded image each card keeps in memory: a card at twice its point
/// size, and no more — every photo on the line holds one.
/// <para>
/// Decoding goes through WIC so EXIF orientation is applied by the codec, and
/// the size a card lays itself out with is read off the same decoded object, so
/// a photo can never be measured one way and drawn another.
/// </para>
/// </summary>
public static class ThumbLoader
{
    public readonly record struct Decoded(ImageSource Image, SizeD Size);

    public static double MaxPixels(LineSize size) => 320 * Layout.ScaleOf(size);

    public static Decoded? Load(string path, LineSize size)
    {
        try
        {
            double cap = MaxPixels(size);
            using var stream = OpenRead(path);
            BitmapSource frame = BitmapDecoder.Create(
                stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad).Frames[0];

            double longest = Math.Max(frame.PixelWidth, frame.PixelHeight);
            if (longest <= 0) return null;

            double shrink = Math.Min(1, cap / longest);
            BitmapSource image = frame;

            if (shrink < 1)
            {
                var scaled = new TransformedBitmap(frame, new ScaleTransform(shrink, shrink));
                scaled.Freeze();
                image = scaled;
            }
            else
            {
                frame.Freeze();
            }

            return new Decoded(image, new SizeD(image.PixelWidth, image.PixelHeight));
        }
        catch (Exception ex)
        {
            Shell.Log.Write($"thumb failed {Path.GetFileName(path)}: {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    /// ReadWrite sharing: a screenshot still being written by another app, or
    /// open in Paint, must never throw here.
    private static FileStream OpenRead(string path) => new(
        path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
