using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Features.Crash;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Features.About;

/// <summary>Version, licence and every shortcut, read from the one command catalog.</summary>
internal sealed partial class AboutViewModel
{
    public event EventHandler? CloseRequested;

    public string VersionText { get; } = Strings.FormatAbout_Version(CrashHandlers.AppVersion);

    public string ThirdPartyNotices { get; } = Infrastructure.ThirdPartyNotices.Read();

    public IReadOnlyList<ShortcutLine> Shortcuts { get; } =
    [
        new(Strings.Shortcut_NewTab, CommandCatalog.Describe(CommandCatalog.NewTab)),
        new(Strings.Shortcut_CloseTab, $"{CommandCatalog.Describe(CommandCatalog.CloseTab)}, {CommandCatalog.Describe(CommandCatalog.CloseTabAlternative)}"),
        new(Strings.Shortcut_NextTab, $"{CommandCatalog.Describe(CommandCatalog.NextTab)}, {CommandCatalog.Describe(CommandCatalog.NextTabAlternative)}"),
        new(Strings.Shortcut_PreviousTab, $"{CommandCatalog.Describe(CommandCatalog.PreviousTab)}, {CommandCatalog.Describe(CommandCatalog.PreviousTabAlternative)}"),
        new(Strings.Shortcut_GoToTab, "Ctrl+1 … Ctrl+8, Ctrl+9"),
        new(Strings.Shortcut_MoveTab, $"{CommandCatalog.Describe(CommandCatalog.MoveTabLeft)}, {CommandCatalog.Describe(CommandCatalog.MoveTabRight)}"),
        new(Strings.Shortcut_Search, $"{CommandCatalog.Describe(CommandCatalog.FocusSearch)}, {CommandCatalog.Describe(CommandCatalog.FocusSearchAlternative)}"),
        new(Strings.Shortcut_Refresh, CommandCatalog.Describe(CommandCatalog.Refresh)),
        new(Strings.Shortcut_OpenConfig, CommandCatalog.Describe(CommandCatalog.OpenConfig)),
        new(Strings.Shortcut_ReloadConfig, CommandCatalog.Describe(CommandCatalog.ReloadConfig)),
        new(Strings.Shortcut_ExpandCollapse, "Right, Left"),
        new(Strings.Shortcut_ExpandAll, "*"),
        new(Strings.Shortcut_ToggleFavourite, CommandCatalog.Describe(CommandCatalog.ToggleFavourite)),
        new(Strings.Shortcut_AddFavouritePath, CommandCatalog.Describe(CommandCatalog.AddFavouritePath)),
        new(Strings.Shortcut_UndoFavourite, CommandCatalog.Describe(CommandCatalog.UndoFavourite)),
        new(Strings.Shortcut_ToggleFavouritesPane, CommandCatalog.Describe(CommandCatalog.ToggleFavouritesPane)),
        new(Strings.Shortcut_OpenInExplorer, CommandCatalog.Describe(CommandCatalog.OpenInExplorer)),
        new(Strings.Shortcut_CopyPath, CommandCatalog.Describe(CommandCatalog.CopyPath)),
        new(Strings.Shortcut_NextPane, $"{CommandCatalog.Describe(CommandCatalog.NextPane)}, {CommandCatalog.Describe(CommandCatalog.PreviousPane)}"),
        new(Strings.Shortcut_ContextMenu, $"{CommandCatalog.Describe(CommandCatalog.ContextMenu)}, Menu"),
        new(Strings.Shortcut_CommandPalette, CommandCatalog.Describe(CommandCatalog.CommandPalette)),
        new(Strings.Shortcut_FindInFile, $"{CommandCatalog.Describe(CommandCatalog.NextMatch)}, {CommandCatalog.Describe(CommandCatalog.PreviousMatch)}"),
        new(Strings.Shortcut_GoToLine, CommandCatalog.Describe(CommandCatalog.GoToLine)),
        new(Strings.Shortcut_ToggleFollow, CommandCatalog.Describe(CommandCatalog.ToggleFollow)),
        new(Strings.Shortcut_EditConfig, CommandCatalog.Describe(CommandCatalog.EditConfig)),
        new(Strings.Shortcut_About, CommandCatalog.Describe(CommandCatalog.About)),
    ];

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
