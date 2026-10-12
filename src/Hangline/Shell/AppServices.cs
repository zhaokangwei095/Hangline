using System.Windows;
using System.Windows.Threading;
using Hangline.Capture;
using Hangline.Core;
using Hangline.Line;

namespace Hangline.Shell;

/// <summary>
/// The single place where the app's pieces are wired together. No DI container:
/// reading the startup order top to bottom should tell you what exists.
/// Phases add their piece here and nowhere else.
/// </summary>
public sealed partial class AppServices
{
    public SettingsStore Store { get; } = new();
    public Settings Settings { get; private set; }
    public TrayController Tray { get; private set; } = null!;
    public LineModel Model { get; private set; } = null!;

    /// Set once the line window exists (P1 onward); the tray menu reads it.
    public ILineHost? Line { get; set; }

    public LineWindow? LineWindow { get; private set; }

    /// Set once the folder and clipboard watchers exist (P5 onward).
    public Watchers.IWatchers? Watchers { get; set; }

    public AppServices()
    {
        Settings = Store.Load();
        Localization.SetLanguage(Settings.Language);
    }

    public void Startup(string[] args)
    {
        Log.Write($"launched: {Environment.ProcessPath} args=[{string.Join("][", args)}]");
        // The tray menu reads the hotkey state, so the inputs have to exist
        // before the menu is built — building it first threw on startup.
        StartupLine();
        StartupInputs();
        ApplyRevealSettings();

        Tray = new TrayController(this);
        Tray.Show();
        Debug.Handle(args, this);
        Log.Write($"started, config at {Store.FilePath}");
    }

    private void StartupLine()
    {
        Inbox.Ensure();

        Model = new LineModel
        {
            Size = Layout.ParseSize(Settings.LineSize),
            KeepOnLine = Settings.KeepChoice,
            SoundOn = !Settings.SoundOff,
            QuietHang = Settings.QuietHang,
            InInbox = Inbox.IsInInbox,
            PlaySound = SoundKit.Play,
            Recycle = Interop.FileOps.Recycle,
            Delay = Delay,
        };

        // What was hanging when Hangline last quit comes back, before the line
        // knows its screen, so nothing falls off at launch.
        Model.Restore(Settings.Pegged);
        Model.Changed += QueueSave;

        var window = new LineWindow(Model) { Owner = null };
        LineWindow = window;
        Line = window;
        window.StartPolling();
        window.ShowAt(0, 0);
    }

    /// Saving on every card change would write the file while the line is being
    /// dragged; once a second is enough.
    private DispatcherTimer? _saveTimer;

    private SettingsWindow? _settings;

    private void QueueSave()
    {
        _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick -= OnSaveTick;
        _saveTimer.Tick += OnSaveTick;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTick(object? sender, EventArgs e)
    {
        if (sender is DispatcherTimer timer) timer.Stop();
        Settings.Pegged = Model.PathsToSave().ToList();
        Save();
    }

    public void Shutdown()
    {
        // The strip window hides itself whenever the line is tucked away, so
        // visibility says nothing about whether photos are hanging; quitting
        // right after a hang used to lose it.
        Settings.Pegged = Model.PathsToSave().ToList();
        Save();
        (Watchers as IDisposable)?.Dispose();
        Clipboards.Dispose();
        Keys.Dispose();
        Sink.Dispose();
        Tray.Dispose();
        LineWindow?.Close();
        Log.Write("quit");
    }

    public void Save() => Store.Save(Settings);

    /// Run something some seconds from now, back on the UI thread.
    public static void Delay(double seconds, Action action)
    {
        Dispatcher dispatcher = Application.Current.Dispatcher;
        _ = Task.Delay(TimeSpan.FromSeconds(seconds)).ContinueWith(_ =>
        {
            try { dispatcher.Invoke(action); }
            catch (Exception ex) { Log.Error("delayed action", ex); }
        });
    }
}
