using Jbx.Platform;

namespace Jbx.Services.Logs;

/// <summary>The numbers of the matching lines, in order. <see cref="IsCapped"/> says more matched than were kept.</summary>
public sealed record LogScan(Reachability Reachability, IReadOnlyList<int> Lines)
{
    public bool IsCapped { get; init; }
}
