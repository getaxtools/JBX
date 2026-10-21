using System.Collections.Concurrent;
using Jbx.Platform;

namespace Jbx.Testing;

/// <summary>
/// A file system whose folders can be present, missing, denied, failing or blackholed. A blackholed
/// call never completes until <see cref="ReleaseBlackholed"/>, like an SMB call to a dead host.
/// </summary>
public sealed class InMemoryFileSystem(SimplePathCanonicalizer? canonicalizer = null) : IFileSystem
{
    private const int NetworkUnreachableCode = 1231;

    private readonly SimplePathCanonicalizer _canonicalizer = canonicalizer ?? SimplePathCanonicalizer.Instance;
    private readonly ConcurrentDictionary<string, FolderBehaviour> _folders = new();
    private readonly ConcurrentDictionary<string, List<FileEntry>> _files = new();
    private readonly ConcurrentDictionary<string, byte[]> _contents = new();
    private readonly ConcurrentBag<TaskCompletionSource> _blackholed = [];
    private int _inFlight;
    private int _maxInFlight;
    private int _probeCount;
    private int _listingCount;

    public int ProbeCount => _probeCount;

    public int ListingCount => _listingCount;

    public int MaxInFlight => _maxInFlight;

    public int InFlight => _inFlight;

    public void AddFolder(string path, FolderBehaviour behaviour = FolderBehaviour.Present)
    {
        _folders[_canonicalizer.From(path).Key] = behaviour;
    }

    public void AddFile(string folder, string name, DateTimeOffset lastWriteUtc, long sizeBytes = 1024)
    {
        var folderPath = _canonicalizer.From(folder);
        _folders.TryAdd(folderPath.Key, FolderBehaviour.Present);
        var entry = new FileEntry(name, _canonicalizer.From(folderPath.Display + "\\" + name), sizeBytes, lastWriteUtc);
        _files.AddOrUpdate(folderPath.Key, _ => [entry], (_, existing) => [.. existing, entry]);
    }

    public void ReleaseBlackholed()
    {
        while (_blackholed.TryTake(out var pending))
        {
            pending.TrySetResult();
        }
    }

    public Task<ProbeResult> ProbeFolderAsync(CanonicalPath folder)
    {
        Interlocked.Increment(ref _probeCount);
        return RunAsync(BehaviourOf(folder), () => Outcome(BehaviourOf(folder)));
    }

    public Task<ProbeResult> ProbeFileAsync(CanonicalPath file)
    {
        Interlocked.Increment(ref _probeCount);
        var parent = _canonicalizer.Parent(file);
        var behaviour = parent is null ? FolderBehaviour.Missing : BehaviourOf(parent);
        var exists = parent is not null && _files.TryGetValue(parent.Key, out var files) && files.Any(entry => entry.Path == file);
        return RunAsync(behaviour, () => behaviour == FolderBehaviour.Present && !exists ? new ProbeResult(Reachability.NotFound) : Outcome(behaviour));
    }

    public Task<FolderListing> ListFilesAsync(CanonicalPath folder, ListingRequest request)
    {
        Interlocked.Increment(ref _listingCount);
        var behaviour = BehaviourOf(folder);
        return RunAsync(behaviour, () => behaviour == FolderBehaviour.Present ? List(folder, request) : FolderListing.Failed(Outcome(behaviour)));
    }

    public void AddFileContent(string folder, string name, byte[] content)
    {
        AddFile(folder, name, DateTimeOffset.UnixEpoch, content.Length);
        _contents[_canonicalizer.From(_canonicalizer.From(folder).Display + "\\" + name).Key] = content;
    }

    /// <summary>Replaces a file's content, as a writer truncating or rotating a log would.</summary>
    public void SetFileContent(CanonicalPath file, byte[] content)
    {
        _contents[file.Key] = content;
    }

    public void AppendFileContent(CanonicalPath file, byte[] more)
    {
        _contents.AddOrUpdate(file.Key, more, (_, existing) => [.. existing, .. more]);
    }

    public Task<FileRange> ReadRangeAsync(CanonicalPath file, long offset, int maxBytes)
    {
        var behaviour = BehaviourOfParent(file);
        return RunAsync(behaviour, () =>
        {
            if (behaviour != FolderBehaviour.Present)
            {
                return FileRange.Failed(Outcome(behaviour));
            }

            if (!_contents.TryGetValue(file.Key, out var content))
            {
                return FileRange.Failed(new ProbeResult(Reachability.NotFound, 2));
            }

            var start = (int)Math.Min(offset, content.Length);
            var count = Math.Min(maxBytes, content.Length - start);
            return new FileRange(Reachability.Reachable, content.AsMemory(start, count).ToArray(), content.Length);
        });
    }

    public Task<FileStat> StatFileAsync(CanonicalPath file)
    {
        var behaviour = BehaviourOfParent(file);
        return RunAsync(behaviour, () =>
        {
            if (behaviour != FolderBehaviour.Present)
            {
                return FileStat.Failed(Outcome(behaviour));
            }

            return _contents.TryGetValue(file.Key, out var content)
                ? new FileStat(Reachability.Reachable, content.Length, LastWriteOf(content))
                : FileStat.Failed(new ProbeResult(Reachability.NotFound, 2));
        });
    }

    public Task<FileContent> ReadFileAsync(CanonicalPath file, long maxBytes)
    {
        var parent = _canonicalizer.Parent(file);
        var behaviour = parent is null ? FolderBehaviour.Missing : BehaviourOf(parent);
        return RunAsync(behaviour, () =>
        {
            if (behaviour != FolderBehaviour.Present)
            {
                return FileContent.Failed(Outcome(behaviour));
            }

            if (!_contents.TryGetValue(file.Key, out var content))
            {
                return FileContent.Failed(new ProbeResult(Reachability.NotFound, 2));
            }

            return content.Length > maxBytes
                ? new FileContent(Reachability.Reachable, ReadOnlyMemory<byte>.Empty) { IsTooLarge = true }
                : new FileContent(Reachability.Reachable, content);
        });
    }

    private FolderBehaviour BehaviourOf(CanonicalPath folder)
    {
        return _folders.TryGetValue(folder.Key, out var behaviour) ? behaviour : FolderBehaviour.Missing;
    }

    private FolderBehaviour BehaviourOfParent(CanonicalPath file)
    {
        var parent = _canonicalizer.Parent(file);
        return parent is null ? FolderBehaviour.Missing : BehaviourOf(parent);
    }

    // Content identity stands in for a write time: any change of content is a new write.
    private static DateTimeOffset LastWriteOf(byte[] content)
    {
        return DateTimeOffset.UnixEpoch.AddTicks(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(content));
    }

    private static ProbeResult Outcome(FolderBehaviour behaviour)
    {
        return behaviour switch
        {
            FolderBehaviour.Present or FolderBehaviour.Blackholed => ProbeResult.Reachable,
            FolderBehaviour.Denied => new ProbeResult(Reachability.Denied, 5),
            FolderBehaviour.Failing => new ProbeResult(Reachability.Unknown, NetworkUnreachableCode),
            _ => new ProbeResult(Reachability.NotFound, 3),
        };
    }

    private FolderListing List(CanonicalPath folder, ListingRequest request)
    {
        var files = _files.TryGetValue(folder.Key, out var entries) ? entries : [];
        var matching = files.Where(file => Matches(file.Name, request.FilePattern)).ToList();
        var newest = matching.OrderByDescending(file => file.LastWriteUtc).Take(request.MaxEntries).ToList();
        return new FolderListing(Reachability.Reachable, newest, matching.Count, matching.Count > request.MaxEntries);
    }

    private static bool Matches(string name, string? pattern)
    {
        if (pattern is null or "*" or "*.*")
        {
            return true;
        }

        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private async Task<T> RunAsync<T>(FolderBehaviour behaviour, Func<T> result)
    {
        var current = Interlocked.Increment(ref _inFlight);
        InterlockedMax(ref _maxInFlight, current);
        try
        {
            if (behaviour == FolderBehaviour.Blackholed)
            {
                var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _blackholed.Add(pending);
                await pending.Task.ConfigureAwait(false);
            }
            else
            {
                await Task.Yield();
            }

            return result();
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static void InterlockedMax(ref int target, int candidate)
    {
        var current = Volatile.Read(ref target);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref target, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }
}
