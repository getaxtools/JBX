using Jbx.Core.Diagnostics;
using Jbx.Platform;
using Jbx.Services.Probing;

namespace Jbx.Services.Logs;

/// <summary>Every read of a log file, bounded by the same gate and kind of timeout as probes and listings.</summary>
public sealed class LogFileReader(IFileSystem fileSystem, FileSystemGate gate, IDiagnosticLog diagnosticLog, TimeProvider time)
{
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    public TimeProvider Time { get; } = time;

    public async Task<FileRange> ReadAsync(CanonicalPath file, long offset, int maxBytes, CancellationToken cancellationToken)
    {
        var outcome = await gate.RunAsync(() => fileSystem.ReadRangeAsync(file, offset, maxBytes), ReadTimeout, cancellationToken).ConfigureAwait(false);
        if (outcome.IsTimedOut || outcome.Value is null)
        {
            diagnosticLog.Write(new AppEvent(EventCode.LogReadTimedOut, EventSeverity.Warning, new EventMeasure(MeasureKind.DurationMilliseconds, (long)ReadTimeout.TotalMilliseconds)), Detail(file));
            return FileRange.Failed(ProbeResult.TimedOut);
        }

        if (outcome.Value.Reachability != Reachability.Reachable)
        {
            var measures = outcome.Value.ErrorCode is { } code ? new[] { new EventMeasure(MeasureKind.HResult, code) } : [];
            diagnosticLog.Write(new AppEvent(EventCode.LogReadFailed, EventSeverity.Warning, measures), Detail(file));
        }

        return outcome.Value;
    }

    public async Task<FileStat> StatAsync(CanonicalPath file, CancellationToken cancellationToken)
    {
        var outcome = await gate.RunAsync(() => fileSystem.StatFileAsync(file), ReachabilityProbe.ProbeTimeout, cancellationToken).ConfigureAwait(false);
        return outcome.IsTimedOut || outcome.Value is null ? FileStat.Failed(ProbeResult.TimedOut) : outcome.Value;
    }

    public void RecordOpened(CanonicalPath file, long sizeBytes, int lineCount, TimeSpan duration)
    {
        diagnosticLog.Write(
            new AppEvent(
                EventCode.LogOpened,
                EventSeverity.Information,
                new EventMeasure(MeasureKind.SizeBytes, sizeBytes),
                new EventMeasure(MeasureKind.Count, lineCount),
                new EventMeasure(MeasureKind.DurationMilliseconds, (long)duration.TotalMilliseconds)),
            Detail(file));
    }

    public void RecordTruncated(CanonicalPath file)
    {
        diagnosticLog.Write(new AppEvent(EventCode.LogTruncated, EventSeverity.Information), Detail(file));
    }

    private static ResourceDetail Detail(CanonicalPath file)
    {
        return new ResourceDetail(new ResourceItem(ResourceKind.Path, file.Display));
    }
}
