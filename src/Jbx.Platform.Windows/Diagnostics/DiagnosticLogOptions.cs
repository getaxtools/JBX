namespace Jbx.Platform.Windows.Diagnostics;

public sealed record DiagnosticLogOptions(string Folder)
{
    public long MaxFileBytes { get; init; } = 1_000_000;

    public long MaxTotalBytes { get; init; } = 5_000_000;

    public int MaxQueuedLines { get; init; } = 1_000;

    public TimeSpan FlushTimeout { get; init; } = TimeSpan.FromSeconds(2);
}
