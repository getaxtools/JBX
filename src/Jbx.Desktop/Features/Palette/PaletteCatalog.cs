using Avalonia.Input;
using Jbx.Desktop.Features.LogViewer;
using Jbx.Desktop.Features.Shell;
using Jbx.Desktop.Features.Tabs;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Features.Palette;

/// <summary>The commands the palette lists, read from the window, the current tab and the file being viewed.</summary>
internal static class PaletteCatalog
{
    public static IEnumerable<PaletteEntry> For(MainWindowViewModel shell)
    {
        var entries = new List<PaletteEntry>();
        if (shell.SelectedTab is { SelectedDocument: { } document } tab)
        {
            entries.AddRange(ForFile(document, tab));
        }

        if (shell.SelectedTab is { } current)
        {
            entries.AddRange(ForTab(current));
        }

        entries.AddRange(ForWindow(shell));
        entries.AddRange(Navigation(shell));
        return entries;
    }

    private static IEnumerable<PaletteEntry> ForFile(LogViewerViewModel document, TabViewModel tab)
    {
        var category = Strings.Palette_File;
        yield return new PaletteEntry(Strings.Palette_NextMatch, category, document.NextMatchCommand) { Shortcut = Describe(CommandCatalog.NextMatch) };
        yield return new PaletteEntry(Strings.Palette_PreviousMatch, category, document.PreviousMatchCommand) { Shortcut = Describe(CommandCatalog.PreviousMatch) };
        yield return new PaletteEntry(Strings.Viewer_GoToLine, category, document.GoToLineCommand) { Shortcut = Describe(CommandCatalog.GoToLine) };
        yield return new PaletteEntry(Strings.Palette_ToggleFollow, category, document.ToggleFollowCommand) { Shortcut = Describe(CommandCatalog.ToggleFollow) };
        yield return new PaletteEntry(Strings.Palette_ToggleOnlyMatches, category, document.ToggleOnlyMatchesCommand);
        yield return new PaletteEntry(Strings.Palette_ShowProblems, category, document.ShowProblemsOnlyCommand);
        yield return new PaletteEntry(Strings.Palette_ShowAllLevels, category, document.ClearLevelsCommand);
        yield return new PaletteEntry(Strings.Viewer_Reload, category, document.ReloadCommand) { Shortcut = Describe(CommandCatalog.Refresh) };
        yield return new PaletteEntry(Strings.Viewer_OpenWithDefaultApp, category, document.OpenWithDefaultAppCommand);
        yield return new PaletteEntry(Strings.Details_ShowInFolder, category, document.ShowInFolderCommand);
        yield return new PaletteEntry(Strings.Palette_CopyFilePath, category, document.CopyPathCommand);
        yield return new PaletteEntry(Strings.Tab_CloseFile, category, tab.CloseDocumentCommand) { Shortcut = Describe(CommandCatalog.CloseTab), Parameter = document };
        yield return new PaletteEntry(Strings.Palette_ShowFolders, category, tab.ShowFoldersCommand);
    }

    private static IEnumerable<PaletteEntry> ForTab(TabViewModel tab)
    {
        var category = Strings.Palette_Tab;
        yield return new PaletteEntry(Strings.Shortcut_Refresh, category, tab.RefreshFoldersCommand) { Shortcut = Describe(CommandCatalog.Refresh) };
        yield return new PaletteEntry(Strings.Shortcut_ExpandAll, category, tab.ExpandAllCommand) { Shortcut = "*" };
        yield return new PaletteEntry(Strings.CommandBar_CollapseAll, category, tab.CollapseAllCommand);
        yield return new PaletteEntry(Strings.Toolbar_ClearFilters, category, tab.ClearFiltersCommand);
        yield return new PaletteEntry(Strings.Palette_OpenSelectedFile, category, tab.OpenFileCommand);
        yield return new PaletteEntry(Strings.Shortcut_OpenInExplorer, category, tab.OpenInExplorerCommand) { Shortcut = Describe(CommandCatalog.OpenInExplorer) };
        yield return new PaletteEntry(Strings.Shortcut_CopyPath, category, tab.CopyPathCommand) { Shortcut = Describe(CommandCatalog.CopyPath) };
        yield return new PaletteEntry(Strings.Shortcut_ToggleFavourite, category, tab.ToggleFavouriteCommand) { Shortcut = Describe(CommandCatalog.ToggleFavourite) };
        foreach (var environment in tab.Environments.Where(environment => environment.Id != tab.Environment.Id))
        {
            yield return new PaletteEntry(Strings.FormatPalette_SwitchEnvironment(environment.DisplayName), Strings.Palette_Environment, tab.SwitchEnvironmentCommand) { Parameter = environment };
        }
    }

    private static IEnumerable<PaletteEntry> ForWindow(MainWindowViewModel shell)
    {
        var category = Strings.Palette_General;
        yield return new PaletteEntry(Strings.Shortcut_NewTab, category, shell.NewTabCommand) { Shortcut = Describe(CommandCatalog.NewTab) };
        yield return new PaletteEntry(Strings.Shortcut_CloseTab, category, shell.CloseTabCommand) { Shortcut = Describe(CommandCatalog.CloseTab) };
        yield return new PaletteEntry(Strings.Shortcut_NextTab, category, shell.NextTabCommand) { Shortcut = Describe(CommandCatalog.NextTab) };
        yield return new PaletteEntry(Strings.Shortcut_PreviousTab, category, shell.PreviousTabCommand) { Shortcut = Describe(CommandCatalog.PreviousTab) };
        yield return new PaletteEntry(Strings.Palette_MoveTabLeft, category, shell.MoveTabLeftCommand) { Shortcut = Describe(CommandCatalog.MoveTabLeft) };
        yield return new PaletteEntry(Strings.Palette_MoveTabRight, category, shell.MoveTabRightCommand) { Shortcut = Describe(CommandCatalog.MoveTabRight) };
        yield return new PaletteEntry(Strings.Shortcut_Search, category, shell.FocusSearchCommand) { Shortcut = Describe(CommandCatalog.FocusSearch) };
        yield return new PaletteEntry(Strings.Shortcut_OpenConfig, category, shell.OpenConfigCommand) { Shortcut = Describe(CommandCatalog.OpenConfig) };
        yield return new PaletteEntry(Strings.Shortcut_ReloadConfig, category, shell.ReloadConfigCommand) { Shortcut = Describe(CommandCatalog.ReloadConfig) };
        yield return new PaletteEntry(Strings.Palette_EditConfig, category, shell.EditConfigCommand) { Shortcut = Describe(CommandCatalog.EditConfig) };
        yield return new PaletteEntry(Strings.Shortcut_ToggleFavouritesPane, category, shell.ToggleFavouritesPaneCommand) { Shortcut = Describe(CommandCatalog.ToggleFavouritesPane) };
        yield return new PaletteEntry(Strings.Shortcut_AddFavouritePath, category, shell.Favourites.AddPathCommand) { Shortcut = Describe(CommandCatalog.AddFavouritePath) };
        yield return new PaletteEntry(Strings.Shortcut_About, category, shell.ShowAboutCommand) { Shortcut = Describe(CommandCatalog.About) };
    }

    private static IEnumerable<PaletteEntry> Navigation(MainWindowViewModel shell)
    {
        foreach (var tab in shell.Tabs.Where(tab => !ReferenceEquals(tab, shell.SelectedTab)))
        {
            yield return new PaletteEntry(Strings.FormatPalette_GoToTab(tab.Header), Strings.Palette_Navigate, shell.SelectTabCommand) { Parameter = tab };
        }

        if (shell.SelectedTab is { } current)
        {
            foreach (var document in current.Documents.Where(document => !ReferenceEquals(document, current.SelectedDocument)))
            {
                yield return new PaletteEntry(Strings.FormatPalette_GoToFile(document.Title), Strings.Palette_Navigate, current.SelectDocumentCommand) { Parameter = document };
            }
        }

        foreach (var path in shell.RecentConfigs.Where(path => path != shell.ConfigPathText))
        {
            yield return new PaletteEntry(Strings.FormatPalette_OpenRecent(path), Strings.Palette_RecentConfigs, shell.OpenRecentConfigCommand) { Parameter = path };
        }
    }

    private static string Describe(KeyGesture gesture)
    {
        return CommandCatalog.Describe(gesture);
    }
}
