using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Jbx.Core.Diagnostics;
using Jbx.Desktop.Infrastructure;

namespace Jbx.Desktop.Features.Crash;

/// <summary>
/// Catches what nothing else caught, logs it locally and shows the crash dialog. This is the one place
/// that catches every exception type: a crash handler that throws would replace the original failure.
/// </summary>
internal static class CrashHandlers
{
    private static readonly TimeSpan BackgroundCrashWait = TimeSpan.FromMinutes(10);
    private static Func<IDiagnosticLog?> _log = () => null;
    private static int _showing;

    public static string AppVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? "unknown";

    public static void Install(Func<IDiagnosticLog?> log)
    {
        _log = log;
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Show(e.Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Record(e.Exception, EventSeverity.Warning);
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                ShowFromAnyThread(exception);
            }
        };
    }

    [SuppressMessage("Design", "CA1031", Justification = "The crash path must survive any failure of its own; the fallback is a plain message.")]
    public static void Show(Exception exception)
    {
        Record(exception, EventSeverity.Error);
        if (Interlocked.Exchange(ref _showing, 1) == 1)
        {
            return;
        }

        try
        {
            var report = CrashReportBuilder.Build(exception, AppVersion, DateTimeOffset.UtcNow);
            CrashWindow? window = null;
            var model = new CrashViewModel(report, new AvaloniaUserInteraction(() => window));
            window = new CrashWindow { DataContext = model };
            model.CloseRequested += (_, _) =>
            {
                window.Close();
                ShutDown();
            };
            window.Show();
            window.Activate();
        }
        catch (Exception)
        {
            ShutDown();
        }
    }

    private static void ShowFromAnyThread(Exception exception)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Show(exception);
            return;
        }

        // The runtime ends the process when this handler returns, so keep it alive until the dialog closes.
        using var closed = new ManualResetEventSlim();
        Dispatcher.UIThread.Post(() =>
        {
            Show(exception);
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Exit += (_, _) => closed.Set();
            }
        });
        closed.Wait(BackgroundCrashWait);
    }

    [SuppressMessage("Design", "CA1031", Justification = "Logging a crash must never become a second crash.")]
    private static void Record(Exception exception, EventSeverity severity)
    {
        try
        {
            var detail = new ResourceDetail(
                new ResourceItem(ResourceKind.ExceptionType, exception.GetType().FullName ?? "unknown"),
                new ResourceItem(ResourceKind.ExceptionMessage, exception.Message));
            _log()?.Write(new AppEvent(EventCode.UnhandledException, severity, new EventMeasure(MeasureKind.HResult, exception.HResult)), detail);
        }
        catch (Exception)
        {
            return;
        }
    }

    private static void ShutDown()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(1);
        }
    }
}
