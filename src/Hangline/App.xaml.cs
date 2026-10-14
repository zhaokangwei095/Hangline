using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;
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

        // WPF's Application creates an internal "Hidden Window" for the APIs
        // that expect a main window. It carries WS_VISIBLE but no
        // WS_EX_TOOLWINDOW, so Explorer lists it on the taskbar as a nameless
        // button — and closing that button tears down the very window the
        // clipboard listener and the message sink hang off, which silently
        // kills "hang copied images" until the app restarts. It only exists
        // once the first window is being created, so this runs after startup
        // rather than before it.
        Dispatcher.BeginInvoke(new Action(HideInternalTaskbarWindow), DispatcherPriority.Loaded);
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

    /// <summary>
    /// WPF's Application creates an internal window for the APIs that expect a
    /// main window. It is titled "Hidden Window", carries WS_VISIBLE and no
    /// WS_EX_TOOLWINDOW, so Explorer lists it on the taskbar as a nameless
    /// button — and closing that button tears down the very window the
    /// clipboard listener and the message sink hang off, which silently kills
    /// "hang copied images" until the app restarts.
    /// <para>
    /// It is restyled rather than closed: WPF needs it alive. Only windows this
    /// process owns are touched, and only the untitled ones — the IME and the
    /// message sink are left alone, they already carry the right style.
    /// </para>
    /// </summary>
    private static void HideInternalTaskbarWindow()
    {
        const int GWL_EXSTYLE = -20;
        const long TOOLWINDOW = 0x0000_0080;
        const long NOACTIVATE = 0x0800_0000;
        // WPF's internal window is titled "Hidden Window". Matching on the
        // title rather than on a style bit: its extended style carries neither
        // WS_VISIBLE nor WS_EX_TOOLWINDOW, so neither says anything useful
        // about the taskbar entry it owns.
        const string HiddenTitle = "Hidden Window";
        const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004,
                   SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;

        IntPtr self = System.Diagnostics.Process.GetCurrentProcess().Id;
        bool Enum(IntPtr hwnd, IntPtr _)
        {
            Interop.NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != self) return true;
            if (hwnd == Interop.NativeMethods.HWND_MESSAGE) return true;

            long style = Interop.NativeMethods.GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            if ((style & TOOLWINDOW) != 0) return true;   // already hidden

            var title = new System.Text.StringBuilder(256);
            Interop.NativeMethods.GetWindowText(hwnd, title, title.Capacity);
            if (title.ToString() != HiddenTitle) return true;

            long next = style | TOOLWINDOW | NOACTIVATE;
            Interop.NativeMethods.SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(next));
            Interop.NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            Shell.Log.Write($"taskbar entry removed for hwnd 0x{hwnd.ToInt64():x} \"{title}\"");
            return true;
        }

        Interop.NativeMethods.EnumWindows(Enum, IntPtr.Zero);
    }
}
