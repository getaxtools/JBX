using Jbx.Services.Search;
using Xunit;

namespace Jbx.Services.Tests.Search;

public sealed class FuzzyRankingTests
{
    private static readonly string[] Commands = ["Close tab", "New tab", "Reload config", "Go to line", "Next tab"];

    [Fact]
    public void Keeps_the_given_order_when_nothing_is_typed()
    {
        var ranked = FuzzyRanking.Rank(Commands, command => command, "  ");

        Assert.Equal(Commands, ranked);
    }

    [Fact]
    public void Puts_a_prefix_match_above_a_scattered_one_and_drops_non_matches()
    {
        var ranked = FuzzyRanking.Rank(Commands, command => command, "ne");

        Assert.Equal(["New tab", "Next tab", "Go to line"], ranked);
    }
}
