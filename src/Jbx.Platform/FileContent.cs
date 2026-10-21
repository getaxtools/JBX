namespace Jbx.Platform;

public sealed record FileContent(Reachability Reachability, ReadOnlyMemory<byte> Bytes)
{
    public bool IsTooLarge { get; init; }

    public int? ErrorCode { get; init; }

    public static FileContent Failed(ProbeResult probe)
    {
        return new FileContent(probe.Reachability, ReadOnlyMemory<byte>.Empty) { ErrorCode = probe.ErrorCode };
    }
}
