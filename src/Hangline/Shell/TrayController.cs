using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace Hangline.Shell;

/// <summary>
/// The tray icon: the app's only always-visible control surface. Owns the icon
/// and the menu's lifecycle; the menu's contents are built by
/// <see cref="MenuBuilder"/> and its actions live in <see cref="AppServices"/>.
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly AppServices _app;
    private readonly Forms.NotifyIcon _icon = new();

    public TrayController(AppServices app) => _app = app;

    public void Show()
    {
        _icon.Icon = IconGen.TrayIcon(_app.Settings.ClipboardEnabled);
        // A truncated tip is how Windows shows a long one; keep it short.
        _icon.Text = "Hangline";
        _icon.ContextMenuStrip = MenuBuilder.Build(_app);
        _icon.DoubleClick += (_, _) => _app.ToggleLine();
        _icon.Visible = true;
    }

    /// The pin fills up when copied images are being hung, like the original's
    /// shirt filling its icon.
    public void RefreshIcon()
    {
        Drawing.Icon? previous = _icon.Icon;
        _icon.Icon = IconGen.TrayIcon(_app.Settings.ClipboardEnabled);
        previous?.Dispose();
    }

    /// The menu reflects live state (how many photos hang), so rebuild it on open.
    public void RebuildMenu() => _icon.ContextMenuStrip = MenuBuilder.Build(_app);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
