using System.Threading;
using System.Windows;
using Hangline.Shell;

namespace Hangline;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppServices? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!ClaimSingleton())
        {
            // Silent exit here looks exactly like a launch that failed.
            Log.Write("another Hangline is already running; this one quits");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("dispatcher", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("domain", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("task", args.Exception);
            args.SetObserved();
        };

        _services = new AppServices();
        _services.Startup(e.Args);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Shutdown();
        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); } catch (ApplicationException) { }
            _singleInstance.Dispose();
        }
        base.OnExit(e);
    }

    /// One line, one instance. A second launch just asks the first to toggle.
    private bool ClaimSingleton()
    {
        _singleInstance = new Mutex(true, @"Local\Hangline.SingleInstance", out bool owned);
        return owned;
    }
}
