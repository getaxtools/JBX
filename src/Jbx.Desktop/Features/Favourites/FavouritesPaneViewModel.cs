using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Core.Config;
using Jbx.Desktop.Features.Dialogs;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;
using Jbx.Services.Favourites;
using Jbx.Services.Settings;

namespace Jbx.Desktop.Features.Favourites;

internal sealed partial class FavouritesPaneViewModel : ObservableObject
{
    private readonly ShellServices _services;
    private readonly Action<FavouriteItemViewModel> _open;
    private ConfigDocument? _document;
    private RemovedFavourite? _lastRemoved;
    private int _probeGeneration;

    public FavouritesPaneViewModel(ShellServices services, Action<FavouriteItemViewModel> open)
    {
        _services = services;
        _open = open;
        services.Favourites.Changed += (_, _) => services.Dispatcher.Post(Reload);
    }

    public ObservableCollection<FavouriteItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial FavouriteItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string? ActionMessage { get; set; }

    [ObservableProperty]
    public partial int FocusToken { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    public void ShowConfig(ConfigDocument? document)
    {
        _document = document;
        Reload();
    }

    public void RequestFocus()
    {
        FocusToken++;
    }

    public void Reload()
    {
        var selectedPath = SelectedItem?.Path;
        var items = _services.Favourites.All
            .Select(item => new FavouriteItemViewModel(item, FavouritesService.IsInConfig(item.Favourite, _document)))
            .ToList();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        IsEmpty = Items.Count == 0;
        SelectedItem = Items.FirstOrDefault(item => item.Path == selectedPath);
        _ = ProbeAllAsync(items);
    }

    [RelayCommand]
    private void Open(FavouriteItemViewModel? item)
    {
        if ((item ?? SelectedItem) is { } chosen)
        {
            _open(chosen);
        }
    }

    [RelayCommand]
    private async Task OpenInExplorerAsync(FavouriteItemViewModel? item)
    {
        if ((item ?? SelectedItem) is not { } chosen)
        {
            return;
        }

        var opened = chosen.IsFile
            ? await _services.Shell.RevealInFolderAsync(chosen.Path).ConfigureAwait(true)
            : await _services.Shell.OpenFolderAsync(chosen.Path).ConfigureAwait(true);
        ActionMessage = opened ? null : Strings.Details_ExplorerFailed;
    }

    [RelayCommand]
    private async Task CopyPathAsync(FavouriteItemViewModel? item)
    {
        if ((item ?? SelectedItem) is { } chosen)
        {
            await _services.Interaction.CopyTextAsync(chosen.PathText).ConfigureAwait(true);
            ActionMessage = Strings.Details_PathCopied;
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(FavouriteItemViewModel? item)
    {
        if ((item ?? SelectedItem) is not { } chosen)
        {
            return;
        }

        var index = Items.IndexOf(chosen);
        _lastRemoved = await _services.Favourites.RemoveAsync(chosen.Path, CancellationToken.None).ConfigureAwait(true);
        Reload();
        SelectedItem = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
        ActionMessage = Strings.FormatFavourites_RemovedWithUndo(chosen.Label);
        RequestFocus();
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (_lastRemoved is not { } removed)
        {
            return;
        }

        _lastRemoved = null;
        await _services.Favourites.UndoRemoveAsync(removed, CancellationToken.None).ConfigureAwait(true);
        Reload();
        SelectedItem = Items.ElementAtOrDefault(removed.Index);
        ActionMessage = Strings.FormatFavourites_Restored(removed.Favourite.Label);
    }

    [RelayCommand]
    private async Task RenameAsync(FavouriteItemViewModel? item)
    {
        if ((item ?? SelectedItem) is not { } chosen)
        {
            return;
        }

        var prompt = new TextPromptViewModel(Strings.Favourites_RenameTitle, Strings.Favourites_RenamePrompt, chosen.Label, Strings.Favourites_RenameAccept);
        var label = await _services.Interaction.AskForTextAsync(prompt).ConfigureAwait(true);
        if (label is not null)
        {
            await _services.Favourites.RenameAsync(chosen.Path, label, CancellationToken.None).ConfigureAwait(true);
            Reload();
            SelectedItem = Items.FirstOrDefault(candidate => candidate.Path == chosen.Path);
        }
    }

    [RelayCommand]
    private Task MoveUpAsync(FavouriteItemViewModel? item)
    {
        return MoveAsync(item ?? SelectedItem, -1);
    }

    [RelayCommand]
    private Task MoveDownAsync(FavouriteItemViewModel? item)
    {
        return MoveAsync(item ?? SelectedItem, 1);
    }

    [RelayCommand]
    private async Task AddPathAsync()
    {
        var prompt = new TextPromptViewModel(Strings.Favourites_AddTitle, Strings.Favourites_AddPrompt, string.Empty, Strings.Favourites_AddAccept);
        var path = await _services.Interaction.AskForTextAsync(prompt).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        var looksLikeFile = System.IO.Path.HasExtension(path.Trim().TrimEnd('\\', '/'));
        var status = await _services.Favourites.AddAsync(path, looksLikeFile ? FavouriteKind.File : FavouriteKind.Folder, CancellationToken.None).ConfigureAwait(true);
        ActionMessage = status switch
        {
            AddFavouriteStatus.Added => Strings.Favourites_Added,
            AddFavouriteStatus.AlreadyAFavourite => Strings.Favourites_AlreadyAFavourite,
            _ => Strings.Favourites_InvalidPath,
        };
        Reload();
    }

    private async Task MoveAsync(FavouriteItemViewModel? item, int offset)
    {
        if (item is null)
        {
            return;
        }

        var target = Items.IndexOf(item) + offset;
        if (target < 0 || target >= Items.Count)
        {
            return;
        }

        await _services.Favourites.MoveAsync(item.Path, target, CancellationToken.None).ConfigureAwait(true);
        Reload();
        SelectedItem = Items.FirstOrDefault(candidate => candidate.Path == item.Path);
        RequestFocus();
    }

    private async Task ProbeAllAsync(IReadOnlyList<FavouriteItemViewModel> items)
    {
        var generation = ++_probeGeneration;
        var probes = items.Select(async item =>
        {
            var result = item.IsFile
                ? await _services.Probe.ProbeFileAsync(item.Path, CancellationToken.None).ConfigureAwait(false)
                : await _services.Probe.ProbeFolderAsync(item.Path, CancellationToken.None).ConfigureAwait(false);
            _services.Dispatcher.Post(() =>
            {
                if (generation == _probeGeneration)
                {
                    item.Status = result.Reachability;
                }
            });
        });
        await Task.WhenAll(probes).ConfigureAwait(false);
    }
}
