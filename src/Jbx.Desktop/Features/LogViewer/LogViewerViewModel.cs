using System.Globalization;
using Avalonia.Controls.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;
using Jbx.Services.Logs;

namespace Jbx.Desktop.Features.LogViewer;

/// <summary>
/// One file open in a tab: its lines, search, filters and follow mode. Lines are fetched as the list
/// asks for them, so opening, scrolling and following never wait for the whole file.
/// </summary>
internal sealed partial class LogViewerViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan FollowInterval = TimeSpan.FromSeconds(1);
    private const int MaxRememberedLines = 4_000;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(150);

    private readonly ShellServices _services;
    private readonly LinePages _pages;
    private readonly Dictionary<int, LogLineViewModel> _shown = [];
    private LogDocument _document;
    private CancellationTokenSource _lifetime = new();
    private ITimer? _followTimer;
    private LevelCounts _levels = LevelCounts.None;
    private int _countedLines;
    private bool _isRefreshing;
    private bool _isSelecting;
    private bool _loadFailed;
    private bool _isStarted;
    private bool _isDisposed;

    public LogViewerViewModel(ShellServices services, LogTarget target)
    {
        _services = services;
        Target = target;
        _pages = new LinePages(services.Dispatcher);
        _pages.PageLoaded += OnPageLoaded;
        _pages.ReadFailed += OnReadFailed;
        _document = new LogDocument(target.Path, services.Logs);
        _pages.Attach(_document);
        Lines = new VirtualLines(LineAt, line => ViewIndexOf(line.Number));
        Selection = new SelectionModel<object?> { SingleSelect = false };
        Selection.SelectionChanged += OnSelectionChanged;
    }

    public LogTarget Target { get; }

    public string Title => Target.Title;

    public string Subtitle => Target.Subtitle;

    public string PathText => Target.Path.Display;

    public VirtualLines Lines { get; }

    public SelectionModel<object?> Selection { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? ProblemText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? NoticeText { get; set; }

    [ObservableProperty]
    public partial bool Follow { get; set; } = true;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial ScrollTarget? ScrollTarget { get; set; }

    [ObservableProperty]
    public partial string LinesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CurrentLineText { get; set; }

    [ObservableProperty]
    public partial string FileText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double GutterWidth { get; set; } = GutterWidthFor(0);

    public bool IsReady => !IsLoading;

    public bool HasProblem => ProblemText is not null;

    public bool HasNotice => NoticeText is not null;

    public string ErrorsLabel => Strings.FormatViewer_Errors(Counts.Number(_levels.Errors));

    public string WarningsLabel => Strings.FormatViewer_Warnings(Counts.Number(_levels.Warnings));

    public string InfoLabel => Strings.FormatViewer_Info(Counts.Number(_levels.Info));

    public string DebugLabel => Strings.FormatViewer_Debug(Counts.Number(_levels.Debug));

    /// <summary>Starts reading the file; once only. A file restored in a background tab starts when first shown.</summary>
    public Task StartAsync()
    {
        if (_isStarted)
        {
            return Task.CompletedTask;
        }

        _isStarted = true;
        return LoadAsync();
    }

    public void Dispose()
    {
        _isDisposed = true;
        StopFollowing();
        _queryRun?.Cancel();
        _lifetime.Cancel();
        _lifetime.Dispose();
        _queryRun?.Dispose();
        _pages.Dispose();
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        StopFollowing();
        _queryRun?.Cancel();
        _lifetime.Cancel();
        _lifetime.Dispose();
        _lifetime = new CancellationTokenSource();
        _document = new LogDocument(Target.Path, _services.Logs);
        _pages.Attach(_document);
        _shown.Clear();
        _viewLines = null;
        _searchLines = [];
        _viewMatcher = null;
        _searchMatcher = null;
        _levels = LevelCounts.None;
        _countedLines = 0;
        ResetLines();
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        if (_loadFailed)
        {
            await ReloadAsync().ConfigureAwait(true);
            return;
        }

        ProblemText = null;
        _pages.RetryFailed();
        foreach (var number in _shown.Where(entry => entry.Value.IsLoading).Select(entry => entry.Key).ToList())
        {
            _shown.Remove(number);
        }

        ResetLines();
    }

    partial void OnFollowChanged(bool value)
    {
        UpdateFollowTimer();
        if (value)
        {
            ProblemText = null;
            ScrollToEnd();
        }
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (value && !_isStarted)
        {
            _ = StartAsync();
        }

        UpdateFollowTimer();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        _loadFailed = false;
        ProblemText = null;
        ProgressText = Strings.FormatViewer_Opening(0);
        var progress = new ThrottledProgress(_services.Dispatcher, _services.Time, ProgressInterval, fraction =>
        {
            ProgressText = Strings.FormatViewer_Opening(Percent(fraction));
            ShowGrowth();
        });
        Reachability outcome;
        try
        {
            outcome = await _document.LoadAsync(progress, _lifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            progress.Finish();
        }

        try
        {
            await FinishLoadingAsync(outcome).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    private async Task FinishLoadingAsync(Reachability outcome)
    {
        IsLoading = false;
        ProgressText = null;
        ShowGrowth();
        if (outcome != Reachability.Reachable)
        {
            _loadFailed = true;
            ProblemText = Strings.FormatViewer_OpenFailed(StatusText.Label(outcome), StatusText.Remedy(outcome));
            return;
        }

        RereadUnfinishedLines();
        await CountNewLevelsAsync().ConfigureAwait(true);
        NoticeText = _document.IsCapped ? Strings.FormatViewer_Capped(Counts.Number(LineIndex.MaxLines)) : NoticeText;
        await RunQueryAsync(TimeSpan.Zero, StartQueryRun()).ConfigureAwait(true);
        if (Follow)
        {
            ScrollToEnd();
        }

        UpdateFollowTimer();
    }

    private void UpdateFollowTimer()
    {
        var shouldFollow = Follow && IsActive && !IsLoading && !_loadFailed;
        if (shouldFollow && _followTimer is null)
        {
            _followTimer = _services.Time.CreateTimer(_ => _services.Dispatcher.Post(() => _ = FollowStepAsync()), null, FollowInterval, FollowInterval);
        }
        else if (!shouldFollow)
        {
            StopFollowing();
        }
    }

    private void StopFollowing()
    {
        _followTimer?.Dispose();
        _followTimer = null;
    }

    private async Task FollowStepAsync()
    {
        // While a query scans the whole file the index must not grow, or the lines added meanwhile would be lost.
        if (_isRefreshing || _followTimer is null || _isDisposed || _activeQueries > 0)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            var refresh = await _document.RefreshAsync(_lifetime.Token).ConfigureAwait(true);
            await ShowRefreshAsync(refresh).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task ShowRefreshAsync(LogRefresh refresh)
    {
        if (refresh.WasTruncated)
        {
            NoticeText = Strings.Viewer_FileReplaced;
            await ReloadAsync().ConfigureAwait(true);
            return;
        }

        if (refresh.Reachability != Reachability.Reachable)
        {
            Follow = false;
            ProblemText = Strings.FormatViewer_FollowStopped(StatusText.Label(refresh.Reachability));
            return;
        }

        if (!refresh.HasChanges)
        {
            return;
        }

        _pages.Invalidate(refresh.FirstChangedLine);
        ReloadShownLine(refresh.FirstChangedLine);
        RereadUnfinishedLines();
        await CountNewLevelsAsync().ConfigureAwait(true);
        if (_viewLines is null)
        {
            ShowGrowth();
        }

        await ExtendScansAsync(refresh.FirstChangedLine, _lifetime.Token).ConfigureAwait(true);

        if (Follow)
        {
            ScrollToEnd();
        }
    }

    private void ShowGrowth()
    {
        if (_viewLines is null)
        {
            Lines.GrowTo(_document.LineCount);
        }

        UpdateSummary();
    }

    private async Task CountNewLevelsAsync()
    {
        var complete = _document.CompleteLineCount;
        var counted = await _document.CountLevelsAsync(_countedLines, _lifetime.Token).ConfigureAwait(true);
        _levels = _levels.Plus(counted);
        _countedLines = complete;
        OnPropertyChanged(nameof(ErrorsLabel));
        OnPropertyChanged(nameof(WarningsLabel));
        OnPropertyChanged(nameof(InfoLabel));
        OnPropertyChanged(nameof(DebugLabel));
    }

    private void UpdateSummary()
    {
        var total = _document.LineCount;
        LinesText = _viewLines is null
            ? Strings.FormatViewer_Lines(Counts.Number(total))
            : Strings.FormatViewer_ShowingLines(Counts.Number(_viewLines.Count), Counts.Number(total));
        FileText = Strings.FormatViewer_FileInfo(RelativeTime.Size(_document.FileLength), _document.Encoding?.Name ?? string.Empty);
        GutterWidth = GutterWidthFor(total);
        UpdateMatchText();
    }

    private LogLineViewModel LineAt(int viewIndex)
    {
        var number = LineNumberAt(viewIndex);
        if (_isDisposed)
        {
            return LogLineViewModel.Loading(number);
        }

        if (_shown.TryGetValue(number, out var shown) && !shown.IsLoading)
        {
            return shown;
        }

        var line = _pages.TryGet(number);
        var model = line is null ? shown ?? LogLineViewModel.Loading(number) : LogLineViewModel.From(line, _searchMatcher);
        Remember(model);
        return model;
    }

    private void Remember(LogLineViewModel model)
    {
        if (_shown.Count >= MaxRememberedLines)
        {
            foreach (var number in _shown.Where(entry => !entry.Value.IsLoading).Select(entry => entry.Key).ToList())
            {
                _shown.Remove(number);
            }
        }

        _shown[model.Number] = model;
    }

    private void OnPageLoaded(int first)
    {
        for (var number = first; number < first + LinePages.PageSize; number++)
        {
            if (!_shown.TryGetValue(number, out var previous) || !previous.IsLoading || _pages.TryGet(number) is not { } line)
            {
                continue;
            }

            var current = LogLineViewModel.From(line, _searchMatcher);
            _shown[number] = current;
            var index = ViewIndexOf(number);
            if (index >= 0)
            {
                ReplaceLine(index, previous, current);
            }
        }
    }

    /// <summary>Lines shown while the writer was still writing them are read again, so they end up whole.</summary>
    private void RereadUnfinishedLines()
    {
        foreach (var number in _shown.Where(entry => entry.Value.IsUnfinished).Select(entry => entry.Key).ToList())
        {
            _pages.Invalidate(number);
            ReloadShownLine(number);
        }
    }

    private void ReloadShownLine(int number)
    {
        if (!_shown.TryGetValue(number, out var previous))
        {
            return;
        }

        var placeholder = LogLineViewModel.Loading(number);
        _shown[number] = placeholder;
        var index = ViewIndexOf(number);
        if (index >= 0)
        {
            ReplaceLine(index, previous, placeholder);
            _pages.TryGet(number);
        }
    }

    private void ReplaceLine(int index, LogLineViewModel previous, LogLineViewModel current)
    {
        var wasSelected = Selection.IsSelected(index);
        _isSelecting = true;
        Lines.Replace(index, previous, current);
        if (wasSelected)
        {
            Selection.Select(index);
        }

        _isSelecting = false;
    }

    private void OnReadFailed(Reachability reachability)
    {
        Follow = false;
        ProblemText = Strings.FormatViewer_ReadFailed(StatusText.Label(reachability));
    }

    private void OnSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<object?> e)
    {
        if (!_isSelecting && Follow && Selection.SelectedIndex >= 0 && Selection.SelectedIndex != Lines.Count - 1)
        {
            Follow = false;
        }

        ShowSelection();
    }

    private void ShowSelection()
    {
        CurrentLineText = CurrentLineNumber() is { } number ? Strings.FormatViewer_CurrentLine(Counts.Number(number + 1)) : null;
        UpdateMatchText();
    }

    private void ScrollToEnd()
    {
        if (Lines.Count > 0)
        {
            ScrollTarget = new ScrollTarget(Lines.Count - 1);
        }
    }

    private void ResetLines()
    {
        _isSelecting = true;
        Lines.Reset(_viewLines?.Count ?? _document.LineCount);
        _isSelecting = false;
        UpdateSummary();
    }

    private static int Percent(double fraction)
    {
        return (int)Math.Clamp(Math.Round(fraction * 100), 0, 100);
    }

    private static double GutterWidthFor(int lineCount)
    {
        const double CharacterWidth = 8;
        const double Padding = 16;
        const int MinimumDigits = 4;
        var digits = Math.Max(MinimumDigits, lineCount.ToString("N0", CultureInfo.CurrentCulture).Length);
        return (digits * CharacterWidth) + Padding;
    }
}
