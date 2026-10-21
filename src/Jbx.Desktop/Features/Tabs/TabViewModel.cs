using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Core.Config;
using Jbx.Desktop.Features.Details;
using Jbx.Desktop.Features.FolderList;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;
using Jbx.Services.Rows;
using Jbx.Services.Settings;

namespace Jbx.Desktop.Features.Tabs;

/// <summary>One environment of the loaded config: its rows, search, filters, selection and details.</summary>
internal sealed partial class TabViewModel : ObservableObject, IRowActions, IDisposable
{
    private readonly ShellServices _services;
    private readonly Action<TabViewModel> _close;
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedFolders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FolderFiles> _folderFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RowViewModel> _rowCache = new(StringComparer.Ordinal);
    private ConfigDocument _document;
    private EnvironmentRows _rows;
    private ReachabilityTracker _tracker;
    private CancellationTokenSource _checks = new();
    private bool _isSwitching;
    private bool _hasChecked;

    public TabViewModel(ShellServices services, ConfigDocument document, DeploymentEnvironment environment, Action<TabViewModel> close)
    {
        _services = services;
        _close = close;
        _document = document;
        Details = new FolderDetailsViewModel(services) { OpenInViewer = OpenInViewer };
        Documents.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasDocuments));
            ShowFoldersCommand.NotifyCanExecuteChanged();
        };
        SortOptions =
        [
            new SortOption(RowSort.Name, Strings.Toolbar_SortByName),
            new SortOption(RowSort.Type, Strings.Toolbar_SortByType),
            new SortOption(RowSort.Status, Strings.Toolbar_SortByStatus),
        ];
        SelectedSort = SortOptions[0];
        _isSwitching = true;
        Environment = environment;
        _isSwitching = false;
        _rows = EnvironmentRows.Resolve(document, environment, services.Resolver, services.Paths);
        _tracker = CreateTracker();
        RefreshFilterOptions();
        Rebuild();
    }

    public ObservableCollection<RowViewModel> Rows { get; } = [];

    public FolderDetailsViewModel Details { get; }

    public IReadOnlyList<SortOption> SortOptions { get; }

    [ObservableProperty]
    public partial IReadOnlyList<DeploymentEnvironment> Environments { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header), nameof(IsProduction), nameof(AccessibleHeader))]
    public partial DeploymentEnvironment Environment { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<FilterOption> TypeOptions { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<FilterOption> TagOptions { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<FilterOption> OwnerOptions { get; set; } = [];

    [ObservableProperty]
    public partial FilterOption? SelectedType { get; set; }

    [ObservableProperty]
    public partial FilterOption? SelectedTag { get; set; }

    [ObservableProperty]
    public partial FilterOption? SelectedOwner { get; set; }

    [ObservableProperty]
    public partial SortOption SelectedSort { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand), nameof(OpenWithDefaultAppCommand), nameof(OpenInExplorerCommand), nameof(CopyPathCommand), nameof(ToggleFavouriteCommand))]
    [NotifyPropertyChangedFor(nameof(IsFavouriteSelected))]
    public partial RowViewModel? SelectedRow { get; set; }

    [ObservableProperty]
    public partial string ResultSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoResultsText))]
    public partial bool HasNoResults { get; set; }

    [ObservableProperty]
    public partial bool IsFiltered { get; set; }

    [ObservableProperty]
    public partial int SearchFocusToken { get; set; }

    [ObservableProperty]
    public partial int ListFocusToken { get; set; }

    ICommand IRowActions.OpenFileCommand => OpenFileCommand;

    ICommand IRowActions.OpenWithDefaultAppCommand => OpenWithDefaultAppCommand;

    ICommand IRowActions.OpenInExplorerCommand => OpenInExplorerCommand;

    ICommand IRowActions.CopyPathCommand => CopyPathCommand;

    ICommand IRowActions.ToggleFavouriteCommand => ToggleFavouriteCommand;

    ICommand IRowActions.ToggleExpandCommand => ToggleExpandCommand;

    public string Header => Environment.DisplayName;

    public bool IsProduction => Environment.IsProductionLike;

    public string NoResultsText => SearchText.Trim().Length > 0
        ? Strings.FormatTab_NoResults(SearchText.Trim())
        : Strings.Tab_NoResultsForFilters;

    public string AccessibleHeader => IsProduction ? Strings.FormatTab_ProductionHeader(Header) : Header;

    public void Activate()
    {
        _isActive = true;
        if (SelectedDocument is { } document)
        {
            document.IsActive = true;
        }

        if (!_hasChecked)
        {
            _hasChecked = true;
            _ = CheckFoldersAsync(_rows.Folders, refresh: false);
        }
    }

    public bool TryReload(ConfigDocument document)
    {
        var environment = document.FindEnvironment(Environment.Id);
        if (environment is null)
        {
            return false;
        }

        _document = document;
        SwitchTo(environment);
        return true;
    }

    public void RequestSearchFocus()
    {
        if (SelectedDocument is { } document)
        {
            document.RequestSearchFocus();
            return;
        }

        SearchFocusToken++;
    }

    public Task RefreshAsync()
    {
        if (SelectedDocument is { } document)
        {
            return document.ReloadCommand.ExecuteAsync(null);
        }

        ReloadExpandedFolders();
        return CheckFoldersAsync(_rows.Folders, refresh: true);
    }

    public void Dispose()
    {
        DisposeDocuments();
        _checks.Cancel();
        _checks.Dispose();
        _tracker.StatusChanged -= OnStatusChanged;
    }

    [RelayCommand]
    private void Close()
    {
        _close(this);
    }

    [RelayCommand]
    private void SwitchEnvironment(DeploymentEnvironment? environment)
    {
        if (environment is not null)
        {
            Environment = environment;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        if (SearchText.Length > 0)
        {
            SearchText = string.Empty;
            return;
        }

        FocusList();
    }

    [RelayCommand]
    private void FocusList()
    {
        if (SelectedDocument is { } document)
        {
            document.RequestListFocus();
            return;
        }

        SelectedRow ??= Rows.FirstOrDefault();
        ListFocusToken++;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedType = TypeOptions[0];
        SelectedTag = TagOptions[0];
        SelectedOwner = OwnerOptions[0];
    }

    [RelayCommand]
    private Task RefreshFoldersAsync()
    {
        return RefreshAsync();
    }

    partial void OnEnvironmentChanged(DeploymentEnvironment value)
    {
        if (!_isSwitching)
        {
            // Files opened from the previous environment would be mislabelled under this one.
            SelectedDocument = null;
            DisposeDocuments();
            SwitchTo(value);
            _ = _services.Settings.UpdateAsync(settings => settings with { LastEnvironmentId = value.Id }, CancellationToken.None);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        Rebuild();
    }

    partial void OnSelectedTypeChanged(FilterOption? value)
    {
        Rebuild();
    }

    partial void OnSelectedTagChanged(FilterOption? value)
    {
        Rebuild();
    }

    partial void OnSelectedOwnerChanged(FilterOption? value)
    {
        Rebuild();
    }

    partial void OnSelectedSortChanged(SortOption value)
    {
        Rebuild();
    }

    partial void OnSelectedRowChanged(RowViewModel? value)
    {
        switch (value)
        {
            case FolderRowViewModel folder:
                Details.ShowFolder(TargetFor(folder), _tracker.StatusOf(folder.Entry));
                break;
            case ApplicationRowViewModel application:
                var folders = _rows.Applications.First(entry => entry.Application.Id == application.Id).Folders;
                Details.ShowApplication(application, folders, _tracker.StatusOf);
                break;
            case FileItemRowViewModel file:
                Details.ShowFile(TargetFor(file), file.File);
                break;
            case FolderNoteRowViewModel:
            case null when !_isSwitching:
                Details.ShowNothing();
                break;
        }
    }

    private DetailsTarget TargetFor(FolderRowViewModel folder)
    {
        return new DetailsTarget(folder.Name, Strings.FormatDetails_FolderSubtitle(folder.Entry.Application.DisplayName, Environment.DisplayName), folder.Entry.Path)
        {
            TypeText = folder.TypeText,
            Notes = folder.Entry.Folder.Notes,
            RetentionDays = folder.Entry.Folder.RetentionDays,
            LogFilePattern = folder.Entry.Folder.LogFilePattern,
            FavouriteLabel = FavouriteLabelFor(folder.Name, folder.Entry),
            Origin = OriginFor(folder.Entry),
        };
    }

    private DetailsTarget TargetFor(FileItemRowViewModel file)
    {
        var subtitle = Strings.FormatDetails_FileSubtitle(file.Folder.Folder.Label, file.Folder.Application.DisplayName, Environment.DisplayName);
        return new DetailsTarget(file.Name, subtitle, file.File.Path)
        {
            Kind = FavouriteKind.File,
            FavouriteLabel = FavouriteLabelFor(file.Name, file.Folder),
            Origin = OriginFor(file.Folder),
        };
    }

    private string FavouriteLabelFor(string name, FolderEntry folder)
    {
        return Strings.FormatFavourites_DefaultLabel(name, folder.Application.DisplayName, Environment.DisplayName);
    }

    private FavouriteOrigin OriginFor(FolderEntry folder)
    {
        return new FavouriteOrigin(folder.Application.Id, folder.Folder.Label, Environment.Id)
        {
            ApplicationDisplayName = folder.Application.DisplayName,
            EnvironmentDisplayName = Environment.DisplayName,
        };
    }

    private void SwitchTo(DeploymentEnvironment environment)
    {
        _isSwitching = true;
        try
        {
            _checks.Cancel();
            _checks.Dispose();
            _checks = new CancellationTokenSource();
            _tracker.StatusChanged -= OnStatusChanged;
            Environment = environment;
            _rows = EnvironmentRows.Resolve(_document, environment, _services.Resolver, _services.Paths);
            _tracker = CreateTracker();
            _rowCache.Clear();
            _folderFiles.Clear();
            Rows.Clear();
            RefreshFilterOptions();
            Rebuild();
            Details.ShowNothing();
        }
        finally
        {
            _isSwitching = false;
        }

        _hasChecked = true;
        ReloadExpandedFolders();
        _ = CheckFoldersAsync(_rows.Folders, refresh: false);
    }

    private ReachabilityTracker CreateTracker()
    {
        var tracker = new ReachabilityTracker(_services.Probe, _services.Time);
        tracker.StatusChanged += OnStatusChanged;
        return tracker;
    }

    private void RefreshFilterOptions()
    {
        Environments = _document.Environments;
        var applications = _document.Applications;
        TypeOptions = WithAll(Strings.Toolbar_AllTypes, applications.SelectMany(application => application.Folders).Select(folder => folder.Type.Key));
        TagOptions = WithAll(Strings.Toolbar_AllTags, applications.SelectMany(application => application.Tags));
        OwnerOptions = WithAll(Strings.Toolbar_AllOwners, applications.Select(application => application.Owner).OfType<string>());
        SelectedType = TypeOptions[0];
        SelectedTag = TagOptions[0];
        SelectedOwner = OwnerOptions[0];
    }

    private static List<FilterOption> WithAll(string allLabel, IEnumerable<string> values)
    {
        return [new FilterOption(null, allLabel), .. values.Distinct(StringComparer.Ordinal).Order(StringComparer.CurrentCultureIgnoreCase).Select(value => new FilterOption(value, value))];
    }

    private void Rebuild()
    {
        if (_rows is null)
        {
            return;
        }

        var query = new RowQuery
        {
            SearchText = SearchText,
            FolderTypes = SelectedType?.Value is { } type ? [type] : [],
            Tags = SelectedTag?.Value is { } tag ? [tag] : [],
            Owners = SelectedOwner?.Value is { } owner ? [owner] : [],
            Sort = SelectedSort?.Sort ?? RowSort.Name,
        };
        var expansion = new RowExpansion
        {
            ApplicationIds = _expanded,
            FolderKeys = _expandedFolders,
            FilesOf = folder => _folderFiles.GetValueOrDefault(folder.Key),
        };
        var listing = RowFlattener.Flatten(_rows, query, expansion, _tracker.StatusOf);
        var rows = listing.Rows.Select(ToViewModel).ToList();
        RowDiff.Apply(Rows, rows);
        IsFiltered = query.IsFiltered;
        HasNoResults = rows.Count == 0 && (query.IsSearching || query.IsFiltered);
        ResultSummary = listing.MatchingFolderCount == listing.TotalFolderCount
            ? Strings.FormatTab_FolderTotal(listing.TotalFolderCount)
            : Strings.FormatTab_FolderMatches(listing.MatchingFolderCount, listing.TotalFolderCount);
    }

    private RowViewModel ToViewModel(RowItem item)
    {
        switch (item)
        {
            case ApplicationRowItem application:
                var applicationRow = (ApplicationRowViewModel)Cached("app:" + application.Entry.Application.Id, () => new ApplicationRowViewModel(application.Entry, this));
                applicationRow.IsExpanded = application.IsExpanded;
                applicationRow.VisibleFolderCount = application.VisibleFolderCount;
                applicationRow.ProblemSummary = ProblemSummary(application.Entry);
                return applicationRow;
            case FolderRowItem folder:
                var folderRow = (FolderRowViewModel)Cached("folder:" + folder.Entry.Key, () => new FolderRowViewModel(folder.Entry, this));
                folderRow.Position = folder.PositionInApplication;
                folderRow.SiblingCount = folder.FoldersInApplication;
                folderRow.IsExpanded = folder.IsExpanded;
                ApplyStatus(folderRow);
                folderRow.IsFavourite = folder.Entry.Path is { } path && _services.Favourites.IsFavourite(path);
                return folderRow;
            case FileRowItem file:
                var fileRow = (FileItemRowViewModel)Cached($"file:{file.Folder.Key}|{file.File.Path.Key}", () => new FileItemRowViewModel(file.Folder, file.File, this));
                fileRow.File = file.File;
                fileRow.Now = _services.Time.GetUtcNow();
                fileRow.Position = file.PositionInFolder;
                fileRow.SiblingCount = file.FilesInFolder;
                fileRow.IsFavourite = _services.Favourites.IsFavourite(file.File.Path);
                return fileRow;
            case FolderNoteRowItem note:
                var noteRow = (FolderNoteRowViewModel)Cached("note:" + note.Folder.Key, () => new FolderNoteRowViewModel(note.Folder));
                noteRow.Text = TreeText.Note(note);
                noteRow.IsProblem = note.Note == FolderNote.Failed;
                return noteRow;
            default:
                throw new InvalidOperationException(item.GetType().Name);
        }
    }

    private RowViewModel Cached(string key, Func<RowViewModel> create)
    {
        if (!_rowCache.TryGetValue(key, out var row))
        {
            row = create();
            _rowCache[key] = row;
        }

        return row;
    }

    private string? ProblemSummary(ApplicationEntry application)
    {
        var problems = application.Folders.Count(folder => StatusText.IsProblem(_tracker.StatusOf(folder).Reachability) && _tracker.StatusOf(folder).CheckedAt is not null);
        return problems == 0 ? null : Counts.Problems(problems);
    }

    private void ApplyStatus(FolderRowViewModel row)
    {
        var status = _tracker.StatusOf(row.Entry);
        row.Status = row.Entry.Path is null ? Reachability.Unknown : status.Reachability;
        row.CheckedAt = status.CheckedAt;
    }

    private async Task CheckFoldersAsync(IReadOnlyList<FolderEntry> folders, bool refresh)
    {
        var token = _checks.Token;
        var tracker = _tracker;
        UpdateProgress();
        if (refresh)
        {
            await tracker.RefreshAsync([.. folders], token).ConfigureAwait(true);
        }
        else
        {
            await tracker.CheckAsync(folders, token).ConfigureAwait(true);
        }

        if (tracker == _tracker)
        {
            UpdateProgress();
        }
    }

    private void OnStatusChanged(object? sender, FolderEntry entry)
    {
        _services.Dispatcher.Post(() =>
        {
            if (!ReferenceEquals(sender, _tracker))
            {
                return;
            }

            if (_rowCache.TryGetValue("folder:" + entry.Key, out var row) && row is FolderRowViewModel folder)
            {
                ApplyStatus(folder);
            }

            if (_rowCache.TryGetValue("app:" + entry.Application.Id, out var parent) && parent is ApplicationRowViewModel application)
            {
                application.ProblemSummary = ProblemSummary(_rows.Applications.First(candidate => candidate.Application.Id == entry.Application.Id));
            }

            if (SelectedRow is FolderRowViewModel selected && selected.Entry.Key == entry.Key)
            {
                Details.ApplyStatus(_tracker.StatusOf(entry));
            }

            UpdateProgress();
        });
    }

    private void UpdateProgress()
    {
        var total = _rows.Folders.Count(folder => folder.Path is not null);
        var checking = _rows.Folders.Count(folder => _tracker.StatusOf(folder).Reachability == Reachability.Checking);
        ProgressText = checking == 0 ? null : Strings.FormatTab_Checking(total - checking, total);
    }
}
