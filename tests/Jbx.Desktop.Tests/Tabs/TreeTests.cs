using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Jbx.Desktop.Features.FolderList;
using Jbx.Desktop.Features.Tabs;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Tests.Harness;
using Jbx.Testing;
using Xunit;

namespace Jbx.Desktop.Tests.Tabs;

public sealed class TreeTests : IDisposable
{
    private readonly TestShell _shell = new();
    private string _apiFolder = string.Empty;

    public void Dispose()
    {
        _shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Clicking_the_chevron_expands_and_collapses_without_moving_the_selection()
    {
        var (window, tab) = await OpenAsync();

        window.Click(window.ByName<Button>(Strings.FormatRows_ExpandNode("Billing Portal")));
        var expanded = Names(tab);
        window.Click(window.ByName<Button>(Strings.FormatRows_CollapseNode("Billing Portal")));

        Assert.Equal(["Billing Portal", "Billing API", "IIS Site Logs", "Orders Service"], expanded);
        Assert.Equal(["Billing Portal", "Orders Service"], Names(tab));
        Assert.Null(tab.SelectedRow);
    }

    [AvaloniaFact]
    public async Task Double_clicking_an_application_selects_it_but_does_not_expand_it()
    {
        var (window, tab) = await OpenAsync();

        window.DoubleClick(window.RowItem("Billing Portal"));

        Assert.Equal("Billing Portal", tab.SelectedRow?.Name);
        Assert.Equal(["Billing Portal", "Orders Service"], Names(tab));
    }

    [AvaloniaFact]
    public async Task Expanding_a_folder_lists_its_files_newest_first_and_enter_opens_one_in_the_viewer()
    {
        var (window, tab) = await OpenAsync();
        tab.FocusListCommand.Execute(null);
        window.Settle();

        window.Press(Key.Right);
        window.Press(Key.Right);
        window.Press(Key.Right);
        await Eventually.TrueAsync(() => tab.Rows.OfType<FileItemRowViewModel>().Count() == 2);
        window.Settle();
        window.Press(Key.Right);
        window.Press(Key.Enter);

        Assert.Equal(["Billing Portal", "Billing API", "api-2026-10-20.log", "api-2026-10-19.log", "IIS Site Logs", "Orders Service"], Names(tab));
        Assert.Equal("api-2026-10-20.log", tab.SelectedRow?.Name);
        Assert.Equal(_apiFolder + @"\api-2026-10-20.log", tab.SelectedDocument?.PathText);
        Assert.Empty(_shell.Shell.OpenedFiles);
    }

    [AvaloniaFact]
    public async Task Left_from_a_file_goes_to_its_folder_and_then_collapses_it()
    {
        var (window, tab) = await OpenWithFilesShownAsync();
        tab.SelectedRow = tab.Rows.OfType<FileItemRowViewModel>().First();
        tab.FocusListCommand.Execute(null);
        window.Settle();

        window.Press(Key.Left);
        var afterFirst = tab.SelectedRow?.Name;
        window.Press(Key.Left);

        Assert.Equal("Billing API", afterFirst);
        Assert.Empty(tab.Rows.OfType<FileItemRowViewModel>());
        Assert.False(Assert.IsType<FolderRowViewModel>(tab.SelectedRow).IsExpanded);
    }

    [AvaloniaFact]
    public async Task Double_clicking_a_file_opens_it_in_a_file_tab_of_its_environment()
    {
        var (window, tab) = await OpenWithFilesShownAsync();

        window.DoubleClick(window.RowItem("api-2026-10-19.log"));

        Assert.Equal("api-2026-10-19.log", Assert.Single(tab.Documents).Title);
        Assert.False(tab.IsBrowsing);
        Assert.Equal(_apiFolder + @"\api-2026-10-19.log", tab.Details.PathText);
        Assert.Empty(_shell.Shell.OpenedFiles);
    }

    [AvaloniaFact]
    public async Task Command_bar_buttons_follow_the_selection()
    {
        var (window, tab) = await OpenWithFilesShownAsync();
        var open = window.ByAutomationId<Button>("CommandBar.Open");
        var explorer = window.ByAutomationId<Button>("CommandBar.OpenInExplorer");
        var enabledWithNothingSelected = (open.IsEffectivelyEnabled, explorer.IsEffectivelyEnabled);
        tab.SelectedRow = tab.Rows.OfType<FolderRowViewModel>().First();
        window.Settle();
        var enabledOnAFolder = (open.IsEffectivelyEnabled, explorer.IsEffectivelyEnabled);
        tab.SelectedRow = tab.Rows.OfType<FileItemRowViewModel>().First();
        window.Settle();

        window.Click(explorer);
        window.Click(open);

        Assert.Equal((false, false), enabledWithNothingSelected);
        Assert.Equal((false, true), enabledOnAFolder);
        Assert.Equal([_apiFolder + @"\api-2026-10-20.log"], _shell.Shell.OpenedFolders);
        Assert.Equal("api-2026-10-20.log", tab.SelectedDocument?.Title);
    }

    [AvaloniaFact]
    public async Task Collapse_all_closes_every_node_and_keeps_the_selection_on_its_application()
    {
        var (window, tab) = await OpenWithFilesShownAsync();
        tab.SelectedRow = tab.Rows.OfType<FileItemRowViewModel>().First();
        window.Settle();

        window.Click(window.ByAutomationId<Button>("CommandBar.CollapseAll"));

        Assert.Equal(["Billing Portal", "Orders Service"], Names(tab));
        Assert.Equal("Billing Portal", tab.SelectedRow?.Name);
    }

    private async Task<(Window Window, TabViewModel Tab)> OpenAsync()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        _shell.AddFoldersFor("dev", TestConfigs.FullExample());
        var document = TestConfigs.FullExample();
        var rows = _shell.Services.Rows(document, document.FindEnvironment("dev")!);
        _apiFolder = rows.Folders.Single(folder => folder.Folder.Label == "Billing API").Path!.Display;
        _shell.FileSystem.AddFile(_apiFolder, "api-2026-10-19.log", _shell.Time.GetUtcNow().AddDays(-1));
        _shell.FileSystem.AddFile(_apiFolder, "api-2026-10-20.log", _shell.Time.GetUtcNow());
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;
        await Eventually.TrueAsync(() => tab.ProgressText is null);
        window.Settle();
        return (window, tab);
    }

    private async Task<(Window Window, TabViewModel Tab)> OpenWithFilesShownAsync()
    {
        var (window, tab) = await OpenAsync();
        tab.ToggleExpandCommand.Execute(tab.Rows.Single(row => row.Name == "Billing Portal"));
        tab.ToggleExpandCommand.Execute(tab.Rows.Single(row => row.Name == "Billing API"));
        await Eventually.TrueAsync(() => tab.Rows.OfType<FileItemRowViewModel>().Count() == 2);
        window.Settle();
        return (window, tab);
    }

    private static List<string> Names(TabViewModel tab)
    {
        return [.. tab.Rows.Select(row => row.Name)];
    }
}
