using CommunityToolkit.Mvvm.ComponentModel;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;
using Jbx.Services.Favourites;
using Jbx.Services.Settings;

namespace Jbx.Desktop.Features.Favourites;

internal sealed partial class FavouriteItemViewModel(FavouriteItem item, bool isInConfig) : ObservableObject
{
    public FavouriteItem Item { get; } = item;

    public CanonicalPath Path => Item.Path;

    public string Label => Item.Favourite.Label;

    public string PathText => Item.Path.Display;

    public bool IsFile => Item.Favourite.Kind == FavouriteKind.File;

    public bool IsMissingFromConfig { get; } = !isInConfig;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel), nameof(AccessibleDescription))]
    public partial Reachability Status { get; set; } = Reachability.Checking;

    public string StatusLabel => StatusText.Label(Status);

    public string AccessibleDescription => string.Join(", ", new[]
    {
        StatusLabel,
        IsFile ? Strings.Favourites_KindFile : Strings.Favourites_KindFolder,
        IsMissingFromConfig ? Strings.Favourites_NotInConfig : null,
        PathText,
    }.OfType<string>());

    public override string ToString()
    {
        return Label;
    }
}
