using System.Collections.Concurrent;
using Jbx.Platform;
using Jbx.Services.Probing;

namespace Jbx.Services.Rows;

/// <summary>
/// The current reachability of each folder in one tab. Every probe gets a generation number, and a
/// result is applied only if no newer probe of that folder has started since: late answers are dropped.
/// </summary>
public sealed class ReachabilityTracker(ReachabilityProbe probe, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, (FolderStatus Status, long Generation)> _states = new(StringComparer.Ordinal);
    private long _generation;

    public event EventHandler<FolderEntry>? StatusChanged;

    public FolderStatus StatusOf(FolderEntry entry)
    {
        return _states.TryGetValue(entry.Key, out var state) ? state.Status : FolderStatus.NotChecked;
    }

    public async Task CheckAsync(IEnumerable<FolderEntry> entries, CancellationToken cancellationToken)
    {
        var checks = entries.Where(entry => entry.Path is not null).Select(entry => CheckOneAsync(entry, cancellationToken)).ToList();
        await Task.WhenAll(checks).ConfigureAwait(false);
    }

    public Task RefreshAsync(IReadOnlyCollection<FolderEntry> entries, CancellationToken cancellationToken)
    {
        probe.Forget(entries.Select(entry => entry.Path).OfType<CanonicalPath>());
        return CheckAsync(entries, cancellationToken);
    }

    private async Task CheckOneAsync(FolderEntry entry, CancellationToken cancellationToken)
    {
        var generation = Interlocked.Increment(ref _generation);
        _states[entry.Key] = (new FolderStatus(Reachability.Checking, null), generation);
        StatusChanged?.Invoke(this, entry);

        var result = await probe.ProbeFolderAsync(entry.Path!, cancellationToken).ConfigureAwait(false);
        var status = new FolderStatus(result.Reachability, timeProvider.GetUtcNow()) { ErrorCode = result.ErrorCode };
        if (TryApply(entry, generation, status))
        {
            StatusChanged?.Invoke(this, entry);
        }
    }

    private bool TryApply(FolderEntry entry, long generation, FolderStatus status)
    {
        while (_states.TryGetValue(entry.Key, out var current))
        {
            if (current.Generation != generation)
            {
                return false;
            }

            if (_states.TryUpdate(entry.Key, (status, generation), current))
            {
                return true;
            }
        }

        return false;
    }
}
