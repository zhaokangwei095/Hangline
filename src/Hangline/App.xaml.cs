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
            // Silent exit here looks exactly like a launch that failed, which is
            // what people then report as "the shortcuts stopped working" — they
            // are looking at the older instance that is still running. Ask that
            // one to bring the line down so the double-click does something.
            Interop.NativeMethods.PostMessage(Interop.NativeMethods.HWND_BROADCAST,
                Interop.NativeMethods.RegisterWindowMessage(Interop.NativeMethods.ToggleMessageName),
                IntPtr.Zero, IntPtr.Zero);
            Log.Write("another Hangline is already running; asked it to show the line");
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

    /// One line, one instance. A second launch asks the first to show its line.
    private bool ClaimSingleton()
    {
        _singleInstance = new Mutex(true, @"Local\Hangline.SingleInstance", out bool owned);
        return owned;
    }
}
