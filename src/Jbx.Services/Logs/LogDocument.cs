using Jbx.Platform;

namespace Jbx.Services.Logs;

/// <summary>
/// One open log file. Keeps only the line index in memory and reads the text of the lines on screen
/// when asked, so a file of hundreds of megabytes opens as fast as the first megabyte arrives.
/// Every read goes through <see cref="LogFileReader"/> and so is bounded; nothing here can block forever.
/// </summary>
public sealed class LogDocument(CanonicalPath path, LogFileReader reader)
{
    public const int ChunkBytes = 1 << 20;
    public const int MaxLineBytes = 64 * 1024;
    public const int MaxPageBytes = 4 << 20;
    public const int MaxMatches = 1_000_000;

    private LineIndex? _index;

    public CanonicalPath Path { get; } = path;

    private LineIndex Index => _index ?? throw new InvalidOperationException("The document is read before it was loaded.");

    public LogEncoding? Encoding => _index?.Encoding;

    public int LineCount => _index?.Count ?? 0;

    public int CompleteLineCount => _index?.CompleteCount ?? 0;

    public bool IsCapped => _index?.IsCapped ?? false;

    /// <summary>The file's length the last time it was read.</summary>
    public long FileLength { get; private set; }

    /// <summary>Indexes the file up to its length when loading starts; later growth is picked up by <see cref="RefreshAsync"/>.</summary>
    public async Task<Reachability> LoadAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var started = reader.Time.GetTimestamp();
        var first = await reader.ReadAsync(Path, 0, ChunkBytes, cancellationToken).ConfigureAwait(false);
        if (first.Reachability != Reachability.Reachable)
        {
            return first.Reachability;
        }

        var encoding = LogEncoding.Detect(first.Bytes.Span);
        var index = new LineIndex(encoding);
        Append(index, first.Bytes[Math.Min(encoding.PreambleLength, first.Bytes.Length)..]);
        FileLength = first.FileLength;
        _index = index;
        var outcome = await ReadOnAsync(first.FileLength, progress, cancellationToken).ConfigureAwait(false);
        reader.RecordOpened(Path, FileLength, index.Count, reader.Time.GetElapsedTime(started));
        return outcome;
    }

    public async Task<LogRefresh> RefreshAsync(CancellationToken cancellationToken)
    {
        if (_index is not { } index)
        {
            return new LogRefresh(Reachability.Unknown, 0, 0);
        }

        var before = index.Count;
        var lastLineUnfinished = before > index.CompleteCount;
        var next = await reader.ReadAsync(Path, index.IndexedLength, ChunkBytes, cancellationToken).ConfigureAwait(false);
        if (next.Reachability != Reachability.Reachable)
        {
            return new LogRefresh(next.Reachability, before, before);
        }

        if (next.FileLength < index.IndexedLength)
        {
            reader.RecordTruncated(Path);
            return new LogRefresh(Reachability.Reachable, 0, before) { WasTruncated = true };
        }

        FileLength = next.FileLength;
        Append(index, next.Bytes);
        var outcome = await ReadOnAsync(next.FileLength, null, cancellationToken).ConfigureAwait(false);
        // Only an unfinished last line can change; a finished one stays as it was.
        var grewLastLine = lastLineUnfinished && next.Bytes.Length > 0;
        return new LogRefresh(outcome, grewLastLine ? before - 1 : before, index.Count);
    }

    public async Task<LogPage> ReadLinesAsync(int first, int count, CancellationToken cancellationToken)
    {
        if (_index is not { } index || first >= index.Count || count <= 0)
        {
            return new LogPage(Reachability.Reachable, []);
        }

        var spans = Enumerable.Range(first, Math.Min(count, index.Count - first)).Select(index.SpanOf).ToList();
        var start = spans[0].Start;
        var totalBytes = spans[^1].End - start;
        if (totalBytes > MaxPageBytes)
        {
            return await ReadLinesOneByOneAsync(first, spans, cancellationToken).ConfigureAwait(false);
        }

        var range = await reader.ReadAsync(Path, start, (int)totalBytes, cancellationToken).ConfigureAwait(false);
        if (range.Reachability != Reachability.Reachable)
        {
            return new LogPage(range.Reachability, []);
        }

        var lines = new List<LogLine>(spans.Count);
        for (var offset = 0; offset < spans.Count; offset++)
        {
            var span = spans[offset];
            var from = (int)Math.Min(span.Start - start, range.Bytes.Length);
            var to = (int)Math.Min(span.End - start, range.Bytes.Length);
            lines.Add(ToLine(first + offset, span, range.Bytes.Span[from..to]));
        }

        return new LogPage(Reachability.Reachable, lines);
    }

    /// <summary>Finds the lines from the request's first line on that match. Levels alone need no reading.</summary>
    public async Task<LogScan> ScanAsync(LogScanRequest request, CancellationToken cancellationToken)
    {
        if (_index is not { } index)
        {
            return new LogScan(Reachability.Reachable, []);
        }

        if (request.Matcher.Query.HasText)
        {
            return await ScanTextAsync(index, request, cancellationToken).ConfigureAwait(false);
        }

        // Millions of lines are a noticeable pause: never on the caller's thread.
        return await Task.Run(() => ScanLevels(index.CopyLevels(request.FromLine), request.Matcher, request.FromLine), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Counts the levels of the finished lines from <paramref name="fromLine"/> on. Reads nothing.</summary>
    public Task<LevelCounts> CountLevelsAsync(int fromLine, CancellationToken cancellationToken)
    {
        return _index is { } index
            ? Task.Run(() => CountLevels(index.CopyLevels(fromLine)), cancellationToken)
            : Task.FromResult(LevelCounts.None);
    }

    private static LevelCounts CountLevels(LineLevel[] levels)
    {
        int errors = 0, warnings = 0, info = 0, debug = 0;
        foreach (var level in levels)
        {
            switch (level)
            {
                case LineLevel.Error:
                    errors++;
                    break;
                case LineLevel.Warning:
                    warnings++;
                    break;
                case LineLevel.Info:
                    info++;
                    break;
                case LineLevel.Debug:
                    debug++;
                    break;
            }
        }

        return new LevelCounts(errors, warnings, info, debug);
    }

    private static LogScan ScanLevels(LineLevel[] levels, LogMatcher matcher, int fromLine)
    {
        var matches = new List<int>();
        for (var offset = 0; offset < levels.Length && matches.Count < MaxMatches; offset++)
        {
            if (matcher.MatchesLevel(levels[offset]))
            {
                matches.Add(fromLine + offset);
            }
        }

        return new LogScan(Reachability.Reachable, matches) { IsCapped = matches.Count == MaxMatches };
    }

    private async Task<LogScan> ScanTextAsync(LineIndex index, LogScanRequest request, CancellationToken cancellationToken)
    {
        var matches = new List<int>();
        var count = index.Count;
        var line = request.FromLine;
        while (line < count && matches.Count < MaxMatches)
        {
            var batch = BatchFrom(index, line, count);
            var page = await ReadLinesAsync(line, batch, cancellationToken).ConfigureAwait(false);
            if (page.Reachability != Reachability.Reachable)
            {
                return new LogScan(page.Reachability, matches);
            }

            matches.AddRange(page.Lines.Where(candidate => request.Matcher.IsMatch(candidate.Text, candidate.Level)).Select(candidate => candidate.Number));
            line += batch;
            request.Progress?.Report(count == 0 ? 1 : (double)line / count);
        }

        if (matches.Count > MaxMatches)
        {
            matches.RemoveRange(MaxMatches, matches.Count - MaxMatches);
        }

        return new LogScan(Reachability.Reachable, matches) { IsCapped = matches.Count == MaxMatches };
    }

    private static int BatchFrom(LineIndex index, int first, int count)
    {
        var start = index.SpanOf(first).Start;
        var last = first;
        while (last + 1 < count && index.SpanOf(last + 1).End - start <= MaxPageBytes)
        {
            last++;
        }

        return last - first + 1;
    }

    private async Task<LogPage> ReadLinesOneByOneAsync(int first, List<LineSpan> spans, CancellationToken cancellationToken)
    {
        var lines = new List<LogLine>(spans.Count);
        for (var offset = 0; offset < spans.Count; offset++)
        {
            var span = spans[offset];
            var range = await reader.ReadAsync(Path, span.Start, (int)Math.Min(span.Length, MaxLineBytes), cancellationToken).ConfigureAwait(false);
            if (range.Reachability != Reachability.Reachable)
            {
                return new LogPage(range.Reachability, lines);
            }

            lines.Add(ToLine(first + offset, span, range.Bytes.Span));
        }

        return new LogPage(Reachability.Reachable, lines);
    }

    private LogLine ToLine(int number, LineSpan span, ReadOnlySpan<byte> bytes)
    {
        var index = Index;
        var isCut = bytes.Length > MaxLineBytes;
        var text = index.Encoding.Decode(isCut ? bytes[..MaxLineBytes] : bytes);
        var level = span.IsComplete ? span.Level : LineLevelClassifier.Classify(text, index.LevelBefore(number));
        return new LogLine(number, text, level) { IsCut = isCut || span.Length > bytes.Length, IsUnfinished = !span.IsComplete };
    }

    private async Task<Reachability> ReadOnAsync(long target, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var index = Index;
        while (index.IndexedLength < target && !index.IsCapped)
        {
            var range = await reader.ReadAsync(Path, index.IndexedLength, ChunkBytes, cancellationToken).ConfigureAwait(false);
            if (range.Reachability != Reachability.Reachable)
            {
                return range.Reachability;
            }

            if (range.Bytes.IsEmpty)
            {
                break;
            }

            FileLength = range.FileLength;
            Append(index, range.Bytes);
            progress?.Report((double)index.IndexedLength / Math.Max(target, 1));
        }

        return Reachability.Reachable;
    }

    private static void Append(LineIndex index, ReadOnlyMemory<byte> bytes)
    {
        // A UTF-16 file being written can end mid-character; the odd byte is read again next time.
        var whole = bytes.Length - (bytes.Length % index.Encoding.UnitSize);
        index.Append(bytes.Span[..whole]);
    }
}
