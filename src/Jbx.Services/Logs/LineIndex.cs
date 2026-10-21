namespace Jbx.Services.Logs;

/// <summary>
/// Where each line of a log file starts, and its level, built from bytes as they are read. Holds offsets,
/// not text, so a large file costs about nine bytes a line. A last line without a newline yet is shown
/// as a provisional line that grows as the writer finishes it.
/// </summary>
public sealed class LineIndex
{
    public const int MaxLines = 20_000_000;

    private readonly Lock _lock = new();
    private readonly ChunkedOffsets _starts = new();
    private readonly List<LineLevel> _levels = [];
    private readonly byte[] _pendingHead;
    private int _pendingHeadLength;
    private long _indexedLength;

    public LineIndex(LogEncoding encoding)
    {
        Encoding = encoding;
        _starts.Add(encoding.PreambleLength);
        _indexedLength = encoding.PreambleLength;
        _pendingHead = new byte[LineLevelClassifier.CharsExamined * encoding.UnitSize];
    }

    public LogEncoding Encoding { get; }

    public bool IsCapped { get; private set; }

    /// <summary>Lines with a newline, plus the provisional last line when there is one.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _levels.Count + (HasPendingLine ? 1 : 0);
            }
        }
    }

    /// <summary>Lines with a newline; their levels are final.</summary>
    public int CompleteCount
    {
        get
        {
            lock (_lock)
            {
                return _levels.Count;
            }
        }
    }

    public long IndexedLength
    {
        get
        {
            lock (_lock)
            {
                return _indexedLength;
            }
        }
    }

    private bool HasPendingLine => !IsCapped && _indexedLength > _starts.Last;

    /// <summary>The byte range of a line, newline included, and its level; a provisional line has no level yet.</summary>
    public LineSpan SpanOf(int line)
    {
        lock (_lock)
        {
            var start = _starts[line];
            var isComplete = line < _levels.Count;
            var end = isComplete ? _starts[line + 1] : _indexedLength;
            var level = isComplete ? _levels[line] : LineLevel.None;
            return new LineSpan(start, end, level, isComplete);
        }
    }

    public LineLevel LevelOf(int line)
    {
        lock (_lock)
        {
            return line < _levels.Count ? _levels[line] : LineLevel.None;
        }
    }

    /// <summary>The levels of the finished lines from <paramref name="from"/> on, copied under one lock for a fast scan.</summary>
    public LineLevel[] CopyLevels(int from)
    {
        lock (_lock)
        {
            var count = Math.Max(0, _levels.Count - from);
            var levels = new LineLevel[count];
            _levels.CopyTo(Math.Min(from, _levels.Count), levels, 0, count);
            return levels;
        }
    }

    public LineLevel LevelBefore(int line)
    {
        lock (_lock)
        {
            return line > 0 && line - 1 < _levels.Count ? _levels[line - 1] : LineLevel.None;
        }
    }

    /// <summary>Indexes the next bytes of the file, which must start where the previous call ended.</summary>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        lock (_lock)
        {
            var position = 0;
            while (!IsCapped)
            {
                var lineEnd = Encoding.FindLineEnd(bytes, position);
                if (lineEnd < 0)
                {
                    KeepHead(bytes[position..]);
                    break;
                }

                KeepHead(bytes[position..lineEnd]);
                CompleteLine(_indexedLength + lineEnd);
                position = lineEnd;
            }

            _indexedLength += bytes.Length;
        }
    }

    private void CompleteLine(long nextStart)
    {
        var head = Encoding.Decode(_pendingHead.AsSpan(0, _pendingHeadLength));
        var previous = _levels.Count > 0 ? _levels[^1] : LineLevel.None;
        _levels.Add(LineLevelClassifier.Classify(head, previous));
        _starts.Add(nextStart);
        _pendingHeadLength = 0;
        IsCapped = _levels.Count >= MaxLines;
    }

    private void KeepHead(ReadOnlySpan<byte> bytes)
    {
        var room = _pendingHead.Length - _pendingHeadLength;
        if (room <= 0)
        {
            return;
        }

        var taken = bytes[..Math.Min(room, bytes.Length)];
        taken.CopyTo(_pendingHead.AsSpan(_pendingHeadLength));
        _pendingHeadLength += taken.Length;
    }
}
