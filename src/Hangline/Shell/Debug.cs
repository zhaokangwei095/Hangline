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

                case "--size" when i + 1 < args.Length:
                    app.SetLineSize(args[++i]);
                    break;
            }
        }
    }
}
