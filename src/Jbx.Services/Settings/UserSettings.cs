using System.Collections.Immutable;

namespace Jbx.Services.Settings;

public sealed record UserSettings
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxRecentConfigs = 10;

    public static UserSettings Defaults { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string? LastConfigPath { get; init; }

    public ImmutableArray<string> RecentConfigPaths { get; init; } = [];

    public ImmutableArray<Favourite> Favourites { get; init; } = [];

    public string? LastEnvironmentId { get; init; }

    public WindowPlacement Window { get; init; } = new();

    public SavedSession? Session { get; init; }
}
