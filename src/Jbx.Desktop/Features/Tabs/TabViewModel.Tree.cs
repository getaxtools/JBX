using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Features.FolderList;
using Jbx.Services.Rows;

namespace Jbx.Desktop.Features.Tabs;

/// <summary>Expanding and collapsing the tree, as Windows tree views do: the chevron or the arrow keys, never a double-click.</summary>
internal sealed partial class TabViewModel
{
    [RelayCommand]
    private void ToggleExpand(RowViewModel? row)
    {
        switch (row ?? SelectedRow)
        {
            case ApplicationRowViewModel application:
                SetExpanded(application, !_expanded.Contains(application.Id));
                break;
            case FolderRowViewModel folder:
                SetFolderExpanded(folder, !folder.IsExpanded);
                break;
        }
    }

    [RelayCommand]
    private void ExpandSelected()
    {
        switch (SelectedRow)
        {
            case ApplicationRowViewModel { IsExpanded: false } application:
                SetExpanded(application, expand: true);
                break;
            case FolderRowViewModel { IsExpanded: false } folder:
                SetFolderExpanded(folder, expand: true);
                break;
            case ApplicationRowViewModel or FolderRowViewModel:
                SelectFirstChild();
                break;
        }
    }

    [RelayCommand]
    private void CollapseSelected()
    {
        switch (SelectedRow)
        {
            case ApplicationRowViewModel { IsExpanded: true } application:
                SetExpanded(application, expand: false);
                break;
            case FolderRowViewModel { IsExpanded: true } folder:
                SetFolderExpanded(folder, expand: false);
                break;
            case FolderRowViewModel folder:
                SelectedRow = ApplicationRowOf(folder.Entry);
                break;
            case FileItemRowViewModel file:
                SelectedRow = FolderRowOf(file.Folder);
                break;
            case FolderNoteRowViewModel note:
                SelectedRow = FolderRowOf(note.Folder);
                break;
        }
    }

    [RelayCommand]
    private void ActivateSelected()
    {
        switch (SelectedRow)
        {
            case ApplicationRowViewModel application:
                SetExpanded(application, !application.IsExpanded);
                break;
            case FolderRowViewModel folder:
                SetFolderExpanded(folder, !folder.IsExpanded);
                break;
            case FileItemRowViewModel file:
                OpenFileCommand.Execute(file);
                break;
        }
    }

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var application in _rows.Applications)
        {
            _expanded.Add(application.Application.Id);
        }

        Rebuild();
    }

    [RelayCommand]
    private void CollapseAll()
    {
        var selected = SelectedRow;
        _expanded.Clear();
        _expandedFolders.Clear();
        Rebuild();
        if (selected is not null && !Rows.Contains(selected))
        {
            SelectedRow = ApplicationRowOf(FolderOf(selected));
        }
    }

    private void SetExpanded(ApplicationRowViewModel application, bool expand)
    {
        var selected = SelectedRow;
        if (expand)
        {
            _expanded.Add(application.Id);
        }
        else
        {
            _expanded.Remove(application.Id);
        }

        Rebuild();
        KeepSelectionVisible(selected, application);
    }

    private void SetFolderExpanded(FolderRowViewModel folder, bool expand)
    {
        var selected = SelectedRow;
        if (expand)
        {
            _expandedFolders.Add(folder.Entry.Key);
            if (!_folderFiles.ContainsKey(folder.Entry.Key))
            {
                _ = LoadFolderFilesAsync(folder.Entry);
            }
        }
        else
        {
            _expandedFolders.Remove(folder.Entry.Key);
        }

        Rebuild();
        KeepSelectionVisible(selected, folder);
    }

    /// <summary>Collapsing a node that holds the selection moves the selection to that node, as Windows does.</summary>
    private void KeepSelectionVisible(RowViewModel? selectedBefore, RowViewModel node)
    {
        if (selectedBefore is not null && !Rows.Contains(selectedBefore))
        {
            SelectedRow = node;
        }
    }

    private void ReloadExpandedFolders()
    {
        foreach (var folder in _rows.Folders.Where(folder => _expandedFolders.Contains(folder.Key)))
        {
            _ = LoadFolderFilesAsync(folder);
        }

        Rebuild();
    }

    /// <summary>Lists a folder for the tree. A reload keeps the previous files on screen until the new listing arrives.</summary>
    private async Task LoadFolderFilesAsync(FolderEntry folder)
    {
        if (folder.Path is not { } path)
        {
            _folderFiles[folder.Key] = FolderFiles.Unresolved();
            Rebuild();
            return;
        }

        var token = _checks.Token;
        _folderFiles.TryAdd(folder.Key, FolderFiles.Loading);
        try
        {
            var listing = await _services.Listing.ListAsync(path, folder.Folder.LogFilePattern, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            _folderFiles[folder.Key] = FolderFiles.From(listing);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Rebuild();
    }

    private void SelectFirstChild()
    {
        var index = SelectedRow is null ? -1 : Rows.IndexOf(SelectedRow);
        var next = index >= 0 && index + 1 < Rows.Count ? Rows[index + 1] : null;
        var isChild = (SelectedRow, next) switch
        {
            (ApplicationRowViewModel, FolderRowViewModel) => true,
            (FolderRowViewModel, FileItemRowViewModel) => true,
            _ => false,
        };
        if (isChild)
        {
            SelectedRow = next;
        }
    }

    private ApplicationRowViewModel? ApplicationRowOf(FolderEntry? folder)
    {
        return folder is null ? null : Rows.OfType<ApplicationRowViewModel>().FirstOrDefault(row => row.Id == folder.Application.Id);
    }

    private FolderRowViewModel? FolderRowOf(FolderEntry folder)
    {
        return Rows.OfType<FolderRowViewModel>().FirstOrDefault(row => row.Entry.Key == folder.Key);
    }

    private static FolderEntry? FolderOf(RowViewModel row)
    {
        return row switch
        {
            FolderRowViewModel folder => folder.Entry,
            FileItemRowViewModel file => file.Folder,
            FolderNoteRowViewModel note => note.Folder,
            _ => null,
        };
    }
}
