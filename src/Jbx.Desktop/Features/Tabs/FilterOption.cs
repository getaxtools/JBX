namespace Jbx.Desktop.Features.Tabs;

/// <summary>One choice in a filter list. A null <see cref="Value"/> is "All".</summary>
internal sealed record FilterOption(string? Value, string Label)
{
    public override string ToString()
    {
        return Label;
    }
}
