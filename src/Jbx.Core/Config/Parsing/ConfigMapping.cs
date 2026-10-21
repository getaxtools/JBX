namespace Jbx.Core.Config.Parsing;

internal sealed record ConfigMapping(IReadOnlyList<ConfigMappingEntry> Entries, SourceLocation? Location)
    : ConfigNode(Location);
