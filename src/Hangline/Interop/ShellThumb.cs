using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Imaging = System.Drawing.Imaging;

namespace Hangline.Interop;

/// <summary>
/// The first frame of a screen recording, taken the way Explorer takes it.
/// <para>
/// Hangline does not link a media framework for this: <c>IShellItemImageFactory</c>
/// asks the shell, which already knows how to decode whatever container the user's
/// recorder produced and caches the answer. If it ever fails, the recording simply
/// does not hang rather than showing a broken card.
/// </para>
/// </summary>
public static class ShellThumb
{
    private static readonly Guid FactoryInterface = new("bcc18b79-ba13-4422-9045-7fe877b4e593");

    public static BitmapSource? Load(string path, int maxPixels)
    {
        IntPtr item = IntPtr.Zero;
        IntPtr hbitmap = IntPtr.Zero;

        try
        {
            Guid riid = FactoryInterface;
            if (NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref riid, out item) != 0)
                return null;

            var factory = (IShellItemImageFactory)Marshal.GetTypedObjectForIUnknown(
                item, typeof(IShellItemImageFactory));

            int size = Math.Max(16, maxPixels);
            factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_THUMBNAILONLY, out hbitmap);
            if (hbitmap == IntPtr.Zero)
            {
                // Some codecs only answer the non-thumbnail request; ask again.
                factory.GetImage(new SIZE { cx = size, cy = size }, 0, out hbitmap);
            }
            if (hbitmap == IntPtr.Zero) return null;

            return ToSource(hbitmap);
        }
        catch (Exception ex)
        {
            Shell.Log.Write($"shell thumb {Path.GetFileName(path)}: {ex.Message}");
            return null;
        }
        finally
        {
            if (hbitmap != IntPtr.Zero) NativeMethods.DeleteObject(hbitmap);
            if (item != IntPtr.Zero) Marshal.Release(item);
        }
    }

    /// The shell hands back a 32bpp DIB section, which WPF refuses to wrap
    /// directly — so it goes through a PNG round trip in memory.
    private static BitmapSource? ToSource(IntPtr hbitmap)
    {
        try
        {
            using var bitmap = Drawing.Bitmap.FromHbitmap(hbitmap);
            using var ms = new MemoryStream();
            bitmap.Save(ms, Imaging.ImageFormat.Png);
            ms.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Shell.Log.Write($"hbitmap: {ex.Message}");
            return null;
        }
    }

    private const uint SIIGBF_THUMBNAILONLY = 0x2;

}

[ComImport]
[Guid("bcc18b79-ba13-4422-9045-7fe877b4e593")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
    void GetImage(SIZE size, uint flags, out IntPtr phbm);
}

[StructLayout(LayoutKind.Sequential)]
internal struct SIZE
{
    public int cx;
    public int cy;
}
