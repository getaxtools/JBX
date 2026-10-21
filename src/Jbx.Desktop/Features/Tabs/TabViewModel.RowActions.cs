using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Features.Details;
using Jbx.Desktop.Features.FolderList;
using Jbx.Desktop.Features.LogViewer;
using Jbx.Desktop.Resources;
using Jbx.Platform;
using Jbx.Services.Favourites;
using Jbx.Services.Settings;

namespace Jbx.Desktop.Features.Tabs;

/// <summary>What the command bar, shortcuts and context menus do to a folder or file row.</summary>
internal sealed partial class TabViewModel
{
    public bool IsFavouriteSelected => SelectedRow switch
    {
        FolderRowViewModel folder => folder.IsFavourite,
        FileItemRowViewModel file => file.IsFavourite,
        _ => false,
    };

    [RelayCommand(CanExecute = nameof(IsFile))]
    private void OpenFile(RowViewModel? row)
    {
        if ((row ?? SelectedRow) is FileItemRowViewModel file)
        {
            var subtitle = Strings.FormatDetails_FileSubtitle(file.Folder.Folder.Label, file.Folder.Application.DisplayName, Environment.DisplayName);
            OpenInViewer(new LogTarget(file.File.Path, file.Name, subtitle));
        }
    }

    [RelayCommand(CanExecute = nameof(IsFile))]
    private async Task OpenWithDefaultAppAsync(RowViewModel? row)
    {
        if ((row ?? SelectedRow) is FileItemRowViewModel file)
        {
            Details.ActionMessage = await FileOpener.OpenAsync(_services.Shell, file.File).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasPath))]
    private async Task OpenInExplorerAsync(RowViewModel? row)
    {
        var opened = (row ?? SelectedRow) switch
        {
            FolderRowViewModel { Entry.Path: { } path } => await _services.Shell.OpenFolderAsync(path).ConfigureAwait(true),
            FileItemRowViewModel file => await _services.Shell.RevealInFolderAsync(file.File.Path).ConfigureAwait(true),
            _ => true,
        };
        Details.ActionMessage = opened ? null : Strings.Details_ExplorerFailed;
    }

    [RelayCommand(CanExecute = nameof(HasPath))]
    private async Task CopyPathAsync(RowViewModel? row)
    {
        if (PathOf(row) is { } path)
        {
            await _services.Interaction.CopyTextAsync(path.Display).ConfigureAwait(true);
            Details.ActionMessage = Strings.Details_PathCopied;
        }
    }

    [RelayCommand(CanExecute = nameof(HasPath))]
    private async Task ToggleFavouriteAsync(RowViewModel? row)
    {
        switch (row ?? SelectedRow)
        {
            case FolderRowViewModel { Entry.Path: { } path } folder:
                var folderRequest = new FavouriteRequest(path, FavouriteLabelFor(folder.Name, folder.Entry), FavouriteKind.Folder) { Origin = OriginFor(folder.Entry) };
                folder.IsFavourite = await _services.Favourites.ToggleAsync(folderRequest, CancellationToken.None).ConfigureAwait(true);
                SyncDetailsFavourite(path, folder.IsFavourite);
                break;
            case FileItemRowViewModel file:
                var fileRequest = new FavouriteRequest(file.File.Path, FavouriteLabelFor(file.Name, file.Folder), FavouriteKind.File) { Origin = OriginFor(file.Folder) };
                file.IsFavourite = await _services.Favourites.ToggleAsync(fileRequest, CancellationToken.None).ConfigureAwait(true);
                SyncDetailsFavourite(file.File.Path, file.IsFavourite);
                break;
        }

        OnPropertyChanged(nameof(IsFavouriteSelected));
    }

    private void SyncDetailsFavourite(CanonicalPath path, bool isFavourite)
    {
        if (Details.Target?.Path == path)
        {
            Details.IsFavourite = isFavourite;
        }
    }

    private CanonicalPath? PathOf(RowViewModel? row)
    {
        return (row ?? SelectedRow) switch
        {
            FolderRowViewModel folder => folder.Entry.Path,
            FileItemRowViewModel file => file.File.Path,
            _ => null,
        };
    }

    private bool HasPath(RowViewModel? row)
    {
        return PathOf(row) is not null;
    }

    private bool IsFile(RowViewModel? row)
    {
        return (row ?? SelectedRow) is FileItemRowViewModel;
    }
}
