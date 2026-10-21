using Jbx.Services.Rows;

namespace Jbx.Desktop.Features.Tabs;

internal sealed record SortOption(RowSort Sort, string Label)
{
    public override string ToString()
    {
        return Label;
    }
}
