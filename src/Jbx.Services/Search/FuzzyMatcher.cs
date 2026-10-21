namespace Jbx.Services.Search;

/// <summary>
/// Subsequence matching with a score: "bilprt" finds "Billing Portal". Higher is better; null is no match.
/// Exact and prefix matches outrank scattered ones.
/// </summary>
public static class FuzzyMatcher
{
    private const int ExactBonus = 1000;
    private const int PrefixBonus = 500;
    private const int SubstringBonus = 250;
    private const int ConsecutiveBonus = 5;
    private const int WordStartBonus = 3;

    public static int? Score(string query, string text)
    {
        if (query.Length == 0)
        {
            return 0;
        }

        if (text.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return ExactBonus;
        }

        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return PrefixBonus + query.Length;
        }

        var substring = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        return substring >= 0 ? SubstringBonus - substring : SubsequenceScore(query, text);
    }

    private static int? SubsequenceScore(string query, string text)
    {
        var score = 0;
        var textIndex = 0;
        var previousMatch = -2;
        foreach (var wanted in query)
        {
            var found = IndexOfIgnoringCase(text, wanted, textIndex);
            if (found < 0)
            {
                return null;
            }

            score += found == previousMatch + 1 ? ConsecutiveBonus : 1;
            score += IsWordStart(text, found) ? WordStartBonus : 0;
            previousMatch = found;
            textIndex = found + 1;
        }

        return score;
    }

    private static int IndexOfIgnoringCase(string text, char wanted, int start)
    {
        var lower = char.ToLowerInvariant(wanted);
        for (var index = start; index < text.Length; index++)
        {
            if (char.ToLowerInvariant(text[index]) == lower)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsWordStart(string text, int index)
    {
        return index == 0 || !char.IsLetterOrDigit(text[index - 1]) || (char.IsUpper(text[index]) && char.IsLower(text[index - 1]));
    }
}
