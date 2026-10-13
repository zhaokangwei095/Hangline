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
    public HotKeyManager Keys { get; private set; } = null!;
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
        Keys = new HotKeyManager(Sink);

        // A second launch of the exe cannot do anything itself, so it broadcasts
        // this and the running instance answers by showing the line.
        Sink.On(NativeMethods.RegisterWindowMessage(NativeMethods.ToggleMessageName),
            (_, _) => OnUi(() => LineWindow?.Reveal()));

        Keys.TryBind(Toggle, ShortcutFor(Toggle), () => Line?.Toggle());
        Keys.TryBind(Region, ShortcutFor(Region), CaptureRegion);
        Keys.TryBind(Fullscreen, ShortcutFor(Fullscreen), CaptureFullscreen);
        Keys.TryBind(Window, ShortcutFor(Window), CaptureWindow);
        ApplySnapshotKey();

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
        PeggedItem? item = Model.Hang(path, flying: true, quietly: Settings.QuietHang, captured: captured);
        if (item is null) return null;

        // The line has to be down before anything can fly onto it, so the peek
        // comes first and the flight second.
        LineWindow?.Peek();
        LineWindow?.FlyIn(item);
        Tray.RebuildMenu();
        return item;
    }

    public void CaptureFullscreen()
    {
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

    public const string Toggle = "toggle";
    public const string Region = "region";
    public const string Fullscreen = "fullscreen";
    public const string Window = "window";
    public const string Snapshot = "snapshot";

    /// <summary>
    /// Whether Print Screen is actually ours at the moment. The settings window
    /// shows this instead of just echoing the checkbox, because a hotkey that
    /// another app grabbed first looks exactly like a feature that does nothing.
    /// </summary>
    public bool SnapshotBound { get; private set; }

    /// The shortcut a binding uses when the user never chose one.
    public Shortcut DefaultFor(string name) => name switch
    {
        Toggle => Shortcut.Standard,
        Region => RegionDefault,
        Fullscreen => FullscreenDefault,
        Window => WindowDefault,
        _ => Shortcut.None,
    };

    /// The shortcut a binding should use: what the user chose, or the built-in
    /// default when they never chose one. An explicit empty entry is their "off".
    public Shortcut ShortcutFor(string name) => name switch
    {
        Toggle => Settings.ToShortcut(),
        _ => Stored(name switch
        {
            Region => Settings.RegionShortcut,
            Fullscreen => Settings.FullscreenShortcut,
            Window => Settings.WindowShortcut,
            _ => null,
        }) ?? DefaultFor(name),
    };

    private static Shortcut? Stored(ShortcutSettings? saved) => saved switch
    {
        null => null,
        { IsEmpty: true } => Shortcut.None,
        var s => new Shortcut(s.VirtualKey, (HotKeyModifiers)s.Modifiers, s.Key),
    };

    /// Remembers the user's choice for any of the four re-bindable keys.
    public void WriteShortcut(string name, Shortcut shortcut)
    {
        switch (name)
        {
            case Toggle: Settings.SetShortcut(shortcut); break;
            case Region: Settings.RegionShortcut = Settings.ToSettings(shortcut); break;
            case Fullscreen: Settings.FullscreenShortcut = Settings.ToSettings(shortcut); break;
            case Window: Settings.WindowShortcut = Settings.ToSettings(shortcut); break;
            default: return;
        }
        Save();
    }

    /// Taking Print Screen over is the Windows equivalent of the original
    /// rewriting the system screenshot preferences: the same key, but the shot
    /// lands on the line instead of the desktop. Releasing it is a single
    /// UnregisterHotKey, and unlike a registry edit it cannot be left behind.
    public void ApplySnapshotKey()
    {
        if (Settings.InboxEnabled)
        {
            SnapshotBound = Keys.TryBind(Snapshot, Shortcut.PrintScreen, CaptureRegion);
            if (!SnapshotBound)
                Log.Write("Print Screen is held by another app; the rest of takeover still works");
        }
        else
        {
            Keys.Unbind(Snapshot);
            SnapshotBound = false;
        }
    }

    /// What the settings window says about the Print Screen key. Claiming the
    /// key is ours when the registration was refused is the one thing this app
    /// must not do.
    public string SnapshotStatus()
    {
        if (!Settings.InboxEnabled) return Localization.T("Print Screen is left to Windows.");
        return SnapshotBound
            ? Localization.T("Print Screen is ours: press it and drag out a region.")
            : Localization.T("Something else already holds Print Screen, so that key is not ours. Everything else about takeover works.");
    }

    /// Re-bind a named shortcut, keeping the old one if Windows refuses the new.
    public bool Rebind(string name, Shortcut shortcut)
    {
        Shortcut fallback = Keys.Current(name);
        if (!Keys.TryBind(name, shortcut, HandlerFor(name)))
        {
            Keys.TryBind(name, fallback, HandlerFor(name));
            return false;
        }
        WriteShortcut(name, shortcut);
        return true;
    }

    private Action HandlerFor(string name) => name switch
    {
        Toggle => () => Line?.Toggle(),
        Region => CaptureRegion,
        Fullscreen => CaptureFullscreen,
        Window => CaptureWindow,
        Snapshot => CaptureRegion,
        _ => () => { },
    };

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
