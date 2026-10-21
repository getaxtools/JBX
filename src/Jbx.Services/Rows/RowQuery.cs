using System.Collections.Immutable;

namespace Jbx.Services.Rows;

public sealed record RowQuery
{
    public static RowQuery Everything { get; } = new();

    public string SearchText { get; init; } = string.Empty;

    public ImmutableHashSet<string> FolderTypes { get; init; } = [];

    public ImmutableHashSet<string> Tags { get; init; } = [];

    public ImmutableHashSet<string> Owners { get; init; } = [];

    public RowSort Sort { get; init; } = RowSort.Name;

    public bool IsSearching => SearchText.Trim().Length > 0;

    public bool IsFiltered => !FolderTypes.IsEmpty || !Tags.IsEmpty || !Owners.IsEmpty;
}
