using Jbx.Services.Logs;
using Xunit;

namespace Jbx.Services.Tests.Logs;

public sealed class LogMatcherTests
{
    [Fact]
    public void Finds_plain_text_ignoring_case_unless_asked_not_to()
    {
        var ignoringCase = Matcher(new LogQuery { Text = "timeout" });
        var matchingCase = Matcher(new LogQuery { Text = "timeout", MatchCase = true });

        Assert.True(ignoringCase.IsMatch("Share TIMEOUT after 3 s", LineLevel.Warning));
        Assert.False(matchingCase.IsMatch("Share TIMEOUT after 3 s", LineLevel.Warning));
    }

    [Fact]
    public void Finds_every_occurrence_for_highlighting()
    {
        var matcher = Matcher(new LogQuery { Text = "ab" });

        var spans = matcher.FindAll("ab-AB-xab");

        Assert.Equal([new MatchSpan(0, 2), new MatchSpan(3, 2), new MatchSpan(7, 2)], spans);
    }

    [Fact]
    public void Matches_patterns_and_reports_where_a_broken_one_goes_wrong()
    {
        var pattern = Matcher(new LogQuery { Text = @"invoice-\d+", UseRegex = true });
        var broken = LogMatcher.Create(new LogQuery { Text = "invoice-(", UseRegex = true });
        var lookBehind = LogMatcher.Create(new LogQuery { Text = "(?<=a)b", UseRegex = true });

        Assert.Equal([new MatchSpan(5, 13)], pattern.FindAll("paid invoice-20412 today"));
        Assert.Equal((PatternProblem.Invalid, 9), (broken.Problem, broken.ProblemOffset));
        Assert.Equal(PatternProblem.Unsupported, lookBehind.Problem);
    }

    [Fact]
    public void Keeps_only_the_chosen_levels_and_every_level_when_none_is_chosen()
    {
        var errorsAndWarnings = Matcher(new LogQuery { Levels = [LineLevel.Error, LineLevel.Warning] });
        var everything = Matcher(LogQuery.Everything);

        Assert.True(errorsAndWarnings.IsMatch("anything", LineLevel.Warning));
        Assert.False(errorsAndWarnings.IsMatch("anything", LineLevel.Info));
        Assert.True(everything.IsMatch("anything", LineLevel.None));
    }

    private static LogMatcher Matcher(LogQuery query)
    {
        return LogMatcher.Create(query).Matcher!;
    }
}
