using Jbx.Core.Diagnostics;
using Jbx.Platform;

namespace Jbx.Services.Probing;

public sealed class FolderListingService(IFileSystem fileSystem, FileSystemGate gate, IDiagnosticLog diagnosticLog)
{
    public const int MaxListingEntries = 5_000;
    public static readonly TimeSpan ListingTimeout = TimeSpan.FromSeconds(5);

    public async Task<FolderListing> ListAsync(CanonicalPath folder, string? filePattern, CancellationToken cancellationToken)
    {
        var request = new ListingRequest(filePattern, MaxListingEntries);
        var outcome = await gate.RunAsync(() => fileSystem.ListFilesAsync(folder, request), ListingTimeout, cancellationToken).ConfigureAwait(false);
        var detail = new ResourceDetail(new ResourceItem(ResourceKind.Path, folder.Display));
        if (outcome.IsTimedOut || outcome.Value is null)
        {
            diagnosticLog.Write(new AppEvent(EventCode.ListingTimedOut, EventSeverity.Warning, new EventMeasure(MeasureKind.DurationMilliseconds, (long)ListingTimeout.TotalMilliseconds)), detail);
            return new FolderListing(Reachability.Timeout, [], 0, false);
        }

        if (outcome.Value.IsTruncated)
        {
            diagnosticLog.Write(
                new AppEvent(EventCode.ListingTruncated, EventSeverity.Information, new EventMeasure(MeasureKind.Count, outcome.Value.MatchingFileCount), new EventMeasure(MeasureKind.Limit, MaxListingEntries)),
                detail);
        }

        return outcome.Value;
    }
}
