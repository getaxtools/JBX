namespace Jbx.Desktop.Infrastructure;

/// <summary>A request to show one item. A class, not a record: asking twice for the same index must still scroll.</summary>
internal sealed class ScrollTarget(int index)
{
    public int Index { get; } = index;
}
