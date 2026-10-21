namespace Jbx.Platform;

public sealed record FileStat(Reachability Reachability, long Length, DateTimeOffset LastWriteUtc)
{
    public int? ErrorCode { get; init; }

    public static FileStat Failed(ProbeResult probe)
    {
        return new FileStat(probe.Reachability, 0, DateTimeOffset.MinValue) { ErrorCode = probe.ErrorCode };
    }
}
