using Jbx.Platform;

namespace Jbx.Services.Rows;

public sealed record FolderStatus(Reachability Reachability, DateTimeOffset? CheckedAt)
{
    public static FolderStatus NotChecked { get; } = new(Reachability.Unknown, null);

    public int? ErrorCode { get; init; }
}
