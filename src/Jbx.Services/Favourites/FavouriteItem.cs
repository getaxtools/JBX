using Jbx.Platform;
using Jbx.Services.Settings;

namespace Jbx.Services.Favourites;

public sealed record FavouriteItem(Favourite Favourite, CanonicalPath Path);
