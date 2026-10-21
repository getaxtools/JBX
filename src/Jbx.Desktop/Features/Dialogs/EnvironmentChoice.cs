using Jbx.Core.Config;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Features.Dialogs;

internal sealed record EnvironmentChoice(DeploymentEnvironment Environment)
{
    public string Name => Environment.DisplayName;

    public string Detail => Environment.ServerHost is { } host ? $"{Environment.Id} · {host}" : Environment.Id;

    public bool IsProduction => Environment.IsProductionLike;

    public string AccessibleName => IsProduction ? Strings.FormatTab_ProductionHeader(Name) : Name;

    public override string ToString()
    {
        return Name;
    }
}
