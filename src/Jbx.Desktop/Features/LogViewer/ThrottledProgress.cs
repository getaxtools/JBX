using Jbx.Desktop.Infrastructure;

namespace Jbx.Desktop.Features.LogViewer;

/// <summary>
/// Progress from a background read, passed to the UI thread at most every <c>interval</c>. A report
/// still queued when the work ends is dropped, so a finished task never shows "99%" afterwards.
/// </summary>
internal sealed class ThrottledProgress(IUiDispatcher dispatcher, TimeProvider time, TimeSpan interval, Action<double> report) : IProgress<double>
{
    private long _lastReportTicks;
    private volatile bool _isFinished;

    public void Report(double value)
    {
        var now = time.GetTimestamp();
        var last = Interlocked.Read(ref _lastReportTicks);
        if (_isFinished || time.GetElapsedTime(last, now) < interval || Interlocked.CompareExchange(ref _lastReportTicks, now, last) != last)
        {
            return;
        }

        dispatcher.Post(() =>
        {
            if (!_isFinished)
            {
                report(value);
            }
        });
    }

    public void Finish()
    {
        _isFinished = true;
    }
}
