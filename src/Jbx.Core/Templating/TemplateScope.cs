using System.Collections.Immutable;

namespace Jbx.Core.Templating;

public sealed record TemplateScope(string EnvironmentId, string ApplicationId, ImmutableDictionary<string, string> Globals);
