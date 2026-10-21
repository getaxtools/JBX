namespace Jbx.Core.Templating;

public interface IEnvironmentVariables
{
    string? Read(string name);
}
