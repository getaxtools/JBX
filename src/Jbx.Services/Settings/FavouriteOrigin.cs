namespace Jbx.Services.Settings;

/// <summary>Where a favourite was starred from, kept so its label still reads well after the config changes.</summary>
public sealed record FavouriteOrigin(string ApplicationId, string FolderLabel, string EnvironmentId)
{
    public string? ApplicationDisplayName { get; init; }

    public string? EnvironmentDisplayName { get; init; }
}
