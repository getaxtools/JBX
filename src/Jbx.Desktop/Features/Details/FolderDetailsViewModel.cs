using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Features.FolderList;
using Jbx.Desktop.Features.LogViewer;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;
using Jbx.Services.Favourites;
using Jbx.Services.Rows;

namespace Jbx.Desktop.Features.Details;

internal sealed partial class FolderDetailsViewModel(ShellServices services) : ObservableObject
{
    public static readonly TimeSpan ListingDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>Where files open; without it they open with their default app.</summary>
    public Action<LogTarget>? OpenInViewer { get; init; }

    private CancellationTokenSource? _listing;

    public ObservableCollection<FileRowViewModel> Files { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFolder), nameof(IsApplication), nameof(IsNothing), nameof(IsFile))]
    public partial DetailsKind Kind { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathText))]
    public partial DetailsTarget? Target { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Subtitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel), nameof(RemedyText))]
    public partial Reachability Status { get; set; }

    [ObservableProperty]
    public partial string? CheckedText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DetailsFact> Facts { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingFiles), nameof(HasFiles), nameof(HasFilesMessage))]
    public partial FilesState FilesState { get; set; }

    [ObservableProperty]
    public partial string? FilesMessage { get; set; }

    [ObservableProperty]
    public partial FileRowViewModel? SelectedFile { get; set; }

    [ObservableProperty]
    public partial bool IsFavourite { get; set; }

    [ObservableProperty]
    public partial string? ActionMessage { get; set; }

    [ObservableProperty]
    public partial int FocusToken { get; set; }

    public bool IsFolder => Kind == DetailsKind.Folder;

    public bool IsApplication => Kind == DetailsKind.Application;

    public bool IsNothing => Kind == DetailsKind.Nothing;

    public bool IsFile => Kind == DetailsKind.File;

    public string StatusLabel => StatusText.Label(Status);

    public string RemedyText => StatusText.Remedy(Status);

    public string PathText => Target?.Path?.Display ?? Strings.Rows_PathUnresolved;

    public bool IsLoadingFiles => FilesState == FilesState.Loading;

    public bool HasFiles => FilesState == FilesState.Loaded;

    public bool HasFilesMessage => FilesMessage is not null;

    public void ShowNothing()
    {
        CancelListing();
        Kind = DetailsKind.Nothing;
        Target = null;
        Files.Clear();
    }

    public void ShowApplication(ApplicationRowViewModel row, IReadOnlyList<FolderEntry> folders, Func<FolderEntry, FolderStatus> statusOf)
    {
        CancelListing();
        Target = null;
        Title = row.Name;
        Subtitle = row.Owner ?? string.Empty;
        var problems = folders.Count(folder => StatusText.IsProblem(statusOf(folder).Reachability));
        Facts = DetailsFact.ForApplication(row.Entry.Application, folders.Count, problems);
        Kind = DetailsKind.Application;
        Files.Clear();
    }

    public void ShowFolder(DetailsTarget target, FolderStatus status)
    {
        CancelListing();
        Target = target;
        Title = target.Title;
        Subtitle = target.Subtitle;
        Facts = DetailsFact.ForFolder(target);
        IsFavourite = target.Path is { } path && services.Favourites.IsFavourite(path);
        ActionMessage = null;
        Kind = DetailsKind.Folder;
        ApplyStatus(status);
        Files.Clear();
        FilesState = FilesState.NotLoaded;
        FilesMessage = null;
        if (status.Reachability is Reachability.Reachable && target.Path is not null)
        {
            _ = LoadFilesAfterDelayAsync();
        }
    }

    public void ShowFile(DetailsTarget target, FileEntry file)
    {
        CancelListing();
        Target = target;
        Title = target.Title;
        Subtitle = target.Subtitle;
        Facts = DetailsFact.ForFile(file, services.Time.GetUtcNow());
        IsFavourite = services.Favourites.IsFavourite(file.Path);
        ActionMessage = null;
        Kind = DetailsKind.File;
        Files.Clear();
    }

    public void ApplyStatus(FolderStatus status)
    {
        Status = status.Reachability;
        CheckedText = status.CheckedAt is { } checkedAt
            ? Strings.FormatDetails_CheckedAt(RelativeTime.Describe(checkedAt, services.Time.GetUtcNow()))
            : null;
        if (status.Reachability == Reachability.Reachable && FilesState == FilesState.NotLoaded && Kind == DetailsKind.Folder)
        {
            _ = LoadFilesAfterDelayAsync();
        }
    }

    private void Open(LogTarget target)
    {
        if (OpenInViewer is { } open)
        {
            open(target);
            return;
        }

        _ = OpenWithDefaultAppAsync(target);
    }

    private async Task OpenWithDefaultAppAsync(LogTarget target)
    {
        ActionMessage = await FileOpener.OpenAsync(services.Shell, target.Path, target.Title).ConfigureAwait(true);
    }

    public void RequestFocus()
    {
        FocusToken++;
    }

    [RelayCommand]
    private Task ViewFilesAsync()
    {
        return LoadFilesAsync(TimeSpan.Zero);
    }

    [RelayCommand]
    private async Task OpenInExplorerAsync()
    {
        if (Target?.Path is not { } path)
        {
            return;
        }

        var opened = Target.Kind == Services.Settings.FavouriteKind.File
            ? await services.Shell.RevealInFolderAsync(path).ConfigureAwait(true)
            : await services.Shell.OpenFolderAsync(path).ConfigureAwait(true);
        ActionMessage = opened ? null : Strings.Details_ExplorerFailed;
    }

    [RelayCommand]
    private async Task CopyPathAsync()
    {
        if (Target?.Path is { } path)
        {
            await services.Interaction.CopyTextAsync(path.Display).ConfigureAwait(true);
            ActionMessage = Strings.Details_PathCopied;
        }
    }

    [RelayCommand]
    private async Task ToggleFavouriteAsync()
    {
        if (Target?.Path is not { } path)
        {
            return;
        }

        var request = new FavouriteRequest(path, Target.FavouriteLabel, Target.Kind) { Origin = Target.Origin };
        IsFavourite = await services.Favourites.ToggleAsync(request, CancellationToken.None).ConfigureAwait(true);
        ActionMessage = IsFavourite ? Strings.Favourites_Added : Strings.Favourites_Removed;
    }

    [RelayCommand]
    private void OpenFile(FileRowViewModel? file)
    {
        file ??= SelectedFile;
        if (file is not null && Target is { } folder)
        {
            Open(new LogTarget(file.Entry.Path, file.Name, Strings.FormatViewer_FileFrom(folder.Title, folder.Subtitle)));
        }
    }

    [RelayCommand]
    private async Task OpenFileWithDefaultAppAsync(FileRowViewModel? file)
    {
        file ??= SelectedFile;
        if (file is not null)
        {
            ActionMessage = await FileOpener.OpenAsync(services.Shell, file.Entry).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void OpenTargetFile()
    {
        if (Kind == DetailsKind.File && Target is { Path: { } path } target)
        {
            Open(new LogTarget(path, target.Title, target.Subtitle));
        }
    }

    [RelayCommand]
    private async Task OpenTargetWithDefaultAppAsync()
    {
        if (Kind == DetailsKind.File && Target?.Path is { } path)
        {
            ActionMessage = await FileOpener.OpenAsync(services.Shell, path, Target.Title).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task RevealFileAsync(FileRowViewModel? file)
    {
        file ??= SelectedFile;
        if (file is not null)
        {
            var opened = await services.Shell.RevealInFolderAsync(file.Entry.Path).ConfigureAwait(true);
            ActionMessage = opened ? null : Strings.Details_ExplorerFailed;
        }
    }

    [RelayCommand]
    private async Task CopyFilePathAsync(FileRowViewModel? file)
    {
        file ??= SelectedFile;
        if (file is not null)
        {
            await services.Interaction.CopyTextAsync(file.Entry.Path.Display).ConfigureAwait(true);
            ActionMessage = Strings.Details_PathCopied;
        }
    }

    private Task LoadFilesAfterDelayAsync()
    {
        return LoadFilesAsync(ListingDelay);
    }

    private async Task LoadFilesAsync(TimeSpan delay)
    {
        if (Target?.Path is not { } path || Target.Kind != Services.Settings.FavouriteKind.Folder)
        {
            return;
        }

        CancelListing();
        var listing = new CancellationTokenSource();
        _listing = listing;
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, services.Time, listing.Token).ConfigureAwait(true);
            }

            FilesState = FilesState.Loading;
            var result = await services.Listing.ListAsync(path, Target.LogFilePattern, listing.Token).ConfigureAwait(true);
            if (!listing.IsCancellationRequested)
            {
                ShowListing(result);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    private void ShowListing(FolderListing listing)
    {
        Files.Clear();
        if (listing.Reachability != Reachability.Reachable)
        {
            FilesState = FilesState.Failed;
            FilesMessage = StatusText.Remedy(listing.Reachability);
            return;
        }

        var now = services.Time.GetUtcNow();
        foreach (var entry in listing.NewestFirst)
        {
            Files.Add(new FileRowViewModel(entry, now));
        }

        FilesState = Files.Count == 0 ? FilesState.Empty : FilesState.Loaded;
        FilesMessage = FilesSummary(listing);
    }

    private string FilesSummary(FolderListing listing)
    {
        if (listing.MatchingFileCount == 0)
        {
            return Target?.LogFilePattern is { } pattern ? Strings.FormatDetails_NoFilesMatch(pattern) : Strings.Details_FolderEmpty;
        }

        return listing.IsTruncated
            ? Strings.FormatDetails_FilesTruncated(listing.NewestFirst.Count, listing.MatchingFileCount)
            : Strings.FormatDetails_FileCount(listing.MatchingFileCount);
    }

    private void CancelListing()
    {
        _listing?.Cancel();
        _listing?.Dispose();
        _listing = null;
        FilesState = FilesState.NotLoaded;
    }
}
