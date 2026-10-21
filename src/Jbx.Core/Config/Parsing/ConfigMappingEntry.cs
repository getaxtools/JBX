namespace Jbx.Core.Config.Parsing;

internal sealed record ConfigMappingEntry(string Key, ConfigNode Value, SourceLocation? KeyLocation);
