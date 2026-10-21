using System.Collections.Immutable;

namespace Jbx.Services.Settings;

/// <summary>The tabs and files open when the window was last used, so the next start picks up where the user left off.</summary>
public sealed record SavedSession
{
    public const int MaxTabs = 20;

    public string? ConfigPath { get; init; }

    public ImmutableArray<SavedTab> Tabs { get; init; } = [];

    public int SelectedTab { get; init; }
}
