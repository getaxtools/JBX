using Jbx.Core.Templating;

namespace Jbx.Core.Config;

public sealed record FolderVariation(FolderType Type, string Label, PathTemplate PathTemplate)
{
    public bool IsEnabled { get; init; } = true;

    public string? Notes { get; init; }

    public int? RetentionDays { get; init; }

    public string? LogFilePattern { get; init; }

    public string? ExternalLogUrl { get; init; }

    public SourceLocation? Location { get; init; }
}
