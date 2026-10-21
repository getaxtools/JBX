namespace Jbx.Platform;

public sealed record FileEntry(string Name, CanonicalPath Path, long SizeBytes, DateTimeOffset LastWriteUtc);
