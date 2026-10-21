namespace Jbx.Platform;

/// <summary>The outcome of one bounded check. <see cref="ErrorCode"/> is the Windows error behind an Unknown result.</summary>
public sealed record ProbeResult(Reachability Reachability, int? ErrorCode = null)
{
    public static ProbeResult Reachable { get; } = new(Platform.Reachability.Reachable);

    public static ProbeResult TimedOut { get; } = new(Platform.Reachability.Timeout);
}
