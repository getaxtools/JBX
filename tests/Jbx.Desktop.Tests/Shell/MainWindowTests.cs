using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Jbx.Desktop.Features.FolderList;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Tests.Harness;
using Jbx.Platform;
using Jbx.Testing;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

public sealed class MainWindowTests : IDisposable
{
    private readonly TestShell _shell = new();

    public void Dispose()
    {
        _shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Asks_for_a_config_on_first_run()
    {
        var window = _shell.CreateWindow();
        window.Show();

        await _shell.ViewModel.InitializeAsync();
        window.Settle();

        Assert.True(window.ByAutomationId<TextBlock>("Shell.EmptyState.Title").IsShown());
        Assert.True(window.ByAutomationId<Button>("Shell.OpenConfig").IsShown());
        Assert.Equal(Strings.Shell_Window_Title, window.Title);
    }

    [AvaloniaFact]
    public async Task Opens_the_remembered_config_in_a_tab_for_the_last_environment()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        await _shell.Settings.LoadAsync(TestContext.Current.CancellationToken);
        await _shell.Settings.UpdateAsync(settings => settings with { LastEnvironmentId = "uat" }, TestContext.Current.CancellationToken);
        var window = _shell.CreateWindow();
        window.Show();

        await _shell.ViewModel.InitializeAsync();
        window.Settle();

        var tab = Assert.Single(_shell.ViewModel.Tabs);
        Assert.Equal("UAT", tab.Header);
        Assert.Equal("UAT - JBX", window.Title);
        Assert.Equal(["Billing Portal", "Orders Service"], tab.Rows.Select(row => row.Name));
    }

    [AvaloniaFact]
    public async Task Marks_production_in_the_window_title_and_with_a_banner()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();

        _shell.ViewModel.SelectedTab!.Environment = _shell.Session.Document!.FindEnvironment("prod")!;
        window.Settle();

        Assert.Equal("Production - JBX", window.Title);
        Assert.True(_shell.ViewModel.SelectedTab.IsProduction);
    }

    [AvaloniaFact]
    public async Task Shows_why_a_remembered_config_cannot_be_opened_with_a_way_to_retry()
    {
        _shell.FileSystem.AddFolder(TestShell.ConfigFolder, FolderBehaviour.Denied);
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Show();

        await _shell.ViewModel.InitializeAsync();
        window.Settle();

        var message = window.ByAutomationId<TextBlock>("Shell.ConfigProblem");
        Assert.True(message.IsShown());
        Assert.Equal(Strings.FormatConfigOpen_Denied(TestShell.ConfigPath), message.Text);
    }

    [AvaloniaFact]
    public async Task Lists_every_problem_of_an_invalid_config_at_once()
    {
        _shell.AddConfig("config/several-errors.yaml");
        _shell.RememberConfig();

        await _shell.ViewModel.InitializeAsync();

        var problems = Assert.Single(_shell.Interaction.ShownProblems);
        Assert.Equal(5, problems.Problems.Count);
        Assert.Contains(problems.Problems, line => line.Message == Strings.FormatConfigError_UnknownToken("missingRoot"));
    }

    [AvaloniaFact]
    public async Task Expands_an_application_with_the_right_arrow_and_shows_folder_status_in_words()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        _shell.AddFoldersFor("dev", TestConfigs.FullExample());
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;
        await Eventually.TrueAsync(() => tab.ProgressText is null);
        tab.FocusListCommand.Execute(null);
        window.Settle();

        window.Press(Key.Right);

        Assert.Equal(["Billing Portal", "Billing API", "IIS Site Logs", "Orders Service"], tab.Rows.Select(row => row.Name));
        var folder = Assert.IsType<FolderRowViewModel>(tab.Rows[1]);
        Assert.Equal(Reachability.Reachable, folder.Status);
        Assert.Equal(Strings.Status_Reachable, folder.StatusLabel);
    }

    [AvaloniaFact]
    public async Task Typing_in_search_filters_the_list_and_says_when_nothing_matches()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;

        tab.SearchText = "ordr";
        window.Settle();
        var afterMatch = tab.Rows.Select(row => row.Name).ToList();
        tab.SearchText = "zzzz";
        window.Settle();

        Assert.Equal(["Orders Service", "Order Scheduler"], afterMatch);
        Assert.True(window.ByAutomationId<TextBlock>("Tab.NoResults").IsShown());
        Assert.Equal(Strings.FormatTab_NoResults("zzzz"), window.ByAutomationId<TextBlock>("Tab.NoResults").Text);
    }

    [AvaloniaFact]
    public async Task Ctrl_d_on_a_folder_adds_it_to_favourites()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;
        tab.SearchText = "scheduler";
        tab.SelectedRow = tab.Rows.OfType<FolderRowViewModel>().Single();
        tab.FocusListCommand.Execute(null);
        window.Settle();

        window.Press(Key.D, RawInputModifiers.Control);

        var favourite = Assert.Single(_shell.ViewModel.Favourites.Items);
        Assert.Contains("Order Scheduler", favourite.Label, StringComparison.Ordinal);
        Assert.True(Assert.IsType<FolderRowViewModel>(tab.SelectedRow).IsFavourite);
    }

    [AvaloniaFact]
    public async Task Closing_the_last_tab_offers_to_open_one()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();

        window.Press(Key.W, RawInputModifiers.Control);

        Assert.Empty(_shell.ViewModel.Tabs);
        Assert.True(_shell.ViewModel.IsReadyWithoutTabs);
        Assert.Equal(Strings.Shell_Window_Title, window.Title);
    }

    [AvaloniaFact]
    public async Task Gives_every_visible_control_a_name_for_screen_readers()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;
        tab.SearchText = "scheduler";
        tab.SelectedRow = tab.Rows.OfType<FolderRowViewModel>().Single();
        window.Settle();

        var unnamed = window.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control is Button or TextBox or ComboBox or ListBox && control.IsShown())
            .Where(control => string.IsNullOrWhiteSpace(ControlAutomationPeer.CreatePeerForElement(control).GetName()))
            .Select(control => $"{control.GetType().Name} in {control.GetVisualParent()?.GetType().Name}")
            .ToList();

        Assert.Empty(unnamed);
    }

    [AvaloniaFact]
    public void Corner_radii_follow_windows_11_rather_than_the_fluent_theme_defaults()
    {
        var window = _shell.CreateWindow();

        window.Show();

        Assert.True(window.TryFindResource("ControlCornerRadius", window.ActualThemeVariant, out var control));
        Assert.True(window.TryFindResource("OverlayCornerRadius", window.ActualThemeVariant, out var overlay));
        Assert.Equal(new CornerRadius(4), control);
        Assert.Equal(new CornerRadius(8), overlay);
    }

    [AvaloniaFact]
    public async Task Refuses_a_config_that_is_not_a_path_and_says_why()
    {
        _shell.Interaction.ConfigToPick = "jbx.yaml";

        await _shell.ViewModel.OpenConfigCommand.ExecuteAsync(null);

        Assert.Equal(Strings.FormatConfigOpen_InvalidPath("jbx.yaml"), _shell.ViewModel.ConfigProblemMessage);
    }
}
