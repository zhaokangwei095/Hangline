using System.Windows;
using System.Windows.Threading;
using Hangline.Capture;
using Hangline.Core;
using Hangline.Interop;
using Hangline.Overlay;
using Hangline.Watchers;

namespace Hangline.Shell;

/// <summary>
/// The ways something gets onto the line that are not the user dragging it:
/// watched folders, the clipboard, and Hangline's own capture shortcuts.
/// </summary>
public sealed partial class AppServices
{
    public MessageSink Sink { get; private set; } = null!;
    public HotKey ToggleKey { get; private set; } = null!;
    public HotKey RegionKey { get; private set; } = null!;
    public HotKey FullscreenKey { get; private set; } = null!;
    public HotKey WindowKey { get; private set; } = null!;
    public ClipboardWatcher Clipboards { get; private set; } = null!;

    /// Shift joins in because Ctrl+Alt+A is already WeChat's screenshot key on
    /// this machine, and stealing a hotkey silently is worse than a longer chord.
    public static Shortcut RegionDefault { get; } =
        new(0x41, HotKeyModifiers.Control | HotKeyModifiers.Alt | HotKeyModifiers.Shift, "A");
    public static Shortcut FullscreenDefault { get; } =
        new(0x46, HotKeyModifiers.Control | HotKeyModifiers.Alt | HotKeyModifiers.Shift, "F");
    public static Shortcut WindowDefault { get; } =
        new(0x57, HotKeyModifiers.Control | HotKeyModifiers.Alt | HotKeyModifiers.Shift, "W");

    private void StartupInputs()
    {
        Sink = new MessageSink();

        ToggleKey = new HotKey(Sink);
        ToggleKey.Pressed += () => Line?.Toggle();
        ToggleKey.TryRegister(Settings.ToShortcut());

        RegionKey = new HotKey(Sink);
        RegionKey.Pressed += CaptureRegion;
        RegionKey.TryRegister(RegionDefault);

        FullscreenKey = new HotKey(Sink);
        FullscreenKey.Pressed += CaptureFullscreen;
        FullscreenKey.TryRegister(FullscreenDefault);

        WindowKey = new HotKey(Sink);
        WindowKey.Pressed += CaptureWindow;
        WindowKey.TryRegister(WindowDefault);

        Clipboards = new ClipboardWatcher(Sink);
        Clipboards.ImageCopied += path => HangFile(path);
        Clipboards.IsEnabled = Settings.ClipboardEnabled;

        Watchers = new WatchManager(this);
    }

    /// A watched folder reported a new file, on a thread pool thread.
    public void OnWatchedFileAppeared(string path) => OnUi(() => HangFile(path));

    public void OnWatchedFolderChanged() => OnUi(() => Model.Prune());

    public PeggedItem? HangFile(string path, RectD? captured = null)
    {
        PeggedItem? item = Model.Hang(path, quietly: Settings.QuietHang);
        if (item is null) return null;
        item.CaptureRect = captured;
        LineWindow?.Peek();
        Tray.RebuildMenu();
        return item;
    }

    public void CaptureFullscreen()
    {
        if (!Settings.InboxEnabled) { Log.Write("capture skipped: inbox off"); }
        Capturer.Result? shot = Capturer.Fullscreen();
        if (shot is not null) HangFile(shot.Value.Path, shot.Value.Captured);
    }

    public void CaptureWindow()
    {
        Capturer.Result? shot = Capturer.ActiveWindow();
        if (shot is not null) HangFile(shot.Value.Path, shot.Value.Captured);
    }

    /// The selection covers the screen the pointer is on, which is the screen
    /// the user is about to cut a piece out of.
    public void CaptureRegion()
    {
        MonitorInfo? monitor = Dpi.MonitorAt(Dpi.CursorPhysical()) ?? Dpi.Monitors().FirstOrDefault();
        if (monitor is null) return;

        RectD? selection = RegionWindow.Select(monitor.Value);
        if (selection is null) return;

        Capturer.Result? shot = Capturer.Region(selection.Value);
        if (shot is not null) HangFile(shot.Value.Path, shot.Value.Captured);
    }

    public void ApplyRevealSettings()
    {
        if (LineWindow is not { } window) return;
        window.RevealOnEdge = !Settings.RevealOnEdgeOff;
        window.RevealOnPeek = Settings.RevealOnNewHang;
        window.ShowOverFullScreen = Settings.ShowOverFullScreen;
    }

    public void SetLineSizeCore(string size)
    {
        Model.Size = Layout.ParseSize(size);
        Model.ReloadThumbnails();
    }

    private static void OnUi(Action action)
    {
        Dispatcher dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }
}
