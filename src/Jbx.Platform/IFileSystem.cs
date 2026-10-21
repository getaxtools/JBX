namespace Jbx.Platform;

/// <summary>
/// Read-only access to folders and files. Calls may block for a long time on an unreachable share
/// and cannot be cancelled once started; callers bound them (see the reachability probe). There is no write method.
/// </summary>
public interface IFileSystem
{
    Task<ProbeResult> ProbeFolderAsync(CanonicalPath folder);

    Task<ProbeResult> ProbeFileAsync(CanonicalPath file);

    Task<FolderListing> ListFilesAsync(CanonicalPath folder, ListingRequest request);

    /// <summary>Reads a whole file into memory, refusing files over <paramref name="maxBytes"/> without reading them.</summary>
    Task<FileContent> ReadFileAsync(CanonicalPath file, long maxBytes);

    /// <summary>Reads at most <paramref name="maxBytes"/> from <paramref name="offset"/>, sharing the file with whoever is writing it.</summary>
    Task<FileRange> ReadRangeAsync(CanonicalPath file, long offset, int maxBytes);

    Task<FileStat> StatFileAsync(CanonicalPath file);
}
