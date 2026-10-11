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

                case "--hang" when i + 1 < args.Length:
                    app.Model.Hang(args[++i]);
                    app.LineWindow?.Peek();
                    break;

                case "--cursor" when i + 1 < args.Length:
                    // x,y in physical pixels
                    string[] parts = args[++i].Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                        NativeMethods.SetCursorPos(x, y);
                    break;

                case "--size" when i + 1 < args.Length:
                    app.SetLineSize(args[++i]);
                    break;
            }
        }
    }
}
