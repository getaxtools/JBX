using Jbx.Services.Favourites;
using Jbx.Services.Settings;
using Jbx.Testing;
using Xunit;

namespace Jbx.Services.Tests.Favourites;

public sealed class FavouritesServiceTests : IDisposable
{
    private const string Share = @"\\fs01.contoso.test\AppLogs";

    private readonly SimplePathCanonicalizer _paths = new(new Dictionary<char, string> { ['Z'] = Share });
    private readonly InMemorySettingsFileStore _store = new();
    private readonly SettingsService _settings;
    private readonly FavouritesService _favourites;

    public FavouritesServiceTests()
    {
        _settings = new SettingsService(_store, new RecordingDiagnosticLog());
        _favourites = new FavouritesService(_settings, _paths);
    }

    public void Dispose()
    {
        _settings.Dispose();
    }

    [Fact]
    public async Task Starring_twice_leaves_no_favourite()
    {
        var folder = _paths.From(Share + @"\prod\Billing");
        var request = new FavouriteRequest(folder, "Billing API (Production)", FavouriteKind.Folder);

        var starred = await _favourites.ToggleAsync(request, TestContext.Current.CancellationToken);
        var unstarred = await _favourites.ToggleAsync(request, TestContext.Current.CancellationToken);

        Assert.True(starred);
        Assert.False(unstarred);
        Assert.Empty(_favourites.All);
    }

    [Fact]
    public async Task Announces_a_change_only_when_the_favourites_themselves_change()
    {
        var raised = 0;
        _favourites.Changed += (_, _) => raised++;

        await _settings.UpdateAsync(current => current with { LastEnvironmentId = "prod" }, TestContext.Current.CancellationToken);
        var afterUnrelatedChange = raised;
        await _favourites.AddAsync(@"Z:\Logs", FavouriteKind.Folder, TestContext.Current.CancellationToken);

        Assert.Equal((0, 1), (afterUnrelatedChange, raised));
    }

    [Fact]
    public async Task Stars_a_file_as_a_file_so_it_opens_rather_than_lists()
    {
        var file = _paths.From(Share + @"\prod\Billing\api-2026-10-20.log");

        await _favourites.ToggleAsync(new FavouriteRequest(file, "api-2026-10-20.log", FavouriteKind.File), TestContext.Current.CancellationToken);

        Assert.Equal(FavouriteKind.File, Assert.Single(_favourites.All).Favourite.Kind);
    }

    [Fact]
    public async Task Treats_a_mapped_drive_and_its_unc_path_as_one_favourite()
    {
        var first = await _favourites.AddAsync(@"Z:\Logs", FavouriteKind.Folder, TestContext.Current.CancellationToken);
        var second = await _favourites.AddAsync(Share + @"\LOGS\", FavouriteKind.Folder, TestContext.Current.CancellationToken);

        Assert.Equal(AddFavouriteStatus.Added, first);
        Assert.Equal(AddFavouriteStatus.AlreadyAFavourite, second);
        Assert.Single(_favourites.All);
    }

    [Fact]
    public async Task Refuses_a_path_that_is_not_absolute()
    {
        var status = await _favourites.AddAsync("logs", FavouriteKind.Folder, TestContext.Current.CancellationToken);

        Assert.Equal(AddFavouriteStatus.InvalidPath, status);
    }

    [Fact]
    public async Task Keeps_a_new_order_after_restart()
    {
        await AddAsync(@"C:\Logs\a", @"C:\Logs\b", @"C:\Logs\c");

        await _favourites.MoveAsync(_paths.From(@"C:\Logs\c"), 0, TestContext.Current.CancellationToken);
        using var restarted = new SettingsService(_store, new RecordingDiagnosticLog());
        await restarted.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([@"C:\Logs\c", @"C:\Logs\a", @"C:\Logs\b"], restarted.Current.Favourites.Select(favourite => favourite.Path));
    }

    [Fact]
    public async Task Puts_a_removed_favourite_back_where_it_was_on_undo()
    {
        await AddAsync(@"C:\Logs\a", @"C:\Logs\b", @"C:\Logs\c");

        var removed = await _favourites.RemoveAsync(_paths.From(@"C:\Logs\b"), TestContext.Current.CancellationToken);
        await _favourites.UndoRemoveAsync(removed!, TestContext.Current.CancellationToken);

        Assert.Equal([@"C:\Logs\a", @"C:\Logs\b", @"C:\Logs\c"], _favourites.All.Select(item => item.Path.Display));
    }

    [Fact]
    public async Task Renames_with_trimmed_text_and_ignores_a_blank_name()
    {
        await AddAsync(@"C:\Logs\a");
        var path = _paths.From(@"C:\Logs\a");

        await _favourites.RenameAsync(path, "  Billing logs  ", TestContext.Current.CancellationToken);
        await _favourites.RenameAsync(path, "   ", TestContext.Current.CancellationToken);

        Assert.Equal("Billing logs", Assert.Single(_favourites.All).Favourite.Label);
    }

    [Fact]
    public void Says_a_favourite_is_no_longer_in_the_config_when_its_folder_is_gone()
    {
        var document = TestConfigs.FullExample();
        var stillThere = new Favourite(Share, "x", FavouriteKind.Folder) { Origin = new FavouriteOrigin("billing-portal", "Billing API", "prod") };
        var gone = stillThere with { Origin = new FavouriteOrigin("billing-portal", "Retired API", "prod") };
        var handAdded = new Favourite(Share, "x", FavouriteKind.Folder);

        Assert.True(FavouritesService.IsInConfig(stillThere, document));
        Assert.False(FavouritesService.IsInConfig(gone, document));
        Assert.True(FavouritesService.IsInConfig(handAdded, document));
    }

    private async Task AddAsync(params string[] paths)
    {
        foreach (var path in paths)
        {
            await _favourites.AddAsync(path, FavouriteKind.Folder, TestContext.Current.CancellationToken);
        }
    }
}
