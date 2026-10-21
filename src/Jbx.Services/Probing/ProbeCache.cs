using System.Collections.Concurrent;
using Jbx.Platform;

namespace Jbx.Services.Probing;

/// <summary>Recent probe results by canonical path. Never persisted: yesterday's "reachable" is worse than no answer.</summary>
public sealed class ProbeCache(TimeProvider timeProvider)
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<CanonicalPath, (ProbeResult Result, DateTimeOffset StoredAt)> _entries = new();

    public bool TryGet(CanonicalPath path, out ProbeResult result)
    {
        if (_entries.TryGetValue(path, out var entry) && timeProvider.GetUtcNow() - entry.StoredAt < TimeToLive)
        {
            result = entry.Result;
            return true;
        }

        result = ProbeResult.TimedOut;
        return false;
    }

    public void Store(CanonicalPath path, ProbeResult result)
    {
        _entries[path] = (result, timeProvider.GetUtcNow());
    }

    public void Forget(IEnumerable<CanonicalPath> paths)
    {
        foreach (var path in paths)
        {
            _entries.TryRemove(path, out _);
        }
    }
}
