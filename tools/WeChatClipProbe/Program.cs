using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Puts a bitmap with a real alpha channel on the clipboard, the way a
/// screenshot tool hands one over: a picture with a transparent background
/// rather than a file. Used to check that what hangs on the line is opaque.
/// <para>
/// Two shapes, because there are two ways a tool can do this:
/// <c>--png</c> also puts a PNG on the clipboard (which keeps the alpha),
/// and the default puts only a bitmap, which is where WPF's
/// <c>Clipboard.GetImage()</c> loses it.
/// </para>
/// </summary>
internal static class Program
{
    [System.STAThread]
    private static int Main(string[] args)
    {
        string path = args.Length > 0 && !args[0].StartsWith("--")
            ? args[0]
            : Path.Combine(Path.GetTempPath(), "wechat-like.png");

        if (args.Contains("--read"))
        {
            Sample(path);
            return 0;
        }

        const int w = 600, h = 380;
        using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.Transparent);
            // Opaque content that reads as "a screenshot".
            g.FillRectangle(System.Drawing.Brushes.WhiteSmoke, 20, 20, 560, 340);
            g.FillRectangle(System.Drawing.Brushes.RoyalBlue, 40, 40, 220, 28);
            g.FillRectangle(System.Drawing.Brushes.DimGray, 40, 90, 520, 8);
            g.FillRectangle(System.Drawing.Brushes.IndianRed, 60, 120, 480, 120);
            g.FillRectangle(System.Drawing.Brushes.SeaGreen, 60, 260, 300, 60);
        }
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);

        // Decoded by WIC so the clipboard object is a BitmapSource with an
        // alpha channel, exactly what a real copy leaves on the pasteboard.
        byte[] bytes = File.ReadAllBytes(path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(bytes);
        image.EndInit();
        image.Freeze();

        var data = new DataObject();
        if (args.Contains("--png"))
            data.SetData("PNG", new MemoryStream(bytes));   // keeps the alpha
        data.SetData(DataFormats.Bitmap, image);
        Clipboard.SetDataObject(data, true);

        // --dibv5 also writes the CF_DIBV5 format, which is what a screenshot
        // tool that preserves transparency actually leaves behind. WPF's
        // DataObject has no such format, so it goes in through Win32.
        if (args.Contains("--dibv5")) PutDibV5(bmp);

        var here = Clipboard.GetDataObject();
        System.Console.WriteLine($"formats: {string.Join(", ", here?.GetFormats() ?? [])}");
        System.Console.WriteLine($"source: {image.PixelWidth}x{image.PixelHeight} {image.Format}, {bytes.Length} bytes");
        Sample(path);
        return 0;
    }

    /// <summary>
    /// Four pixels, so the alpha channel can be read rather than guessed: a
    /// copy that was never flattened reads 0,0,0,0 at the corners.
    /// </summary>
    private static void Sample(string path)
    {
        var probe = new BitmapImage();
        probe.BeginInit();
        probe.CacheOption = BitmapCacheOption.OnLoad;
        probe.UriSource = new Uri(path);
        probe.EndInit();

        var wb = new WriteableBitmap(probe);
        int pw = wb.PixelWidth, ph = wb.PixelHeight, pitch = pw * 4;
        var buf = new byte[pitch * ph];
        wb.CopyPixels(new Int32Rect(0, 0, pw, ph), buf, pitch, 0);

        (int, int)[] samples = [ (0, 0), (pw / 2, ph / 2), (pw - 1, ph - 1), (30, 30) ];
        foreach ((int x, int y) in samples)
        {
            int i = y * pitch + x * 4;
            System.Console.WriteLine($"  pixel({x},{y}) = {buf[i]},{buf[i + 1]},{buf[i + 2]},{buf[i + 3]}");
        }
    }

    /// <summary>
    /// Writes a CF_DIBV5 blob into the clipboard through Win32, which is the
    /// only way to put that format there: WPF's DataObject has no DIB format.
    /// The header is a BITMAPV5HEADER with an alpha mask, so the reader has
    /// something real to work on.
    /// </summary>
    private static void PutDibV5(System.Drawing.Bitmap bmp)
    {
        const int header = 124;
        int w = bmp.Width, h = bmp.Height, stride = w * 4;

        // The bitmap is read bottom-up, the way a DIB stores its rows.
        var rows = new byte[stride * h];
        for (int row = 0; row < h; row++)
        {
            int to = (h - 1 - row) * stride;
            for (int x = 0; x < w; x++)
            {
                var c = bmp.GetPixel(x, row);
                int i = to + x * 4;
                rows[i] = c.B; rows[i + 1] = c.G; rows[i + 2] = c.R; rows[i + 3] = c.A;
            }
        }

        var dib = new byte[header + rows.Length];
        using var ms = new MemoryStream(dib);
        using var bw = new System.IO.BinaryWriter(ms);
        bw.Write(header);            // bV5Size
        bw.Write(w);                 // bV5Width
        bw.Write(h);                 // bV5Height (positive: bottom-up)
        bw.Write((short)1);          // bV5Planes
        bw.Write((short)32);         // bV5BitCount
        bw.Write(0);                 // bV5Compression: BI_RGB
        bw.Write(rows.Length);       // bV5SizeImage
        bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);   // pels per metre, clr used/important
        bw.Write(0x00FF0000u);       // bV5RedMask
        bw.Write(0x0000FF00u);       // bV5GreenMask
        bw.Write(0x000000FFu);       // bV5BlueMask
        bw.Write(0xFF000000u);       // bV5AlphaMask
        bw.Flush();
        System.Buffer.BlockCopy(rows, 0, dib, header, rows.Length);

        var hmem = System.Runtime.InteropServices.Marshal.AllocHGlobal(dib.Length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(dib, 0, hmem, dib.Length);
            if (!OpenClipboard(IntPtr.Zero)) { System.Console.WriteLine("could not open the clipboard"); return; }
            EmptyClipboard();
            // The clipboard owns the memory once SetClipboardData succeeds, so
            // it is not freed here — only on failure.
            IntPtr set = SetClipboardData(17, hmem);
            CloseClipboard();
            if (set == IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(hmem);
                System.Console.WriteLine("SetClipboardData(CF_DIBV5) refused");
            }
            else System.Console.WriteLine($"CF_DIBV5 on the clipboard: {w}x{h}, {dib.Length} bytes");
        }
        catch (Exception ex)
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(hmem);
            System.Console.WriteLine($"dibv5: {ex.GetType().Name} {ex.Message}");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr owner);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool CloseClipboard();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EmptyClipboard();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint format, IntPtr memory);
}
