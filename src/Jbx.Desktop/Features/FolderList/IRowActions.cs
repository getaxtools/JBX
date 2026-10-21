using System.Windows.Input;

namespace Jbx.Desktop.Features.FolderList;

/// <summary>What a row's chevron and context menu can do. Menus live in a popup outside the list, so rows carry this.</summary>
internal interface IRowActions
{
    ICommand OpenFileCommand { get; }

    ICommand OpenWithDefaultAppCommand { get; }

    ICommand OpenInExplorerCommand { get; }

    ICommand CopyPathCommand { get; }

    ICommand ToggleFavouriteCommand { get; }

    ICommand ToggleExpandCommand { get; }
}
