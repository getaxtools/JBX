using Jbx.Core.Diagnostics;
using Jbx.Platform;

namespace Jbx.Services.Probing;

public sealed class ReachabilityProbe(IFileSystem fileSystem, FileSystemGate gate, ProbeCache cache, IDiagnosticLog diagnosticLog)
{
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private readonly Dictionary<CanonicalPath, Task<ProbeResult>> _inFlight = [];
    private readonly Lock _inFlightLock = new();

    public Task<ProbeResult> ProbeFolderAsync(CanonicalPath folder, CancellationToken cancellationToken)
    {
        return ProbeAsync(folder, () => fileSystem.ProbeFolderAsync(folder), cancellationToken);
    }

    public Task<ProbeResult> ProbeFileAsync(CanonicalPath file, CancellationToken cancellationToken)
    {
        return ProbeAsync(file, () => fileSystem.ProbeFileAsync(file), cancellationToken);
    }

    public void Forget(IEnumerable<CanonicalPath> paths)
    {
        var forgotten = paths.ToList();
        cache.Forget(forgotten);
        lock (_inFlightLock)
        {
            forgotten.ForEach(path => _inFlight.Remove(path));
        }
    }

    private Task<ProbeResult> ProbeAsync(CanonicalPath path, Func<Task<ProbeResult>> call, CancellationToken cancellationToken)
    {
        if (cache.TryGet(path, out var cached))
        {
            return Task.FromResult(cached);
        }

        lock (_inFlightLock)
        {
            // Two rows showing the same folder share one probe while it is still running.
            if (!_inFlight.TryGetValue(path, out var probe) || probe.IsCompleted)
            {
                probe = RunAsync(path, call, cancellationToken);
                _inFlight[path] = probe;
            }

            return probe;
        }
    }

    private async Task<ProbeResult> RunAsync(CanonicalPath path, Func<Task<ProbeResult>> call, CancellationToken cancellationToken)
    {
        var outcome = await gate.RunAsync(call, ProbeTimeout, cancellationToken).ConfigureAwait(false);
        var result = outcome.IsTimedOut || outcome.Value is null ? ProbeResult.TimedOut : outcome.Value;
        cache.Store(path, result);
        Record(path, result);
        return result;
    }

    private void Record(CanonicalPath path, ProbeResult result)
    {
        var detail = new ResourceDetail(new ResourceItem(ResourceKind.Path, path.Display));
        switch (result.Reachability)
        {
            case Reachability.Timeout:
                diagnosticLog.Write(new AppEvent(EventCode.ProbeTimedOut, EventSeverity.Warning, new EventMeasure(MeasureKind.DurationMilliseconds, (long)ProbeTimeout.TotalMilliseconds)), detail);
                break;
            case Reachability.Unknown:
                diagnosticLog.Write(new AppEvent(EventCode.ProbeFailed, EventSeverity.Warning, new EventMeasure(MeasureKind.HResult, result.ErrorCode ?? 0)), detail);
                break;
        }
    }
}
