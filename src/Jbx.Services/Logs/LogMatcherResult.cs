namespace Jbx.Services.Logs;

/// <summary>A matcher, or why the pattern could not become one and where it went wrong.</summary>
public sealed record LogMatcherResult(LogMatcher? Matcher, PatternProblem Problem)
{
    public int ProblemOffset { get; init; }
}
