namespace Jbx.Services.Settings;

public sealed record WindowPlacement
{
    public double Width { get; init; } = 1100;

    public double Height { get; init; } = 700;

    public bool IsMaximized { get; init; }

    public bool IsFavouritesPaneVisible { get; init; } = true;

    public double FavouritesPaneWidth { get; init; } = 260;

    public double DetailsPaneWidth { get; init; } = 380;
}
