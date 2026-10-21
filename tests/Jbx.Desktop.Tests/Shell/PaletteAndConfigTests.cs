using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Jbx.Desktop.Features.LogViewer;
using Jbx.Desktop.Features.Shell;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Tests.Harness;
using Jbx.Testing;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

public sealed class PaletteAndConfigTests : IDisposable
{
    private readonly TestShell _shell = new();

    public void Dispose()
    {
        _shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Ctrl_shift_p_finds_a_command_by_a_few_letters_and_enter_runs_it()
    {
        var window = await OpenAsync();
        var tab = _shell.ViewModel.SelectedTab!;

        window.Press(Key.P, RawInputModifiers.Control | RawInputModifiers.Shift);
        var filter = window.ByAutomationId<TextBox>("Palette.Filter");
        var focusedOnOpen = filter.IsFocused;
        _shell.ViewModel.Palette.FilterText = "switch prod";
        window.Settle();
        window.Press(Key.Enter);

        Assert.True(focusedOnOpen);
        Assert.Equal("prod", tab.Environment.Id);
        Assert.False(_shell.ViewModel.Palette.IsOpen);
    }

    [AvaloniaFact]
    public async Task Lists_only_the_commands_that_can_run_now()
    {
        await OpenAsync();
        var tab = _shell.ViewModel.SelectedTab!;

        _shell.ViewModel.OpenPaletteCommand.Execute(null);
        var withNothingSelected = Titles();
        _shell.ViewModel.Palette.CloseCommand.Execute(null);
        tab.OpenInViewer(new LogTarget(_shell.Paths.From(TestShell.ConfigPath), "jbx.yaml", "Config"));
        _shell.ViewModel.OpenPaletteCommand.Execute(null);
        var withAFileOpen = Titles();

        Assert.DoesNotContain(Strings.Shortcut_CopyPath, withNothingSelected);
        Assert.DoesNotContain(Strings.Palette_NextMatch, withNothingSelected);
        Assert.Contains(Strings.Palette_EditConfig, withNothingSelected);
        Assert.Contains(Strings.Palette_NextMatch, withAFileOpen);
        Assert.Contains(Strings.FormatPalette_SwitchEnvironment("Production"), withAFileOpen);
    }

    [AvaloniaFact]
    public async Task Escape_closes_the_palette_without_running_anything()
    {
        var window = await OpenAsync();
        var environment = _shell.ViewModel.SelectedTab!.Environment;

        window.Press(Key.P, RawInputModifiers.Control | RawInputModifiers.Shift);
        _shell.ViewModel.Palette.FilterText = "switch prod";
        window.Settle();
        window.Press(Key.Escape);

        Assert.False(_shell.ViewModel.Palette.IsOpen);
        Assert.Same(environment, _shell.ViewModel.SelectedTab!.Environment);
    }

    [AvaloniaFact]
    public async Task Reloads_the_config_when_it_is_saved_with_changes()
    {
        var window = await OpenAsync();
        var tab = _shell.ViewModel.SelectedTab!;

        SaveConfig(Fixture.ReadText("config/valid-full.yaml").Replace("Billing Portal", "Billing Hub", StringComparison.Ordinal));
        _shell.Time.Advance(MainWindowViewModel.ConfigCheckInterval);
        await window.SettleUntilAsync(() => tab.Rows.Any(row => row.Name == "Billing Hub"));

        Assert.Equal(Strings.Shell_ConfigChangedReloaded, _shell.ViewModel.StatusMessage);
        Assert.Empty(_shell.Interaction.ShownProblems);
    }

    [AvaloniaFact]
    public async Task Keeps_the_previous_config_after_a_broken_save_and_shows_the_problems_only_when_asked()
    {
        var window = await OpenAsync();
        var tab = _shell.ViewModel.SelectedTab!;

        SaveConfig(Fixture.ReadText("config/several-errors.yaml"));
        _shell.Time.Advance(MainWindowViewModel.ConfigCheckInterval);
        await window.SettleUntilAsync(() => _shell.ViewModel.HasPendingProblems);
        var shownBeforeAsking = _shell.Interaction.ShownProblems.Count;
        window.Click(window.ByAutomationId<Button>("Shell.ShowProblems"));

        Assert.Equal(Strings.Shell_ConfigChangedInvalid, _shell.ViewModel.StatusMessage);
        Assert.Contains(tab.Rows, row => row.Name == "Billing Portal");
        Assert.Equal(0, shownBeforeAsking);
        Assert.Single(_shell.Interaction.ShownProblems);
    }

    [AvaloniaFact]
    public async Task Edit_config_opens_the_config_in_its_editor()
    {
        var window = await OpenAsync();

        window.Press(Key.O, RawInputModifiers.Control | RawInputModifiers.Shift);
        await window.SettleUntilAsync(() => _shell.Shell.OpenedFiles.Count == 1);

        Assert.Equal([TestShell.ConfigPath], _shell.Shell.OpenedFiles);
        Assert.Equal(Strings.Shell_EditingConfig, _shell.ViewModel.StatusMessage);
    }

    private async Task<Window> OpenAsync()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        window.Settle();
        return window;
    }

    private void SaveConfig(string content)
    {
        _shell.FileSystem.SetFileContent(_shell.Paths.From(TestShell.ConfigPath), Encoding.UTF8.GetBytes(content));
    }

    private List<string> Titles()
    {
        return [.. _shell.ViewModel.Palette.Matches.Select(entry => entry.Title)];
    }
}
