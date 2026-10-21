namespace Jbx.Core.Config;

/// <summary>One error or warning in a config file. <see cref="Subject"/> is the key, value or token it is about.</summary>
public sealed record ConfigProblem(ConfigErrorCode Code, string? Subject, SourceLocation? Location)
{
    public string? ApplicationId { get; init; }

    public string? FolderLabel { get; init; }

    public string? EnvironmentId { get; init; }
}
