namespace Jbx.Services.Settings;

public sealed record Favourite(string Path, string Label, FavouriteKind Kind)
{
    public FavouriteOrigin? Origin { get; init; }
}
