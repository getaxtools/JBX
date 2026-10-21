using Jbx.Platform;

namespace Jbx.Services.Logs;

/// <summary>
/// What a follow step found. <see cref="WasTruncated"/> means the file got shorter (rotated or cleared)
/// and the document must be loaded again. Lines from <see cref="FirstChangedLine"/> on are new or grew.
/// </summary>
public sealed record LogRefresh(Reachability Reachability, int FirstChangedLine, int LineCount)
{
    public bool WasTruncated { get; init; }

    public bool HasChanges => FirstChangedLine < LineCount;
}
