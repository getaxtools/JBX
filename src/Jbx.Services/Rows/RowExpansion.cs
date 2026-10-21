namespace Jbx.Services.Rows;

/// <summary>Which tree nodes are open, and the files already listed for open folders.</summary>
public sealed record RowExpansion
{
    public static RowExpansion None { get; } = new();

    public IReadOnlySet<string> ApplicationIds { get; init; } = new HashSet<string>();

    public IReadOnlySet<string> FolderKeys { get; init; } = new HashSet<string>();

    public Func<FolderEntry, FolderFiles?> FilesOf { get; init; } = _ => null;
}
