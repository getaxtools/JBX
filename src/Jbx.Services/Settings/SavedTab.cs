using System.Collections.Immutable;

namespace Jbx.Services.Settings;

/// <summary>One environment tab. <see cref="SelectedFile"/> is -1 when the folders were showing.</summary>
public sealed record SavedTab(string EnvironmentId)
{
    public ImmutableArray<SavedFile> Files { get; init; } = [];

    public int SelectedFile { get; init; } = -1;
}
