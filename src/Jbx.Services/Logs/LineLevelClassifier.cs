namespace Jbx.Services.Logs;

/// <summary>
/// Finds a log line's level from the first level word in it, so "INFO no error found" is Info. A line
/// that continues the one before it, as stack trace lines do, keeps that line's level.
/// </summary>
public static class LineLevelClassifier
{
    public const int CharsExamined = 256;

    private static readonly Dictionary<string, LineLevel>.AlternateLookup<ReadOnlySpan<char>> Words = CreateWords();

    public static LineLevel Classify(ReadOnlySpan<char> line, LineLevel previous)
    {
        // A stack trace can contain level words ("--- End of stack trace ---"); it still belongs to its error.
        if (previous != LineLevel.None && IsContinuation(line))
        {
            return previous;
        }

        var examined = line.Length > CharsExamined ? line[..CharsExamined] : line;
        var start = -1;
        for (var index = 0; index <= examined.Length; index++)
        {
            var isLetter = index < examined.Length && char.IsAsciiLetter(examined[index]);
            if (isLetter && start < 0)
            {
                start = index;
            }
            else if (!isLetter && start >= 0)
            {
                if (Words.TryGetValue(examined[start..index], out var level))
                {
                    return level;
                }

                start = -1;
            }
        }

        return LineLevel.None;
    }

    /// <summary>Indented lines, and exception headers such as "System.TimeoutException:" that follow an error line.</summary>
    private static bool IsContinuation(ReadOnlySpan<char> line)
    {
        if (line.IsEmpty)
        {
            return false;
        }

        if (line[0] is ' ' or '\t' || line.StartsWith("Caused by", StringComparison.Ordinal) || line.StartsWith("Traceback", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal))
        {
            return true;
        }

        // An exception type, such as "System.TimeoutException:" or "TypeError:", but not a plain "Error:".
        var end = line.IndexOfAny(':', ' ');
        var firstWord = end < 0 ? line : line[..end];
        return firstWord.EndsWith("Exception", StringComparison.Ordinal)
            || (firstWord.EndsWith("Error", StringComparison.Ordinal) && firstWord.Length > "Error".Length);
    }

    private static Dictionary<string, LineLevel>.AlternateLookup<ReadOnlySpan<char>> CreateWords()
    {
        var words = new Dictionary<string, LineLevel>(StringComparer.Ordinal);
        Add(words, LineLevel.Error, "ERROR", "ERR", "EROR", "FATAL", "FTL", "CRITICAL", "CRIT", "EMERG", "ALERT", "SEVERE", "Error", "Fatal", "Critical", "Severe", "error", "fatal", "critical");
        Add(words, LineLevel.Warning, "WARN", "WARNING", "WRN", "Warn", "Warning", "warn", "warning");
        Add(words, LineLevel.Info, "INFO", "INF", "INFORMATION", "NOTICE", "Info", "Information", "Notice", "info", "information", "notice");
        Add(words, LineLevel.Debug, "DEBUG", "DBG", "TRACE", "TRC", "VERBOSE", "VRB", "FINE", "FINER", "FINEST", "Debug", "Trace", "Verbose", "debug", "trace", "verbose");
        return words.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    private static void Add(Dictionary<string, LineLevel> words, LineLevel level, params string[] spellings)
    {
        foreach (var spelling in spellings)
        {
            words[spelling] = level;
        }
    }
}
