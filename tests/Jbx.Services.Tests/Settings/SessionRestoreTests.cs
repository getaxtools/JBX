using Jbx.Services.Settings;
using Jbx.Testing;
using Xunit;

namespace Jbx.Services.Tests.Settings;

public sealed class SessionRestoreTests
{
    private const string ConfigPath = @"\\fs01.contoso.test\config\jbx.yaml";
    private const string LogPath = @"\\fs01.contoso.test\AppLogs\prod\BillingPortal\Api\logs\api.log";

    private readonly SimplePathCanonicalizer _paths = SimplePathCanonicalizer.Instance;

    [Fact]
    public void Reopens_tabs_whose_environment_still_exists_and_keeps_the_selection_on_the_right_tab()
    {
        var session = new SavedSession
        {
            ConfigPath = ConfigPath,
            Tabs = [new SavedTab("retired"), new SavedTab("uat"), new SavedTab("prod") { Files = [new SavedFile(LogPath, "api.log", "Billing API")], SelectedFile = 0 }],
            SelectedTab = 2,
        };

        var plan = SessionRestore.Plan(session, _paths.From(ConfigPath), TestConfigs.FullExample(), _paths);

        Assert.Equal(["uat", "prod"], plan.Tabs.Select(tab => tab.Environment.Id));
        Assert.Equal(1, plan.SelectedTab);
        Assert.Equal((LogPath, 0), (Assert.Single(plan.Tabs[1].Files).Path.Display, plan.Tabs[1].SelectedFile));
    }

    [Fact]
    public void Restores_nothing_for_a_different_config()
    {
        var session = new SavedSession { ConfigPath = @"\\fs01.contoso.test\config\other.yaml", Tabs = [new SavedTab("prod")] };

        var plan = SessionRestore.Plan(session, _paths.From(ConfigPath), TestConfigs.FullExample(), _paths);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public async Task Keeps_the_session_in_the_settings_file()
    {
        var store = new InMemorySettingsFileStore();
        var session = new SavedSession { ConfigPath = ConfigPath, Tabs = [new SavedTab("prod") { Files = [new SavedFile(LogPath, "api.log", "Billing API")] }] };
        using (var settings = new SettingsService(store, new RecordingDiagnosticLog()))
        {
            await settings.LoadAsync(TestContext.Current.CancellationToken);
            await settings.UpdateAsync(current => current with { Session = session }, TestContext.Current.CancellationToken);
        }

        using var reloaded = new SettingsService(store, new RecordingDiagnosticLog());
        await reloaded.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("prod", Assert.Single(reloaded.Current.Session!.Tabs).EnvironmentId);
        Assert.Equal(LogPath, Assert.Single(reloaded.Current.Session.Tabs[0].Files).Path);
    }
}
