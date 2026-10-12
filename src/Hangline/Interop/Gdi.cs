using System.IO;
using Drawing = System.Drawing;
using Imaging = System.Drawing.Imaging;

namespace Hangline.Interop;

/// <summary>
/// The only place screen pixels are grabbed. Kept small and GDI-based on
/// purpose: it needs no broker, no permission prompt and no media framework,
/// and it captures a rectangle in physical pixels, which is exactly what the
/// line needs to know where a capture came from.
/// </summary>
public static class Gdi
{
    /// CAPTUREBLT leaves out windows that say they should not be captured —
    /// which is what a protected video player does, and what the line itself is.
    /// Named values, not a hand-rolled int: CopyFromScreen validates the enum and
    /// rejects the same bits spelled as a number.
    private static readonly Drawing.CopyPixelOperation WithBlt =
        Drawing.CopyPixelOperation.SourceCopy | Drawing.CopyPixelOperation.CaptureBlt;

    public static byte[]? Capture(Hangline.Core.RectD physical)
    {
        int x = (int)Math.Round(physical.X);
        int y = (int)Math.Round(physical.Y);
        int width = (int)Math.Round(physical.Width);
        int height = (int)Math.Round(physical.Height);

        if (width <= 0 || height <= 0) return null;

        try
        {
            using var bmp = new Drawing.Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb);
            using (var g = Drawing.Graphics.FromImage(bmp))
            {
                try
                {
                    g.CopyFromScreen(x, y, 0, 0, new Drawing.Size(width, height), WithBlt);
                }
                catch (ArgumentException)
                {
                    // Some stacks refuse CAPTUREBLT; a plain grab is better than no capture.
                    g.CopyFromScreen(x, y, 0, 0, new Drawing.Size(width, height),
                        Drawing.CopyPixelOperation.SourceCopy);
                }
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Shell.Log.Write($"capture failed: {ex.Message}");
            return null;
        }
    }

    public static Hangline.Core.RectD VirtualScreen()
    {
        int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        return new Hangline.Core.RectD(x, y,
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));
    }
}
