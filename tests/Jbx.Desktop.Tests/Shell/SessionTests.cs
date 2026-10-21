using Avalonia.Headless.XUnit;
using Jbx.Desktop.Features.LogViewer;
using Jbx.Desktop.Tests.Harness;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

public sealed class SessionTests : IDisposable
{
    private const string LogFolder = @"\\fs01.contoso.test\AppLogs\prod\BillingPortal\Api\logs";

    private readonly TestShell _first = new();
    private readonly TestShell _second = new();

    public void Dispose()
    {
        _first.Dispose();
        _second.Dispose();
    }

    [AvaloniaFact]
    public async Task Reopens_the_environment_tabs_and_files_that_were_open_last_time()
    {
        var log = _first.Paths.From(LogFolder + @"\api.log");
        _first.AddConfig();
        _first.RememberConfig();
        var firstWindow = _first.CreateWindow();
        firstWindow.Show();
        await _first.ViewModel.InitializeAsync();
        _first.Interaction.EnvironmentToPick = "prod";
        await _first.ViewModel.NewTabCommand.ExecuteAsync(null);
        _first.ViewModel.SelectedTab!.OpenInViewer(new LogTarget(log, "api.log", "Billing API"));
        await firstWindow.SettleUntilAsync(() => _first.SettingsStore.Content?.Contains("api.log", StringComparison.Ordinal) == true);
        firstWindow.Close();

        _second.SettingsStore.Content = _first.SettingsStore.Content;
        _second.AddConfig();
        var secondWindow = _second.CreateWindow();
        secondWindow.Show();
        await _second.ViewModel.InitializeAsync();

        Assert.Equal(["dev", "prod"], _second.ViewModel.Tabs.Select(tab => tab.Environment.Id));
        Assert.Equal("prod", _second.ViewModel.SelectedTab?.Environment.Id);
        Assert.Equal(log, _second.ViewModel.SelectedTab?.SelectedDocument?.Target.Path);
    }

    [AvaloniaFact]
    public async Task Starts_with_one_tab_for_the_last_environment_when_nothing_was_open()
    {
        _first.AddConfig();
        _first.RememberConfig();

        await _first.ViewModel.InitializeAsync();

        Assert.Single(_first.ViewModel.Tabs);
    }
}
