namespace Jbx.Core.Config.Parsing;

internal sealed record ConfigSequence(IReadOnlyList<ConfigNode> Items, SourceLocation? Location) : ConfigNode(Location);
