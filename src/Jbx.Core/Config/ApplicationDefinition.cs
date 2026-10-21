using System.Collections.Immutable;

namespace Jbx.Core.Config;

public sealed record ApplicationDefinition(string Id, string DisplayName, ImmutableArray<FolderVariation> Folders)
{
    public string? Description { get; init; }

    public string? Owner { get; init; }

    public ImmutableArray<string> Tags { get; init; } = [];

    public string? Classification { get; init; }

    public SourceLocation? Location { get; init; }
}
