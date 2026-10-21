namespace Jbx.Core.Config.Parsing;

/// <summary>
/// Caps how many nodes a document may expand to. Counting aliases alone is not enough:
/// nine aliases nested ten deep expand to a billion nodes.
/// </summary>
internal sealed class NodeBudget(int maxNodes)
{
    private int _remaining = maxNodes;

    public bool IsExhausted => _remaining < 0;

    public bool TrySpend()
    {
        _remaining--;
        return _remaining >= 0;
    }
}
