using System.Text.RegularExpressions;

namespace Jbx.Services.Logs;

/// <summary>
/// Decides whether a line matches a query and where. Patterns run without backtracking, so a pattern
/// typed into the search box cannot stall on a hostile line however long the file is.
/// </summary>
public sealed class LogMatcher
{
    public const int MaxHighlightsPerLine = 64;

    private readonly LogQuery _query;
    private readonly Regex? _regex;

    private LogMatcher(LogQuery query, Regex? regex)
    {
        _query = query;
        _regex = regex;
    }

    public LogQuery Query => _query;

    public static LogMatcherResult Create(LogQuery query)
    {
        if (!query.HasText || !query.UseRegex)
        {
            return new LogMatcherResult(new LogMatcher(query, null), PatternProblem.None);
        }

        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (query.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            return new LogMatcherResult(new LogMatcher(query, new Regex(query.Text, options)), PatternProblem.None);
        }
        catch (RegexParseException exception)
        {
            return new LogMatcherResult(null, PatternProblem.Invalid) { ProblemOffset = exception.Offset };
        }
        catch (NotSupportedException)
        {
            return new LogMatcherResult(null, PatternProblem.Unsupported);
        }
    }

    public bool MatchesLevel(LineLevel level)
    {
        return !_query.HasLevels || _query.Levels.Contains(level);
    }

    public bool IsMatch(string text, LineLevel level)
    {
        if (!MatchesLevel(level))
        {
            return false;
        }

        if (!_query.HasText)
        {
            return true;
        }

        return _regex?.IsMatch(text) ?? text.Contains(_query.Text, Comparison);
    }

    public IReadOnlyList<MatchSpan> FindAll(string text)
    {
        if (!_query.HasText)
        {
            return [];
        }

        var spans = new List<MatchSpan>();
        if (_regex is not null)
        {
            foreach (var match in _regex.EnumerateMatches(text))
            {
                if (match.Length > 0)
                {
                    spans.Add(new MatchSpan(match.Index, match.Length));
                }

                if (spans.Count == MaxHighlightsPerLine)
                {
                    break;
                }
            }

            return spans;
        }

        var from = 0;
        while (spans.Count < MaxHighlightsPerLine)
        {
            var found = text.IndexOf(_query.Text, from, Comparison);
            if (found < 0)
            {
                break;
            }

            spans.Add(new MatchSpan(found, _query.Text.Length));
            from = found + _query.Text.Length;
        }

        return spans;
    }

    private StringComparison Comparison => _query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
