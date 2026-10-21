namespace Jbx.Services.Search;

/// <summary>Orders items by how well their text matches what was typed; with nothing typed, keeps their order.</summary>
public static class FuzzyRanking
{
    public static IReadOnlyList<T> Rank<T>(IEnumerable<T> items, Func<T, string> textOf, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return [.. items];
        }

        return items
            .Select((item, index) => (Item: item, Index: index, Score: FuzzyMatcher.Score(trimmed, textOf(item))))
            .Where(candidate => candidate.Score is not null)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => candidate.Item)
            .ToList();
    }
}
