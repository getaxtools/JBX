namespace Jbx.Services.Logs;

/// <summary>One line as shown: its zero-based number, its text (cut at a size limit when <see cref="IsCut"/>) and its level.</summary>
public sealed record LogLine(int Number, string Text, LineLevel Level)
{
    public bool IsCut { get; init; }

    /// <summary>The last line, read before the writer finished it; its text may still grow.</summary>
    public bool IsUnfinished { get; init; }
}
