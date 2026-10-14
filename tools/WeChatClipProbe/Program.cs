using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Puts a bitmap with a real alpha channel on the clipboard, the way WeChat's
/// Alt+A hands one over: a picture with a transparent background rather than a
/// file. Used to check that what hangs on the line is opaque.
/// </summary>
internal static class Program
{
    [System.STAThread]
    private static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "wechat-like.png");

        // --read samples a file that already exists, so the same command can
        // check the copy Hangline wrote after flattening.
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
        // WeChat puts both: the PNG (which keeps the alpha) and a bitmap for
        // apps that only ask for pixels.
        data.SetData("PNG", new MemoryStream(bytes));
        data.SetData(DataFormats.Bitmap, image);
        Clipboard.SetDataObject(data, true);

        // Which formats are actually on the clipboard now, so a mismatch
        // between this probe and the real thing is visible immediately.
        var here = Clipboard.GetDataObject();
        if (here is not null)
        {
            System.Console.WriteLine("formats on the clipboard:");
            foreach (string f in here.GetFormats())
                System.Console.WriteLine("  " + f);
        }

        System.Console.WriteLine($"on the clipboard: {image.PixelWidth}x{image.PixelHeight} {image.Format}");
        System.Console.WriteLine($"written: {path} ({new FileInfo(path).Length} bytes)");
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
        // The self-test draws a red rectangle along (0.1,0.1)-(0.45,0.4), a
        // yellow arrow from (0.5,0.45) to (0.85,0.8) and a blue mosaic band at
        // y=0.62. Sampling those spots says whether anything was drawn.
        samples = [.. samples,
            ((int)(pw * 0.1), (int)(ph * 0.1)),
            ((int)(pw * 0.45), (int)(ph * 0.4)),
            ((int)(pw * 0.5), (int)(ph * 0.45)),
            ((int)(pw * 0.85), (int)(ph * 0.8)),
            ((int)(pw * 0.3), (int)(ph * 0.62)) ];
        foreach ((int x, int y) in samples)
        {
            int i = y * pitch + x * 4;
            System.Console.WriteLine($"  pixel({x},{y}) = {buf[i]},{buf[i + 1]},{buf[i + 2]},{buf[i + 3]}");
        }
    }
}
