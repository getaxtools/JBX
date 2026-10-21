using System.Collections.Immutable;
using Jbx.Core.Config;
using Jbx.Platform;
using Jbx.Services.Settings;

namespace Jbx.Services.Favourites;

/// <summary>
/// The user's starred paths, shared by every tab and kept across configs. Keyed by canonical path,
/// so the same folder reached through a mapped drive and through its UNC path is one favourite.
/// </summary>
public sealed class FavouritesService
{
    private readonly SettingsService _settings;
    private readonly IPathCanonicalizer _paths;
    private ImmutableArray<Favourite> _lastSeen;

    public FavouritesService(SettingsService settings, IPathCanonicalizer paths)
    {
        _settings = settings;
        _paths = paths;
        _lastSeen = settings.Current.Favourites;
        settings.Changed += OnSettingsChanged;
    }

    /// <summary>Raised when the favourites change, not on every settings write, so the pane is not rebuilt for nothing.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<FavouriteItem> All =>
        _settings.Current.Favourites
            .Select(favourite => _paths.TryCanonicalize(favourite.Path) is { } path ? new FavouriteItem(favourite, path) : null)
            .OfType<FavouriteItem>()
            .ToList();

    public bool IsFavourite(CanonicalPath path)
    {
        return IndexOf(path) >= 0;
    }

    public async Task<bool> ToggleAsync(FavouriteRequest request, CancellationToken cancellationToken)
    {
        if (IsFavourite(request.Path))
        {
            await RemoveAsync(request.Path, cancellationToken).ConfigureAwait(false);
            return false;
        }

        var favourite = new Favourite(request.Path.Display, request.Label, request.Kind) { Origin = request.Origin };
        await _settings.UpdateAsync(current => current with { Favourites = current.Favourites.Add(favourite) }, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<AddFavouriteStatus> AddAsync(string pathText, FavouriteKind kind, CancellationToken cancellationToken)
    {
        var path = _paths.TryCanonicalize(pathText);
        if (path is null)
        {
            return AddFavouriteStatus.InvalidPath;
        }

        if (IsFavourite(path))
        {
            return AddFavouriteStatus.AlreadyAFavourite;
        }

        var favourite = new Favourite(path.Display, _paths.FileName(path), kind);
        await _settings.UpdateAsync(current => current with { Favourites = current.Favourites.Add(favourite) }, cancellationToken).ConfigureAwait(false);
        return AddFavouriteStatus.Added;
    }

    public Task RenameAsync(CanonicalPath path, string label, CancellationToken cancellationToken)
    {
        var trimmed = label.Trim();
        return trimmed.Length == 0
            ? Task.CompletedTask
            : ReplaceAsync(path, favourite => favourite with { Label = trimmed }, cancellationToken);
    }

    public async Task MoveAsync(CanonicalPath path, int newIndex, CancellationToken cancellationToken)
    {
        var index = IndexOf(path);
        if (index < 0)
        {
            return;
        }

        await _settings.UpdateAsync(current =>
        {
            var favourite = current.Favourites[index];
            var without = current.Favourites.RemoveAt(index);
            return current with { Favourites = without.Insert(Math.Clamp(newIndex, 0, without.Length), favourite) };
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RemovedFavourite?> RemoveAsync(CanonicalPath path, CancellationToken cancellationToken)
    {
        var index = IndexOf(path);
        if (index < 0)
        {
            return null;
        }

        var removed = new RemovedFavourite(_settings.Current.Favourites[index], index);
        await _settings.UpdateAsync(current => current with { Favourites = current.Favourites.RemoveAt(index) }, cancellationToken).ConfigureAwait(false);
        return removed;
    }

    public Task UndoRemoveAsync(RemovedFavourite removed, CancellationToken cancellationToken)
    {
        return _settings.UpdateAsync(
            current => current with { Favourites = current.Favourites.Insert(Math.Clamp(removed.Index, 0, current.Favourites.Length), removed.Favourite) },
            cancellationToken);
    }

    public static bool IsInConfig(Favourite favourite, ConfigDocument? document)
    {
        if (favourite.Origin is not { } origin)
        {
            return true;
        }

        return document is not null
            && document.FindEnvironment(origin.EnvironmentId) is not null
            && document.Applications.Any(application => application.Id == origin.ApplicationId
                && application.Folders.Any(folder => folder.Label == origin.FolderLabel));
    }

    private int IndexOf(CanonicalPath path)
    {
        var favourites = _settings.Current.Favourites;
        for (var index = 0; index < favourites.Length; index++)
        {
            if (_paths.TryCanonicalize(favourites[index].Path) == path)
            {
                return index;
            }
        }

        return -1;
    }

    private async Task ReplaceAsync(CanonicalPath path, Func<Favourite, Favourite> change, CancellationToken cancellationToken)
    {
        var index = IndexOf(path);
        if (index >= 0)
        {
            await _settings.UpdateAsync(current => current with { Favourites = current.Favourites.SetItem(index, change(current.Favourites[index])) }, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Compared by content: every settings write rebuilds the array.
        var current = _settings.Current.Favourites;
        var unchanged = current.IsDefaultOrEmpty || _lastSeen.IsDefaultOrEmpty
            ? current.IsDefaultOrEmpty && _lastSeen.IsDefaultOrEmpty
            : current.SequenceEqual(_lastSeen);
        if (unchanged)
        {
            return;
        }

        _lastSeen = current;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
