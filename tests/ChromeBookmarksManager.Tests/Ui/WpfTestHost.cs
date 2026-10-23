using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace ChromeBookmarksManager.Tests.Ui;

public static class WpfTestHost
{
    private static readonly object Gate = new();
    private static Thread? _thread;
    private static Dispatcher? _dispatcher;
    private static ExceptionDispatchInfo? _startupException;

    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Run(() =>
        {
            action();
            return true;
        });
    }

    public static T Run<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = GetDispatcher();

        return dispatcher.CheckAccess()
            ? action()
            : dispatcher.Invoke(action);
    }

    private static Dispatcher GetDispatcher()
    {
        lock (Gate)
        {
            if (_dispatcher is not null)
            {
                _startupException?.Throw();
                return _dispatcher;
            }

            using var started = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                try
                {
                    if (WpfApplication.Current is null)
                    {
                        _ = new global::ChromeBookmarksManager.App
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown
                        };
                    }

                    _dispatcher = Dispatcher.CurrentDispatcher;
                }
                catch (Exception exception)
                {
                    _startupException = ExceptionDispatchInfo.Capture(exception);
                }
                finally
                {
                    started.Set();
                }

                if (_startupException is null)
                {
                    Dispatcher.Run();
                }
            })
            {
                IsBackground = true,
                Name = "ChromeBookmarksManager WPF test dispatcher"
            };

            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            started.Wait();
            _startupException?.Throw();

            return _dispatcher!;
        }
    }
}
