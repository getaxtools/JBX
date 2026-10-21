using Jbx.Core.Config;
using Jbx.Desktop.Infrastructure;
using Jbx.Services.Rows;

namespace Jbx.Desktop.Tests.Harness;

internal static class ShellServicesExtensions
{
    public static EnvironmentRows Rows(this ShellServices services, ConfigDocument document, DeploymentEnvironment environment)
    {
        return EnvironmentRows.Resolve(document, environment, services.Resolver, services.Paths);
    }
}
