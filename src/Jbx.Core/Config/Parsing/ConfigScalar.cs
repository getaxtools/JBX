namespace Jbx.Core.Config.Parsing;

internal sealed record ConfigScalar(string Value, bool IsNull, SourceLocation? Location) : ConfigNode(Location);
