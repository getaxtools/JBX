using Jbx.Core.Diagnostics;
using Jbx.Platform;
using Jbx.Services.Probing;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Services.Tests.Probing;

public sealed class FolderListingServiceTests : IDisposable
{
    private const string Folder = @"\\fs01.contoso.test\AppLogs\prod\Billing\Api\logs";
    private static readonly DateTimeOffset Today = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDiagnosticLog _log = new();
    private readonly InMemoryFileSystem _fileSystem = new();
    private readonly FileSystemGate _gate;
    private readonly FolderListingService _listing;

    public FolderListingServiceTests()
    {
        _gate = new FileSystemGate(_time);
        _listing = new FolderListingService(_fileSystem, _gate, _log);
    }

    public void Dispose()
    {
        _fileSystem.ReleaseBlackholed();
        _gate.Dispose();
    }

    [Fact]
    public async Task Lists_the_newest_files_first_and_says_when_more_exist_than_it_shows()
    {
        for (var index = 0; index < FolderListingService.MaxListingEntries + 10; index++)
        {
            _fileSystem.AddFile(Folder, $"api-{index:D5}.log", Today.AddMinutes(index));
        }

        var listing = await _listing.ListAsync(SimplePathCanonicalizer.Instance.From(Folder), null, TestContext.Current.CancellationToken);

        Assert.True(listing.IsTruncated);
        Assert.Equal(FolderListingService.MaxListingEntries, listing.NewestFirst.Count);
        Assert.Equal(FolderListingService.MaxListingEntries + 10, listing.MatchingFileCount);
        Assert.Equal("api-05009.log", listing.NewestFirst[0].Name);
        Assert.True(_log.Contains(EventCode.ListingTruncated));
    }

    [Fact]
    public async Task Lists_only_files_matching_the_log_file_pattern()
    {
        _fileSystem.AddFile(Folder, "api-1.log", Today);
        _fileSystem.AddFile(Folder, "api-1.log.zip", Today);
        _fileSystem.AddFile(Folder, "readme.txt", Today);

        var listing = await _listing.ListAsync(SimplePathCanonicalizer.Instance.From(Folder), "api-*.log", TestContext.Current.CancellationToken);

        Assert.Equal(["api-1.log"], listing.NewestFirst.Select(file => file.Name));
        Assert.False(listing.IsTruncated);
    }

    [Fact]
    public async Task Times_out_a_listing_of_a_blackholed_share_after_five_seconds()
    {
        _fileSystem.AddFolder(Folder, FolderBehaviour.Blackholed);

        var pending = _listing.ListAsync(SimplePathCanonicalizer.Instance.From(Folder), null, TestContext.Current.CancellationToken);
        _time.Advance(FolderListingService.ListingTimeout);
        var listing = await pending;

        Assert.Equal(Reachability.Timeout, listing.Reachability);
        Assert.Empty(listing.NewestFirst);
    }

    [Fact]
    public async Task Reports_a_denied_folder_as_denied_rather_than_empty()
    {
        _fileSystem.AddFolder(Folder, FolderBehaviour.Denied);

        var listing = await _listing.ListAsync(SimplePathCanonicalizer.Instance.From(Folder), null, TestContext.Current.CancellationToken);

        Assert.Equal(Reachability.Denied, listing.Reachability);
    }
}
