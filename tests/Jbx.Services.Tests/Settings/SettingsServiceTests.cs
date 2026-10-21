using Jbx.Core.Diagnostics;
using Jbx.Services.Settings;
using Jbx.Testing;
using Xunit;

namespace Jbx.Services.Tests.Settings;

public sealed class SettingsServiceTests
{
    private readonly InMemorySettingsFileStore _store = new();
    private readonly RecordingDiagnosticLog _log = new();

    [Fact]
    public async Task Starts_from_defaults_on_first_run()
    {
        using var service = new SettingsService(_store, _log);

        var outcome = await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SettingsLoadStatus.FirstRun, outcome.Status);
        Assert.Null(service.Current.LastConfigPath);
        Assert.Empty(service.Current.Favourites);
    }

    [Fact]
    public async Task Reads_back_everything_it_wrote()
    {
        var favourite = new Favourite(@"\\fs01.contoso.test\AppLogs\prod\Billing", "Billing API (Production)", FavouriteKind.Folder)
        {
            Origin = new FavouriteOrigin("billing-portal", "Billing API", "prod"),
        };
        using (var writer = new SettingsService(_store, _log))
        {
            await writer.UpdateAsync(
                settings => settings with
                {
                    LastConfigPath = @"\\fs01.contoso.test\config\jbx.yaml",
                    RecentConfigPaths = [@"\\fs01.contoso.test\config\jbx.yaml", @"C:\configs\team.json"],
                    Favourites = [favourite],
                    Window = new WindowPlacement { Width = 1400, IsMaximized = true },
                },
                TestContext.Current.CancellationToken);
        }

        using var reader = new SettingsService(_store, _log);
        var outcome = await reader.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SettingsLoadStatus.Loaded, outcome.Status);
        Assert.Equal(@"\\fs01.contoso.test\config\jbx.yaml", reader.Current.LastConfigPath);
        Assert.Equal(2, reader.Current.RecentConfigPaths.Length);
        Assert.Equal(favourite, Assert.Single(reader.Current.Favourites));
        Assert.Equal(1400, reader.Current.Window.Width);
        Assert.True(reader.Current.Window.IsMaximized);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\": 99}")]
    public async Task Sets_aside_a_corrupt_or_newer_file_and_starts_from_defaults(string content)
    {
        _store.Content = content;
        using var service = new SettingsService(_store, _log);

        var outcome = await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SettingsLoadStatus.SetAside, outcome.Status);
        Assert.Equal("settings.corrupt-20261020090000.json", outcome.SetAsideFileName);
        Assert.Equal(content, Assert.Single(_store.SetAsideContents));
        Assert.Equal(UserSettings.Defaults, service.Current);
        Assert.True(_log.Contains(EventCode.SettingsSetAside));
    }

    [Fact]
    public async Task Reports_a_failed_write_without_throwing_and_keeps_the_change_in_memory()
    {
        _store.IsWritable = false;
        using var service = new SettingsService(_store, _log);

        var written = await service.UpdateAsync(settings => settings with { LastConfigPath = @"C:\configs\jbx.yaml" }, TestContext.Current.CancellationToken);

        Assert.False(written);
        Assert.Equal(@"C:\configs\jbx.yaml", service.Current.LastConfigPath);
        Assert.True(_log.Contains(EventCode.SettingsWriteFailed));
    }

    [Fact]
    public async Task Keeps_only_the_ten_most_recent_configs()
    {
        using var service = new SettingsService(_store, _log);
        var paths = Enumerable.Range(1, 15).Select(index => $@"C:\configs\team-{index}.yaml").ToList();

        await service.UpdateAsync(settings => settings with { RecentConfigPaths = [.. paths] }, TestContext.Current.CancellationToken);

        Assert.Equal(paths.Take(UserSettings.MaxRecentConfigs), service.Current.RecentConfigPaths);
    }

    [Fact]
    public async Task Fills_in_defaults_for_fields_missing_from_an_older_file()
    {
        _store.Content = "{\"schemaVersion\": 1, \"lastConfigPath\": \"C:\\\\configs\\\\jbx.yaml\"}";
        using var service = new SettingsService(_store, _log);

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(@"C:\configs\jbx.yaml", service.Current.LastConfigPath);
        Assert.Empty(service.Current.Favourites);
        Assert.Equal(new WindowPlacement(), service.Current.Window);
    }
}
