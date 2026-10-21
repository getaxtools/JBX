namespace Jbx.Services.Logs;

/// <summary>What to scan for and from which line; progress, when given, hears the fraction done.</summary>
public sealed record LogScanRequest(LogMatcher Matcher, int FromLine)
{
    public IProgress<double>? Progress { get; init; }
}
