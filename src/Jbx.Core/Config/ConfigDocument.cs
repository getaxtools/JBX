using System.Collections.Immutable;

namespace Jbx.Core.Config;

public sealed record ConfigDocument(
    int Version,
    ImmutableArray<DeploymentEnvironment> Environments,
    ImmutableArray<ApplicationDefinition> Applications)
{
    public ImmutableDictionary<string, string> Globals { get; init; } = ImmutableDictionary<string, string>.Empty;

    public DeploymentEnvironment? FindEnvironment(string environmentId)
    {
        return Environments.FirstOrDefault(environment => environment.Id == environmentId);
    }
}
