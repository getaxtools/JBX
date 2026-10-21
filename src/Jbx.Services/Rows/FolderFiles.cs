using Jbx.Platform;

namespace Jbx.Services.Rows;

/// <summary>The files an expanded folder shows in the tree: still loading, listed, or why they couldn't be.</summary>
public sealed record FolderFiles(FolderFilesState State, IReadOnlyList<FileEntry> NewestFirst, int MatchingFileCount)
{
    public static FolderFiles Loading { get; } = new(FolderFilesState.Loading, [], 0);

    public Reachability Reachability { get; init; } = Reachability.Reachable;

    public static FolderFiles From(FolderListing listing)
    {
        return listing.Reachability == Reachability.Reachable
            ? new FolderFiles(FolderFilesState.Listed, listing.NewestFirst, listing.MatchingFileCount)
            : new FolderFiles(FolderFilesState.Failed, [], 0) { Reachability = listing.Reachability };
    }

    public static FolderFiles Unresolved()
    {
        return new FolderFiles(FolderFilesState.Failed, [], 0) { Reachability = Reachability.Unknown };
    }
}
