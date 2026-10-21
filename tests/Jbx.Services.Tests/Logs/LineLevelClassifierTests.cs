using Jbx.Services.Logs;
using Xunit;

namespace Jbx.Services.Tests.Logs;

public sealed class LineLevelClassifierTests
{
    [Theory]
    [InlineData("2026-10-20T09:00:00Z INFO no error found", LineLevel.Info)]
    [InlineData("[10:00:01 ERR] Payment run failed", LineLevel.Error)]
    [InlineData("level=warn msg=\"slow share\"", LineLevel.Warning)]
    [InlineData("2026-10-20 09:00:00,001 DEBUG Billing.Api - entering", LineLevel.Debug)]
    [InlineData("2026-10-20 09:00:00 FATAL out of memory", LineLevel.Error)]
    [InlineData("GET /invoices 200 12ms", LineLevel.None)]
    [InlineData("INFORMATIONAL", LineLevel.None)]
    public void Takes_the_first_level_word_in_the_line(string line, LineLevel expected)
    {
        var level = LineLevelClassifier.Classify(line, LineLevel.None);

        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData("\tat Billing.Run() in Runner.cs:line 12")]
    [InlineData("System.TimeoutException: The operation has timed out.")]
    [InlineData("Caused by: java.io.IOException: share unavailable")]
    [InlineData("--- End of stack trace from previous location ---")]
    public void A_stack_trace_line_without_a_level_continues_the_error_before_it(string line)
    {
        var level = LineLevelClassifier.Classify(line, LineLevel.Error);

        Assert.Equal(LineLevel.Error, level);
    }

    [Fact]
    public void A_line_starting_error_colon_is_an_error_not_a_continuation()
    {
        var level = LineLevelClassifier.Classify("Error: disk full", LineLevel.Info);

        Assert.Equal(LineLevel.Error, level);
    }

    [Fact]
    public void An_ordinary_line_without_a_level_has_none()
    {
        var level = LineLevelClassifier.Classify("Billing.Run() returned", LineLevel.Error);

        Assert.Equal(LineLevel.None, level);
    }

    [Fact]
    public void Looks_only_at_the_start_of_a_very_long_line()
    {
        var line = new string('x', 300) + " ERROR late";

        var level = LineLevelClassifier.Classify(line, LineLevel.None);

        Assert.Equal(LineLevel.None, level);
    }
}
