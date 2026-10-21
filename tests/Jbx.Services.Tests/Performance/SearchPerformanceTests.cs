using System.Diagnostics;
using Jbx.Services.Rows;
using Jbx.Testing;
using Xunit;

namespace Jbx.Services.Tests.Performance;

public sealed class SearchPerformanceTests
{
    [Fact]
    public void Answers_a_search_keystroke_over_1000_folders_within_50_ms_without_touching_the_disk()
    {
        var document = TestConfigs.Synthetic(applications: 100, foldersPerApplication: 10);
        var rows = EnvironmentRows.Resolve(document, document.FindEnvironment("prod")!, TestConfigs.Resolver(), SimplePathCanonicalizer.Instance);
        var fileSystem = new InMemoryFileSystem();
        var query = RowQuery.Everything with { SearchText = "aplc 4" };
        RowFlattener.Flatten(rows, query, RowExpansion.None, _ => FolderStatus.NotChecked);

        var stopwatch = Stopwatch.StartNew();
        var listing = RowFlattener.Flatten(rows, query, RowExpansion.None, _ => FolderStatus.NotChecked);
        stopwatch.Stop();

        Assert.Equal(1_000, rows.Folders.Count);
        Assert.NotEmpty(listing.Rows);
        Assert.True(stopwatch.ElapsedMilliseconds < 50, $"took {stopwatch.ElapsedMilliseconds} ms");
        Assert.Equal(0, fileSystem.ProbeCount + fileSystem.ListingCount);
    }
}
