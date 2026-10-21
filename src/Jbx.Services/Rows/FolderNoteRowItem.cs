using Jbx.Platform;

namespace Jbx.Services.Rows;

/// <summary>A line under an expanded folder that is not a file: loading, empty, failed, or "more files than shown".</summary>
public sealed record FolderNoteRowItem(FolderEntry Folder, FolderNote Note) : RowItem(Level: 3)
{
    public Reachability Reachability { get; init; } = Reachability.Reachable;

    public int ShownFileCount { get; init; }

    public int MatchingFileCount { get; init; }
}
