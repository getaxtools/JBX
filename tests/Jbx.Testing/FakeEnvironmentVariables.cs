using Jbx.Core.Templating;

namespace Jbx.Testing;

public sealed class FakeEnvironmentVariables(IReadOnlyDictionary<string, string> values) : IEnvironmentVariables
{
    public FakeEnvironmentVariables()
        : this(new Dictionary<string, string>())
    {
    }

    public string? Read(string name)
    {
        return values.TryGetValue(name, out var value) ? value : null;
    }
}
