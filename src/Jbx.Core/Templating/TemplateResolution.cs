using Jbx.Core.Config;

namespace Jbx.Core.Templating;

/// <summary>A resolved path, or the problems that stopped it resolving; never a half-resolved path.</summary>
public sealed record TemplateResolution(string? Path, IReadOnlyList<ConfigProblem> Problems)
{
    public bool IsResolved => Path is not null;
}
