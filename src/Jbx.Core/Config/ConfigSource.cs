namespace Jbx.Core.Config;

/// <summary>A config file already read into memory, so parsing never touches the disk.</summary>
public sealed record ConfigSource(string FileName, ReadOnlyMemory<byte> Content);
