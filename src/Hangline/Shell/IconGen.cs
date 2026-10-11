using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Hangline.Shell;

/// <summary>
/// The tray icon is drawn here rather than shipped as an asset, like the
/// original draws everything in code. It is a clothespin gripping a rope —
/// deliberately not the original app's shirt, which is not under the MIT grant.
/// </summary>
public static class IconGen
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <param name="filled">Full pin means "hanging copied images" is on.</param>
    public static Icon TrayIcon(bool filled, int size = 32)
    {
        using var bmp = Render(size, filled);
        IntPtr handle = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(handle);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static Bitmap Render(int size, bool filled = false)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);

        float u = size / 32f;

        // The rope: a shallow sag across the icon, light enough to read on dark
        // and solid tray backgrounds alike.
        using (var rope = new Pen(Color.FromArgb(235, 235, 235), 1.8f * u))
        {
            rope.StartCap = LineCap.Round;
            rope.EndCap = LineCap.Round;
            g.DrawBezier(rope,
                new PointF(1.5f * u, 9.5f * u),
                new PointF(10f * u, 15f * u),
                new PointF(22f * u, 15f * u),
                new PointF(30.5f * u, 9.5f * u));
        }

        DrawPin(g, u, 16f * u, 11.5f * u, 8.4f * u, 17f * u, filled, -14f);
        if (size >= 24) DrawPin(g, u, 7.5f * u, 13f * u, 6f * u, 13f * u, filled, 12f);

        return bmp;
    }

    /// One pin: two legs that meet at the jaw, plus the metal band.
    private static void DrawPin(Graphics g, float u, float cx, float top, float width, float height,
                                bool filled, float angle)
    {
        var state = g.Save();
        g.TranslateTransform(cx, top);
        g.RotateTransform(angle);

        float w = width, h = height;
        var body = new GraphicsPath();
        float r = w * 0.42f;
        body.AddArc(-w / 2, 0, w, r * 2, 180, 180);
        body.AddArc(w / 2 - r * 2, 0, r * 2, r * 2, 0, 180);
        body.CloseFigure();

        using (var fill = new LinearGradientBrush(
                   new RectangleF(-w / 2, 0, w, h),
                   Color.FromArgb(filled ? 255 : 235, 245, 245, 245),
                   Color.FromArgb(filled ? 250 : 210, 210, 214, 220),
                   LinearGradientMode.Horizontal))
        using (var edge = new Pen(Color.FromArgb(200, 70, 74, 80), 0.9f * u))
        {
            g.FillPath(fill, body);
            g.DrawPath(edge, body);
        }

        // The slot the rope passes through.
        using (var slot = new SolidBrush(Color.FromArgb(120, 40, 42, 46)))
            g.FillRectangle(slot, -w * 0.28f, h * 0.16f, w * 0.56f, 1.6f * u);

        // The split between the two legs of the clip.
        using (var split = new Pen(Color.FromArgb(150, 90, 94, 100), 0.8f * u))
            g.DrawLine(split, 0, h * 0.42f, 0, h);

        g.Restore(state);
    }

    /// Writes a multi-size .ico so the published exe has a file icon too.
    public static void WriteIco(string path, int[] sizes)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        bw.Write((short)0);            // reserved
        bw.Write((short)1);            // type: icon
        bw.Write((short)sizes.Length);

        var images = new List<byte[]>();
        foreach (int size in sizes) images.Add(EncodePng(size));

        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            byte[] png = images[i];
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)0);         // colours
            bw.Write((byte)0);         // reserved
            bw.Write((short)1);        // planes
            bw.Write((short)32);       // bpp
            bw.Write(png.Length);
            bw.Write(offset);
            offset += png.Length;
        }
        foreach (byte[] png in images) bw.Write(png);
    }

    private static byte[] EncodePng(int size)
    {
        using var bmp = Render(size, filled: false);
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
}
