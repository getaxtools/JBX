namespace Jbx.Platform;

/// <summary>
/// The newest files of a folder, at most the requested number. <see cref="IsTruncated"/> says more exist,
/// so a capped list is never mistaken for a complete one.
/// </summary>
public sealed record FolderListing(
    Reachability Reachability,
    IReadOnlyList<FileEntry> NewestFirst,
    int MatchingFileCount,
    bool IsTruncated)
{
    public int? ErrorCode { get; init; }

    public static FolderListing Failed(ProbeResult probe)
    {
        return new FolderListing(probe.Reachability, [], 0, false) { ErrorCode = probe.ErrorCode };
    }
}
