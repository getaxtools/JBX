namespace Jbx.Platform.Windows.FileSystem;

/// <summary>
/// Each call runs on its own thread because an unreachable share blocks it for tens of seconds and
/// ignores cancellation. The caller decides how long to wait; this class never decides to give up.
/// </summary>
public sealed class WindowsFileSystem(IPathCanonicalizer canonicalizer) : IFileSystem
{
    private const int SharingViolation = 32;
    private static readonly TimeSpan SharingViolationRetryDelay = TimeSpan.FromMilliseconds(200);

    public Task<ProbeResult> ProbeFolderAsync(CanonicalPath folder)
    {
        return RunOnDedicatedThread(() => ProbeFolder(folder.Display));
    }

    public Task<ProbeResult> ProbeFileAsync(CanonicalPath file)
    {
        return RunOnDedicatedThread(() => ProbeFile(file.Display));
    }

    public Task<FolderListing> ListFilesAsync(CanonicalPath folder, ListingRequest request)
    {
        return RunOnDedicatedThread(() => ListFiles(folder.Display, request));
    }

    public Task<FileContent> ReadFileAsync(CanonicalPath file, long maxBytes)
    {
        return Task.Factory
            .StartNew(() => ReadFileWithOneRetryAsync(file.Display, maxBytes), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .Unwrap();
    }

    public Task<FileRange> ReadRangeAsync(CanonicalPath file, long offset, int maxBytes)
    {
        return RunOnDedicatedThread(() => ReadRange(file.Display, offset, maxBytes));
    }

    public Task<FileStat> StatFileAsync(CanonicalPath file)
    {
        return RunOnDedicatedThread(() => StatFile(file.Display));
    }

    private static Task<T> RunOnDedicatedThread<T>(Func<T> work)
    {
        return Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private static async Task<FileContent> ReadFileWithOneRetryAsync(string file, long maxBytes)
    {
        var first = ReadFile(file, maxBytes);
        if (first.ErrorCode != SharingViolation)
        {
            return first;
        }

        // Editors save by writing a temporary file and renaming it; one short retry rides over the swap.
        await Task.Delay(SharingViolationRetryDelay).ConfigureAwait(false);
        return ReadFile(file, maxBytes);
    }

    private static FileContent ReadFile(string file, long maxBytes)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > maxBytes)
            {
                return new FileContent(Reachability.Reachable, ReadOnlyMemory<byte>.Empty) { IsTooLarge = true };
            }

            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return new FileContent(Reachability.Reachable, bytes);
        }
        catch (IOException exception)
        {
            var errorCode = exception.HResult & 0xFFFF;
            return errorCode == SharingViolation
                ? new FileContent(Reachability.Unknown, ReadOnlyMemory<byte>.Empty) { ErrorCode = SharingViolation }
                : FileContent.Failed(ReachabilityClassifier.Classify(exception));
        }
        catch (UnauthorizedAccessException)
        {
            return FileContent.Failed(ReachabilityClassifier.Denied());
        }
    }

    private static FileRange ReadRange(string file, long offset, int maxBytes)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1, FileOptions.SequentialScan);
            var length = stream.Length;
            if (offset >= length)
            {
                return new FileRange(Reachability.Reachable, ReadOnlyMemory<byte>.Empty, length);
            }

            stream.Position = offset;
            var buffer = new byte[(int)Math.Min(maxBytes, length - offset)];
            var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return new FileRange(Reachability.Reachable, buffer.AsMemory(0, read), length);
        }
        catch (IOException exception)
        {
            return FileRange.Failed(ReachabilityClassifier.Classify(exception));
        }
        catch (UnauthorizedAccessException)
        {
            return FileRange.Failed(ReachabilityClassifier.Denied());
        }
    }

    private static FileStat StatFile(string file)
    {
        try
        {
            // Through a handle rather than FileInfo, which reports a denied file as missing.
            using var handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var lastWrite = new DateTimeOffset(File.GetLastWriteTimeUtc(handle), TimeSpan.Zero);
            return new FileStat(Reachability.Reachable, RandomAccess.GetLength(handle), lastWrite);
        }
        catch (IOException exception)
        {
            return FileStat.Failed(ReachabilityClassifier.Classify(exception));
        }
        catch (UnauthorizedAccessException)
        {
            return FileStat.Failed(ReachabilityClassifier.Denied());
        }
    }

    private static ProbeResult ProbeFolder(string folder)
    {
        try
        {
            using var entries = new DirectoryInfo(folder).EnumerateFileSystemInfos().GetEnumerator();
            entries.MoveNext();
            return ProbeResult.Reachable;
        }
        catch (IOException exception)
        {
            return ReachabilityClassifier.Classify(exception);
        }
        catch (UnauthorizedAccessException)
        {
            return ReachabilityClassifier.Denied();
        }
    }

    private static ProbeResult ProbeFile(string file)
    {
        try
        {
            using var handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return ProbeResult.Reachable;
        }
        catch (IOException exception)
        {
            return ReachabilityClassifier.Classify(exception);
        }
        catch (UnauthorizedAccessException)
        {
            return ReachabilityClassifier.Denied();
        }
    }

    private FolderListing ListFiles(string folder, ListingRequest request)
    {
        try
        {
            return CollectNewest(folder, request);
        }
        catch (IOException exception)
        {
            return FolderListing.Failed(ReachabilityClassifier.Classify(exception));
        }
        catch (UnauthorizedAccessException)
        {
            return FolderListing.Failed(ReachabilityClassifier.Denied());
        }
    }

    private FolderListing CollectNewest(string folder, ListingRequest request)
    {
        // IgnoreInaccessible stays off: with it on, a denied folder lists as empty instead of denied.
        var options = new EnumerationOptions { IgnoreInaccessible = false, RecurseSubdirectories = false, MatchType = MatchType.Win32 };
        var oldestKeptFirst = new PriorityQueue<FileInfo, DateTime>();
        var matching = 0;
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles(request.FilePattern ?? "*", options))
        {
            matching++;
            if (oldestKeptFirst.Count < request.MaxEntries)
            {
                oldestKeptFirst.Enqueue(file, file.LastWriteTimeUtc);
            }
            else if (oldestKeptFirst.TryPeek(out _, out var oldestKept) && file.LastWriteTimeUtc > oldestKept)
            {
                oldestKeptFirst.EnqueueDequeue(file, file.LastWriteTimeUtc);
            }
        }

        var newestFirst = oldestKeptFirst.UnorderedItems
            .Select(item => item.Element)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(ToEntry)
            .OfType<FileEntry>()
            .ToList();
        return new FolderListing(Reachability.Reachable, newestFirst, matching, matching > request.MaxEntries);
    }

    private FileEntry? ToEntry(FileInfo file)
    {
        var path = canonicalizer.TryCanonicalize(file.FullName);
        return path is null ? null : new FileEntry(file.Name, path, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero));
    }
}
