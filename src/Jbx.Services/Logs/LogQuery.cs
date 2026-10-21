using System.Collections.Immutable;

namespace Jbx.Services.Logs;

/// <summary>What the viewer searches for: text or a pattern, and which levels to keep. No levels means every level.</summary>
public sealed record LogQuery
{
    public static LogQuery Everything { get; } = new();

    public string Text { get; init; } = string.Empty;

    public bool MatchCase { get; init; }

    public bool UseRegex { get; init; }

    public ImmutableHashSet<LineLevel> Levels { get; init; } = [];

    public bool HasText => Text.Length > 0;

    public bool HasLevels => !Levels.IsEmpty;
}
