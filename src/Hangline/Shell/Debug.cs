using System.Windows;
using Hangline.Interop;
using Hangline.Core;

namespace Hangline.Shell;

/// <summary>
/// Development-only handles, so the overlay can be looked at and measured
/// without a person moving a mouse. Nothing here is reachable from the UI, and
/// every path needs an explicit command-line argument.
/// <para>
/// <c>cursor</c> moves the real pointer, which takes the mouse away from the
/// user — it is only ever run deliberately, from a script.
/// </para>
/// </summary>
public static class Debug
{
    private static void AnnotateSelfTest(string path)
    {
        var editor = new Overlay.AnnotatorWindow(path);
        editor.Show();
        editor.SelfTest();
        editor.Save();
        Log.Write($"annotate self-test wrote {path}");
    }

    public static void Handle(string[] args, AppServices app)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pin":
                    app.LineWindow?.Reveal();
                    if (app.LineWindow is { } pinned) pinned.PinLine(true);
                    break;

                // --hang path            hangs a file in place
                // --hang path@x,y,w,h    also says where it was captured, so it flies in
                case "--hang" when i + 1 < args.Length:
                {
                    string spec = args[++i];
                    RectD? captured = null;
                    int at = spec.LastIndexOf('@');
                    if (at > 0)
                    {
                        string[] dims = spec[(at + 1)..].Split(',');
                        if (dims.Length == 4 && dims.All(d => int.TryParse(d, out _)))
                        {
                            captured = new RectD(int.Parse(dims[0]), int.Parse(dims[1]),
                                                 int.Parse(dims[2]), int.Parse(dims[3]));
                        }
                        spec = spec[..at];
                    }
                    app.HangFile(spec, captured);
                    break;
                }

                case "--cursor" when i + 1 < args.Length:
                    // x,y in physical pixels
                    string[] parts = args[++i].Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                        NativeMethods.SetCursorPos(x, y);
                    break;

                case "--preview":
                    var live = app.Model.Live();
                    if (live.Count > 0)
                        Overlay.PreviewHost.Show(live, live.Count - 1);
                    break;

                case "--annotate" when i + 1 < args.Length:
                {
                    var editor = new Overlay.AnnotatorWindow(args[++i]);
                    editor.ContentRendered += (_, _) => Log.Write("annotator rendered");
                    editor.Closed += (_, _) => Log.Write("annotator closed");
                    editor.Show();
                    Log.Write($"annotator shown: visible={editor.IsVisible} size={editor.Width}x{editor.Height}");
                    break;
                }

                // Draw one of each mark and save it, so the render pipeline can
                // be checked without a hand on the mouse.
                case "--annotate-test" when i + 1 < args.Length:
                    AnnotateSelfTest(args[++i]);
                    break;

                // Spike: can the OS trim a recording at all, from an unpackaged
                // .NET 8 WPF app? Answer this before building a UI on top of it.
                case "--trim-test" when i + 2 < args.Length:
                {
                    string clip = args[++i];
                    int seconds = int.Parse(args[++i]);
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        bool ok = await Line.Trimmer.TryTrim(clip, TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
                        Log.Write(ok
                            ? $"TRIM ok via ffmpeg, {new System.IO.FileInfo(clip).Length} bytes"
                            : $"TRIM unavailable (ffmpeg found: {Line.Trimmer.Find() is not null})");
                    });
                    break;
                }

                case "--trim-window" when i + 1 < args.Length:
                    new Overlay.TrimWindow(args[++i]).Show();
                    break;

                case "--size" when i + 1 < args.Length:
                    app.SetLineSize(args[++i]);
                    break;
            }
        }
    }
}
