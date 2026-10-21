using Jbx.Services.Settings;

namespace Jbx.Services.Favourites;

/// <summary>What Undo needs to put a removed favourite back where it was.</summary>
public sealed record RemovedFavourite(Favourite Favourite, int Index);
