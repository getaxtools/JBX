namespace Jbx.Core.Config;

public sealed record DeploymentEnvironment(string Id, string DisplayName, string? ServerHost)
{
    public SourceLocation? Location { get; init; }

    public bool IsProductionLike =>
        Id.StartsWith("prod", StringComparison.OrdinalIgnoreCase)
        || DisplayName.StartsWith("prod", StringComparison.OrdinalIgnoreCase);
}
