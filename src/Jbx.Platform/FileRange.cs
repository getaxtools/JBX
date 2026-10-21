namespace Jbx.Platform;

/// <summary>Part of a file, read without locking it. <see cref="FileLength"/> is the length when it was read, so callers notice growth and truncation.</summary>
public sealed record FileRange(Reachability Reachability, ReadOnlyMemory<byte> Bytes, long FileLength)
{
    public int? ErrorCode { get; init; }

    public static FileRange Failed(ProbeResult probe)
    {
        return new FileRange(probe.Reachability, ReadOnlyMemory<byte>.Empty, 0) { ErrorCode = probe.ErrorCode };
    }
}
