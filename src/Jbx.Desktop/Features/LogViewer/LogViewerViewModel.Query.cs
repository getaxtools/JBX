using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;
using Jbx.Services.Logs;

namespace Jbx.Desktop.Features.LogViewer;

/// <summary>
/// Search, filter and level buttons. Search marks and steps through matches; "only matching lines"
/// and the level buttons narrow the list itself.
/// </summary>
internal sealed partial class LogViewerViewModel
{
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private LogMatcher? _searchMatcher;
    private LogMatcher? _viewMatcher;
    private int _activeQueries;
    private List<int>? _viewLines;
    private List<int> _searchLines = [];
    private CancellationTokenSource? _queryRun;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool MatchCase { get; set; }

    [ObservableProperty]
    public partial bool UseRegex { get; set; }

    [ObservableProperty]
    public partial bool ShowOnlyMatches { get; set; }

    [ObservableProperty]
    public partial bool ShowErrors { get; set; }

    [ObservableProperty]
    public partial bool ShowWarnings { get; set; }

    [ObservableProperty]
    public partial bool ShowInfo { get; set; }

    [ObservableProperty]
    public partial bool ShowDebug { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPatternError))]
    public partial string? PatternErrorText { get; set; }

    [ObservableProperty]
    public partial string? MatchText { get; set; }

    [ObservableProperty]
    public partial int SearchFocusToken { get; set; }

    public bool HasPatternError => PatternErrorText is not null;

    public bool IsFiltered => _viewLines is not null;

    [ObservableProperty]
    public partial int ListFocusToken { get; set; }

    public void RequestSearchFocus()
    {
        SearchFocusToken++;
    }

    public void RequestListFocus()
    {
        ListFocusToken++;
    }

    [RelayCommand]
    private void NextMatch()
    {
        GoToMatch(1);
    }

    [RelayCommand]
    private void PreviousMatch()
    {
        GoToMatch(-1);
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void ClearLevels()
    {
        ShowErrors = ShowWarnings = ShowInfo = ShowDebug = false;
    }

    [RelayCommand]
    private void ShowProblemsOnly()
    {
        ShowInfo = ShowDebug = false;
        ShowErrors = ShowWarnings = true;
    }

    [RelayCommand]
    private void ToggleOnlyMatches()
    {
        ShowOnlyMatches = !ShowOnlyMatches;
    }

    partial void OnSearchTextChanged(string value)
    {
        ScheduleQuery(SearchDelay);
    }

    partial void OnMatchCaseChanged(bool value)
    {
        ScheduleQuery(TimeSpan.Zero);
    }

    partial void OnUseRegexChanged(bool value)
    {
        ScheduleQuery(TimeSpan.Zero);
    }

    partial void OnShowOnlyMatchesChanged(bool value)
    {
        ScheduleQuery(TimeSpan.Zero);
    }

    partial void OnShowErrorsChanged(bool value)
    {
        ScheduleQuery(TimeSpan.Zero);
    }

    partial void OnShowWarningsChanged(bool value)
    {
        ScheduleQuery(TimeSpan.Zero);
    }

    partial void OnShowInfoChanged(bool value)
    {
        ScheduleQuery(TimeSpan.Zero);
    }

    partial void OnShowDebugChanged(bool value)
    {
        ScheduleQuery(TimeSpan.Zero);
    }

    private LogQuery BuildQuery()
    {
        var levels = ImmutableHashSet.CreateBuilder<LineLevel>();
        AddIf(levels, ShowErrors, LineLevel.Error);
        AddIf(levels, ShowWarnings, LineLevel.Warning);
        AddIf(levels, ShowInfo, LineLevel.Info);
        AddIf(levels, ShowDebug, LineLevel.Debug);
        return new LogQuery { Text = SearchText, MatchCase = MatchCase, UseRegex = UseRegex, Levels = levels.ToImmutable() };
    }

    private static void AddIf(ImmutableHashSet<LineLevel>.Builder levels, bool isOn, LineLevel level)
    {
        if (isOn)
        {
            levels.Add(level);
        }
    }

    private void ScheduleQuery(TimeSpan delay)
    {
        if (!_isDisposed)
        {
            _ = RunQueryAsync(delay, StartQueryRun());
        }
    }

    /// <summary>Every query, typed or applied after loading, runs here, so a newer one always cancels an older one.</summary>
    private CancellationToken StartQueryRun()
    {
        _queryRun?.Cancel();
        _queryRun?.Dispose();
        _queryRun = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        return _queryRun.Token;
    }

    private async Task RunQueryAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        _activeQueries++;
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, _services.Time, cancellationToken).ConfigureAwait(true);
            }

            // While the file is still opening the query waits; loading applies it at the end.
            if (!IsLoading)
            {
                await ApplyQueryAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            _activeQueries--;
        }
    }

    private async Task ApplyQueryAsync(CancellationToken cancellationToken)
    {
        var query = BuildQuery();
        var created = LogMatcher.Create(query);
        if (created.Matcher is not { } textMatcher)
        {
            PatternErrorText = created.Problem == PatternProblem.Unsupported
                ? Strings.Viewer_PatternUnsupported
                : Strings.FormatViewer_PatternInvalid(created.ProblemOffset + 1);
            return;
        }

        PatternErrorText = null;
        var search = query.HasText ? textMatcher : null;
        var view = query.HasText && ShowOnlyMatches ? textMatcher : query.HasLevels ? LogMatcher.Create(query with { Text = string.Empty }).Matcher : null;
        var anchor = CurrentLineNumber();
        var viewLines = view is null ? null : await ScanAsync(view, 0, cancellationToken).ConfigureAwait(true);
        List<int>? searchLines = search is null ? [] : ReferenceEquals(search, view) ? viewLines : await ScanAsync(search, 0, cancellationToken).ConfigureAwait(true);
        if ((view is not null && viewLines is null) || searchLines is null)
        {
            return;
        }

        ProgressText = null;
        _searchMatcher = search;
        _viewMatcher = view;
        _viewLines = viewLines;
        _searchLines = searchLines;
        _shown.Clear();
        OnPropertyChanged(nameof(IsFiltered));
        ResetLines();
        ShowAfterQuery(anchor);
    }

    /// <summary>Scans the whole file; null when the file stopped answering, which is then on screen.</summary>
    private async Task<List<int>?> ScanAsync(LogMatcher matcher, int fromLine, CancellationToken cancellationToken)
    {
        ProgressText = Strings.FormatViewer_Searching(0);
        var progress = new ThrottledProgress(_services.Dispatcher, _services.Time, ProgressInterval, fraction => ProgressText = Strings.FormatViewer_Searching(Percent(fraction)));
        LogScan scan;
        try
        {
            scan = await _document.ScanAsync(new LogScanRequest(matcher, fromLine) { Progress = progress }, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            progress.Finish();
        }

        cancellationToken.ThrowIfCancellationRequested();
        ProgressText = null;
        if (scan.Reachability != Reachability.Reachable)
        {
            ProblemText = Strings.FormatViewer_ReadFailed(StatusText.Label(scan.Reachability));
            return null;
        }

        if (scan.IsCapped)
        {
            NoticeText = Strings.FormatViewer_MatchesCapped(Counts.Number(LogDocument.MaxMatches));
        }

        return [.. scan.Lines];
    }

    private void ShowAfterQuery(int? anchor)
    {
        if (_searchLines.Count > 0)
        {
            var from = anchor ?? 0;
            var next = _searchLines.FirstOrDefault(line => line >= from, _searchLines[0]);
            SelectLine(next);
            return;
        }

        if (anchor is { } line)
        {
            SelectLine(line);
        }
        else if (Follow)
        {
            ScrollToEnd();
        }
    }

    /// <summary>Adds what a followed file gained to the narrowed list and to the matches.</summary>
    /// <summary>Adds what a followed file gained to the narrowed list and to the matches, with the matchers that built them.</summary>
    private async Task ExtendScansAsync(int fromLine, CancellationToken cancellationToken)
    {
        if (_viewMatcher is { } view && _viewLines is { } viewLines)
        {
            var added = await _document.ScanAsync(new LogScanRequest(view, fromLine), cancellationToken).ConfigureAwait(true);
            if (added.Reachability != Reachability.Reachable)
            {
                return;
            }

            if (MergeTail(viewLines, fromLine, added.Lines))
            {
                Lines.GrowTo(viewLines.Count);
            }
            else
            {
                ResetLines();
            }
        }

        if (_searchMatcher is { } search && !ReferenceEquals(_searchLines, _viewLines))
        {
            var added = await _document.ScanAsync(new LogScanRequest(search, fromLine), cancellationToken).ConfigureAwait(true);
            if (added.Reachability == Reachability.Reachable)
            {
                MergeTail(_searchLines, fromLine, added.Lines);
            }
        }

        UpdateSummary();
    }

    /// <summary>Replaces the entries from <paramref name="fromLine"/> on. True when nothing before the new entries changed.</summary>
    private static bool MergeTail(List<int> lines, int fromLine, IReadOnlyList<int> tail)
    {
        var keep = lines.FindIndex(line => line >= fromLine);
        var removed = keep >= 0 ? lines.Count - keep : 0;
        var unchanged = removed == 0 || (removed == 1 && tail.Count > 0 && tail[0] == lines[keep]);
        if (keep >= 0)
        {
            lines.RemoveRange(keep, removed);
        }

        lines.AddRange(tail);
        return unchanged;
    }

    /// <summary>Selects the next match (<paramref name="step"/> 1) or the previous one (-1), wrapping round.</summary>
    private void GoToMatch(int step)
    {
        if (_searchLines.Count == 0)
        {
            return;
        }

        var forward = step > 0;
        var current = CurrentLineNumber() ?? (forward ? -1 : int.MaxValue);
        var position = _searchLines.BinarySearch(current);
        var index = position >= 0
            ? position + step
            : forward ? ~position : ~position - 1;
        var wrapped = (index + _searchLines.Count) % _searchLines.Count;
        SelectLine(_searchLines[wrapped]);
    }

    private void UpdateMatchText()
    {
        if (_searchMatcher is null)
        {
            MatchText = null;
            return;
        }

        if (_searchLines.Count == 0)
        {
            MatchText = Strings.Viewer_NoMatches;
            return;
        }

        var position = CurrentLineNumber() is { } line ? _searchLines.BinarySearch(line) : -1;
        MatchText = position >= 0
            ? Strings.FormatViewer_MatchOf(Counts.Number(position + 1), Counts.Number(_searchLines.Count))
            : Strings.FormatViewer_Matches(Counts.Number(_searchLines.Count));
    }

    /// <summary>Selects a line, or the nearest shown line after it when a filter hides it.</summary>
    private void SelectLine(int number)
    {
        var index = NearestViewIndex(number);
        if (index < 0)
        {
            return;
        }

        Follow = false;
        _isSelecting = true;
        Selection.Clear();
        Selection.Select(index);
        _isSelecting = false;
        ScrollTarget = new ScrollTarget(index);
        ShowSelection();
    }

    private int? CurrentLineNumber()
    {
        var index = Selection.SelectedIndex;
        return index >= 0 && index < Lines.Count ? LineNumberAt(index) : null;
    }

    private int LineNumberAt(int viewIndex)
    {
        return _viewLines is null ? viewIndex : _viewLines[viewIndex];
    }

    private int ViewIndexOf(int number)
    {
        if (_viewLines is null)
        {
            return number < Lines.Count ? number : -1;
        }

        var position = _viewLines.BinarySearch(number);
        return position >= 0 ? position : -1;
    }

    private int NearestViewIndex(int number)
    {
        if (Lines.Count == 0)
        {
            return -1;
        }

        if (_viewLines is null)
        {
            return Math.Clamp(number, 0, Lines.Count - 1);
        }

        var position = _viewLines.BinarySearch(number);
        return Math.Min(position >= 0 ? position : ~position, Lines.Count - 1);
    }
}
