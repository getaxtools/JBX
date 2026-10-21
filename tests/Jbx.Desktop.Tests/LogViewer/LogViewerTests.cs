using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Jbx.Desktop.Features.LogViewer;
using Jbx.Desktop.Features.Tabs;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Tests.Harness;
using Jbx.Desktop.Text;
using Jbx.Platform;
using Jbx.Services.Logs;
using Jbx.Testing;
using Xunit;

namespace Jbx.Desktop.Tests.LogViewer;

public sealed class LogViewerTests : IDisposable
{
    private const string Folder = @"\\fs01.contoso.test\AppLogs\dev\BillingPortal\Api\logs";

    private readonly TestShell _shell = new();
    private readonly CanonicalPath _file;

    public LogViewerTests()
    {
        _file = _shell.Paths.From(Folder + @"\api.log");
    }

    public void Dispose()
    {
        _shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Shows_every_line_and_starts_at_the_end_while_following()
    {
        var (window, _, viewer) = await OpenAsync(Lines(500));
        var list = window.ByAutomationId<ListBox>("Viewer.Lines");

        await window.SettleUntilAsync(() => list.ContainerFromIndex(499) is not null && LineAt(viewer, 499) is { IsLoading: false });

        Assert.Equal(500, viewer.Lines.Count);
        Assert.Equal("499 WARN request 499 served", LineAt(viewer, 499).Text);
        Assert.Equal(LineLevel.Warning, LineAt(viewer, 499).Level);
        Assert.Equal(Strings.FormatViewer_Lines(Counts.Number(500)), viewer.LinesText);
    }

    [AvaloniaFact]
    public async Task Search_marks_matches_and_enter_and_f3_step_through_them()
    {
        var (window, _, viewer) = await OpenAsync(Lines(500));
        viewer.RequestSearchFocus();
        window.Settle();

        viewer.SearchText = "request 4";
        _shell.Time.Advance(LogViewerViewModel.SearchDelay);
        await window.SettleUntilAsync(() => viewer.MatchText is not null);
        var first = viewer.MatchText;
        window.Press(Key.Enter);
        var second = viewer.MatchText;
        window.Press(Key.F3, RawInputModifiers.Shift);
        await window.SettleUntilAsync(() => LineAt(viewer, 4) is { IsLoading: false });

        Assert.Equal(Strings.FormatViewer_MatchOf("1", "111"), first);
        Assert.Equal(Strings.FormatViewer_MatchOf("2", "111"), second);
        Assert.Equal(Strings.FormatViewer_MatchOf("1", "111"), viewer.MatchText);
        Assert.Equal([new MatchSpan(7, 9)], LineAt(viewer, 4).Highlights);
    }

    [AvaloniaFact]
    public async Task Level_buttons_and_only_matching_lines_narrow_the_list()
    {
        var (window, _, viewer) = await OpenAsync(Lines(30));

        viewer.ShowWarnings = true;
        await window.SettleUntilAsync(() => viewer.IsFiltered);
        var warnings = viewer.Lines.Count;
        viewer.SearchText = "request 1";
        viewer.ShowOnlyMatches = true;
        _shell.Time.Advance(LogViewerViewModel.SearchDelay);
        await window.SettleUntilAsync(() => viewer.Lines.Count == 6);

        Assert.Equal(15, warnings);
        Assert.Equal(Strings.FormatViewer_Warnings(Counts.Number(15)), viewer.WarningsLabel);
        Assert.Equal(Strings.FormatViewer_ShowingLines(Counts.Number(6), Counts.Number(30)), viewer.LinesText);
        Assert.Equal([1, 11, 13, 15, 17, 19], Enumerable.Range(0, 6).Select(index => LineAt(viewer, index).Number));
    }

    [AvaloniaFact]
    public async Task Follows_lines_as_they_are_written_including_a_line_still_being_written()
    {
        var (window, _, viewer) = await OpenAsync("one\ntw");

        _shell.FileSystem.AppendFileContent(_file, "o\nthree\n"u8.ToArray());
        _shell.Time.Advance(LogViewerViewModel.FollowInterval);
        await window.SettleUntilAsync(() => viewer.Lines.Count == 3 && LineAt(viewer, 1) is { IsLoading: false });

        Assert.Equal(["one", "two", "three"], Enumerable.Range(0, 3).Select(index => LineAt(viewer, index).Text));
    }

    [AvaloniaFact]
    public async Task Counts_and_reaches_matches_in_lines_written_after_the_search()
    {
        var (window, _, viewer) = await OpenAsync("alpha\nbeta\n");
        viewer.SearchText = "gamma";
        _shell.Time.Advance(LogViewerViewModel.SearchDelay);
        await window.SettleUntilAsync(() => viewer.MatchText == Strings.Viewer_NoMatches);
        viewer.Follow = true;

        _shell.FileSystem.AppendFileContent(_file, "gamma one\ngamma two\n"u8.ToArray());
        _shell.Time.Advance(LogViewerViewModel.FollowInterval);
        await window.SettleUntilAsync(() => viewer.Lines.Count == 4 && viewer.MatchText != Strings.Viewer_NoMatches);
        viewer.NextMatchCommand.Execute(null);

        Assert.Equal(Strings.FormatViewer_MatchOf("1", "2"), viewer.MatchText);
    }

    [AvaloniaFact]
    public async Task Opens_a_cleared_or_rotated_file_again_from_the_start()
    {
        var (window, _, viewer) = await OpenAsync(Lines(100));

        _shell.FileSystem.SetFileContent(_file, "fresh start\n"u8.ToArray());
        _shell.Time.Advance(LogViewerViewModel.FollowInterval);
        await window.SettleUntilAsync(() => viewer.Lines.Count == 1 && viewer.IsReady);

        Assert.Equal(Strings.Viewer_FileReplaced, viewer.NoticeText);
    }

    [AvaloniaFact]
    public async Task Stops_following_and_says_why_when_the_share_stops_answering()
    {
        var (window, _, viewer) = await OpenAsync(Lines(10));
        _shell.FileSystem.AddFolder(Folder, FolderBehaviour.Blackholed);

        _shell.Time.Advance(LogViewerViewModel.FollowInterval);
        await window.SettleUntilAsync(() => _shell.FileSystem.InFlight == 1);
        _shell.Time.Advance(LogFileReader.ReadTimeout);
        await window.SettleUntilAsync(() => viewer.HasProblem);

        Assert.False(viewer.Follow);
        Assert.Equal(Strings.FormatViewer_FollowStopped(Strings.Status_Timeout), viewer.ProblemText);
    }

    [AvaloniaFact]
    public async Task Says_why_a_file_cannot_be_opened_and_offers_to_try_again()
    {
        _shell.FileSystem.AddFolder(Folder, FolderBehaviour.Denied);
        var (window, _, viewer) = await OpenAsync(content: null);

        await window.SettleUntilAsync(() => viewer.HasProblem);

        Assert.Equal(Strings.FormatViewer_OpenFailed(Strings.Status_Denied, Strings.Remedy_Denied), viewer.ProblemText);
        Assert.True(window.ByAutomationId<TextBlock>("Viewer.Problem").IsShown());
    }

    [AvaloniaFact]
    public async Task Ctrl_w_closes_the_open_file_before_the_environment()
    {
        var (window, tab, _) = await OpenAsync(Lines(10));

        window.Press(Key.W, RawInputModifiers.Control);
        var afterFirst = (tab.Documents.Count, _shell.ViewModel.Tabs.Count, tab.IsBrowsing);
        window.Press(Key.W, RawInputModifiers.Control);

        Assert.Equal((0, 1, true), afterFirst);
        Assert.Empty(_shell.ViewModel.Tabs);
    }

    [AvaloniaFact]
    public async Task Copies_the_selected_lines_as_text()
    {
        var (_, _, viewer) = await OpenAsync(Lines(10));

        viewer.Selection.SelectRange(2, 4);
        await viewer.CopyLinesCommand.ExecuteAsync(null);

        var copied = Assert.Single(_shell.Interaction.CopiedTexts);
        Assert.Equal(string.Join(Environment.NewLine, "2 INFO request 2 served", "3 WARN request 3 served", "4 INFO request 4 served"), copied);
    }

    [AvaloniaFact]
    public async Task Goes_to_a_line_by_number()
    {
        var (window, _, viewer) = await OpenAsync(Lines(500));
        _shell.Interaction.TextToEnter = "250";

        await viewer.GoToLineCommand.ExecuteAsync(null);
        window.Settle();

        Assert.Equal(249, viewer.Selection.SelectedIndex);
        Assert.Equal(Strings.FormatViewer_CurrentLine(Counts.Number(250)), viewer.CurrentLineText);
        Assert.False(viewer.Follow);
    }

    [AvaloniaFact]
    public async Task Says_where_a_broken_pattern_goes_wrong_and_keeps_the_lines()
    {
        var (window, _, viewer) = await OpenAsync(Lines(20));

        viewer.UseRegex = true;
        viewer.SearchText = "request (";
        _shell.Time.Advance(LogViewerViewModel.SearchDelay);
        await window.SettleUntilAsync(() => viewer.HasPatternError);

        Assert.Equal(20, viewer.Lines.Count);
        Assert.True(window.ByAutomationId<TextBlock>("Viewer.PatternError").IsShown());
    }

    private async Task<(Window Window, TabViewModel Tab, LogViewerViewModel Viewer)> OpenAsync(string? content)
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        if (content is not null)
        {
            _shell.FileSystem.AddFolder(Folder);
            _shell.FileSystem.SetFileContent(_file, Encoding.UTF8.GetBytes(content));
        }

        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        var tab = _shell.ViewModel.SelectedTab!;
        tab.OpenInViewer(new LogTarget(_file, "api.log", "Billing API"));
        var viewer = tab.SelectedDocument!;
        await window.SettleUntilAsync(() => viewer.IsReady);
        return (window, tab, viewer);
    }

    private static LogLineViewModel LineAt(LogViewerViewModel viewer, int index)
    {
        return (LogLineViewModel)viewer.Lines[index]!;
    }

    private static string Lines(int count)
    {
        var builder = new StringBuilder();
        for (var line = 0; line < count; line++)
        {
            builder.Append(line).Append(line % 2 == 0 ? " INFO" : " WARN").Append(" request ").Append(line).Append(" served\n");
        }

        return builder.ToString();
    }
}
