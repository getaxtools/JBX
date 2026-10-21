using Jbx.Platform;
using Jbx.Services.Settings;

namespace Jbx.Desktop.Features.Details;

/// <summary>A folder the details panel can show, whether it came from a config row or from a favourite.</summary>
internal sealed record DetailsTarget(string Title, string Subtitle, CanonicalPath? Path)
{
    public string? TypeText { get; init; }

    public string? Notes { get; init; }

    public int? RetentionDays { get; init; }

    public string? LogFilePattern { get; init; }

    public FavouriteOrigin? Origin { get; init; }

    public FavouriteKind Kind { get; init; } = FavouriteKind.Folder;

    public string FavouriteLabel { get; init; } = string.Empty;
}
