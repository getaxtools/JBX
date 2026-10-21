using Jbx.Platform;
using Jbx.Services.Settings;

namespace Jbx.Services.Favourites;

public sealed record FavouriteRequest(CanonicalPath Path, string Label, FavouriteKind Kind)
{
    public FavouriteOrigin? Origin { get; init; }
}
