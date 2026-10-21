using Jbx.Core.Templating;

namespace Jbx.Platform.Windows;

public sealed class ProcessEnvironmentVariables : IEnvironmentVariables
{
    public string? Read(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }
}
