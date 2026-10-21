namespace Jbx.Core.Config.Parsing;

internal sealed record ProblemContext(List<ConfigProblem> Errors, List<ConfigProblem> Warnings)
{
    public string? ApplicationId { get; init; }

    public string? FolderLabel { get; init; }

    public void AddError(ConfigErrorCode code, string? subject, SourceLocation? location)
    {
        Errors.Add(Describe(code, subject, location));
    }

    public void AddWarning(ConfigErrorCode code, string? subject, SourceLocation? location)
    {
        Warnings.Add(Describe(code, subject, location));
    }

    private ConfigProblem Describe(ConfigErrorCode code, string? subject, SourceLocation? location)
    {
        return new ConfigProblem(code, subject, location) { ApplicationId = ApplicationId, FolderLabel = FolderLabel };
    }
}
