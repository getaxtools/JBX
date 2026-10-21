using Jbx.Platform;
using Jbx.Services.Rows;
using Jbx.Testing;
using Xunit;

namespace Jbx.Services.Tests.Rows;

public sealed class RowFlattenerTests
{
    private readonly EnvironmentRows _production;

    public RowFlattenerTests()
    {
        var document = TestConfigs.FullExample();
        _production = EnvironmentRows.Resolve(document, document.FindEnvironment("prod")!, TestConfigs.Resolver(), SimplePathCanonicalizer.Instance);
    }

    [Fact]
    public void Resolves_enabled_folders_for_the_environment_and_skips_disabled_ones()
    {
        var billing = _production.Applications.Single(application => application.Application.Id == "billing-portal");

        Assert.Equal(["Billing API", "IIS Site Logs"], billing.Folders.Select(folder => folder.Folder.Label));
        Assert.Equal(@"\\fs01.contoso.test\AppLogs\prod\BillingPortal\Api\logs", billing.Folders[0].Path?.Display);
    }

    [Fact]
    public void Shows_applications_collapsed_until_one_is_expanded()
    {
        var collapsed = Flatten(RowQuery.Everything);
        var expanded = Flatten(RowQuery.Everything, "billing-portal");

        Assert.All(collapsed.Rows, row => Assert.IsType<ApplicationRowItem>(row));
        Assert.Equal(["Billing Portal", "Billing API", "IIS Site Logs", "Orders Service"], expanded.Rows.Select(Describe));
        var secondFolder = Assert.IsType<FolderRowItem>(expanded.Rows[2]);
        Assert.Equal((2, 2), (secondFolder.PositionInApplication, secondFolder.FoldersInApplication));
    }

    [Fact]
    public void Finds_an_application_from_a_scattered_abbreviation_and_shows_its_folders()
    {
        var listing = Flatten(RowQuery.Everything with { SearchText = "bilprt" });

        Assert.Equal(["Billing Portal", "Billing API", "IIS Site Logs"], listing.Rows.Select(Describe));
    }

    [Fact]
    public void Shows_only_the_matching_folders_when_the_search_matches_a_folder_label()
    {
        var listing = Flatten(RowQuery.Everything with { SearchText = "scheduler" });

        Assert.Equal(["Orders Service", "Order Scheduler"], listing.Rows.Select(Describe));
        Assert.Equal(1, listing.MatchingFolderCount);
        Assert.Equal(3, listing.TotalFolderCount);
    }

    [Fact]
    public void Ranks_an_exact_name_above_a_partial_match()
    {
        var listing = Flatten(RowQuery.Everything with { SearchText = "orders service" });

        Assert.Equal("Orders Service", Describe(listing.Rows[0]));
    }

    [Fact]
    public void Shows_nothing_rather_than_everything_when_nothing_matches()
    {
        var listing = Flatten(RowQuery.Everything with { SearchText = "zzzz" });

        Assert.Empty(listing.Rows);
        Assert.Equal(0, listing.MatchingFolderCount);
    }

    [Fact]
    public void Filters_by_folder_type_tag_and_owner()
    {
        var webOnly = Flatten(RowQuery.Everything with { FolderTypes = ["web"] });
        var commerce = Flatten(RowQuery.Everything with { Tags = ["commerce"] });
        var finance = Flatten(RowQuery.Everything with { Owners = ["Finance Platform Team"] });

        Assert.Equal(["Billing Portal", "IIS Site Logs"], webOnly.Rows.Select(Describe));
        Assert.Equal(["Orders Service", "Order Scheduler"], commerce.Rows.Select(Describe));
        Assert.Equal("Billing Portal", Describe(Assert.IsType<ApplicationRowItem>(finance.Rows[0])));
    }

    [Fact]
    public void Puts_problems_first_when_sorting_by_status()
    {
        var statuses = new Dictionary<string, Reachability>
        {
            ["billing-portal/Billing API"] = Reachability.Reachable,
            ["billing-portal/IIS Site Logs"] = Reachability.Reachable,
            ["orders-service/Order Scheduler"] = Reachability.Timeout,
        };

        var listing = RowFlattener.Flatten(
            _production,
            RowQuery.Everything with { Sort = RowSort.Status },
            RowExpansion.None,
            entry => new FolderStatus(statuses[entry.Key], null));

        Assert.Equal("Orders Service", Describe(listing.Rows[0]));
    }

    [Fact]
    public void Lists_an_expanded_folders_files_directly_under_it()
    {
        var api = ApiFolder();
        var files = new FolderFiles(FolderFilesState.Listed, [FileIn(api, "api-2.log"), FileIn(api, "api-1.log")], 2);

        var listing = FlattenWithOpenFolder(api, files);

        Assert.Equal(["Billing Portal", "Billing API", "api-2.log", "api-1.log", "IIS Site Logs", "Orders Service"], listing.Rows.Select(Describe));
        Assert.True(Assert.IsType<FolderRowItem>(listing.Rows[1]).IsExpanded);
        var second = Assert.IsType<FileRowItem>(listing.Rows[3]);
        Assert.Equal((2, 2), (second.PositionInFolder, second.FilesInFolder));
    }

    [Fact]
    public void Says_loading_empty_or_failed_rather_than_showing_an_open_folder_with_nothing_under_it()
    {
        var api = ApiFolder();

        var loading = NoteUnder(api, null);
        var empty = NoteUnder(api, new FolderFiles(FolderFilesState.Listed, [], 0));
        var failed = NoteUnder(api, FolderFiles.From(new FolderListing(Reachability.Timeout, [], 0, false)));

        Assert.Equal(FolderNote.Loading, loading.Note);
        Assert.Equal(FolderNote.Empty, empty.Note);
        Assert.Equal((FolderNote.Failed, Reachability.Timeout), (failed.Note, failed.Reachability));
    }

    [Fact]
    public void Shows_at_most_a_hundred_files_and_says_how_many_more_exist()
    {
        var api = ApiFolder();
        var many = Enumerable.Range(0, 150).Select(index => FileIn(api, $"api-{index}.log")).ToList();

        var listing = FlattenWithOpenFolder(api, new FolderFiles(FolderFilesState.Listed, many, 4_000));

        Assert.Equal(RowFlattener.MaxFilesPerFolder, listing.Rows.OfType<FileRowItem>().Count());
        var more = Assert.Single(listing.Rows.OfType<FolderNoteRowItem>());
        Assert.Equal((FolderNote.MoreFiles, 100, 4_000), (more.Note, more.ShownFileCount, more.MatchingFileCount));
    }

    [Fact]
    public void Hides_an_open_folders_files_while_its_application_is_collapsed()
    {
        var api = ApiFolder();
        var expansion = new RowExpansion { FolderKeys = new HashSet<string> { api.Key } };

        var listing = RowFlattener.Flatten(_production, RowQuery.Everything, expansion, _ => FolderStatus.NotChecked);

        Assert.All(listing.Rows, row => Assert.IsType<ApplicationRowItem>(row));
    }

    private RowListing Flatten(RowQuery query, params string[] expanded)
    {
        return RowFlattener.Flatten(_production, query, new RowExpansion { ApplicationIds = expanded.ToHashSet() }, _ => FolderStatus.NotChecked);
    }

    private RowListing FlattenWithOpenFolder(FolderEntry openFolder, FolderFiles? files)
    {
        var expansion = new RowExpansion
        {
            ApplicationIds = new HashSet<string> { openFolder.Application.Id },
            FolderKeys = new HashSet<string> { openFolder.Key },
            FilesOf = folder => folder == openFolder ? files : null,
        };
        return RowFlattener.Flatten(_production, RowQuery.Everything, expansion, _ => FolderStatus.NotChecked);
    }

    private FolderNoteRowItem NoteUnder(FolderEntry folder, FolderFiles? files)
    {
        return Assert.Single(FlattenWithOpenFolder(folder, files).Rows.OfType<FolderNoteRowItem>());
    }

    private FolderEntry ApiFolder()
    {
        return _production.Folders.Single(folder => folder.Folder.Label == "Billing API");
    }

    private static FileEntry FileIn(FolderEntry folder, string name)
    {
        var path = SimplePathCanonicalizer.Instance.From(folder.Path!.Display + @"\" + name);
        return new FileEntry(name, path, 1024, new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
    }

    private static string Describe(RowItem row)
    {
        return row switch
        {
            ApplicationRowItem application => application.Entry.Application.DisplayName,
            FolderRowItem folder => folder.Entry.Folder.Label,
            FileRowItem file => file.File.Name,
            FolderNoteRowItem note => note.Note.ToString(),
            _ => throw new InvalidOperationException(),
        };
    }
}
