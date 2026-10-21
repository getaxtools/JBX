using Avalonia.Input;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Infrastructure;

/// <summary>Command bar tooltips: the command's name with its shortcut, as Windows command bars show them.</summary>
internal static class CommandTips
{
    public static string OpenFile => Strings.FormatCommandBar_Tip(Strings.Details_OpenFile, Strings.CommandBar_EnterKey);

    public static string OpenInExplorer => WithShortcut(Strings.CommandBar_OpenInExplorer, CommandCatalog.OpenInExplorer);

    public static string CopyPath => WithShortcut(Strings.Details_CopyPath, CommandCatalog.CopyPath);

    public static string ToggleFavourite => WithShortcut(Strings.CommandBar_Favourite, CommandCatalog.ToggleFavourite);

    public static string ExpandAll => WithShortcut(Strings.CommandBar_ExpandAll, CommandCatalog.ExpandAll);

    public static string CollapseAll => Strings.CommandBar_CollapseAll;

    public static string Refresh => WithShortcut(Strings.Toolbar_Refresh, CommandCatalog.Refresh);

    public static string ToggleFollow => WithShortcut(Strings.Viewer_FollowTip, CommandCatalog.ToggleFollow);

    public static string GoToLine => WithShortcut(Strings.Viewer_GoToLine, CommandCatalog.GoToLine);

    public static string EditConfig => WithShortcut(Strings.Palette_EditConfig, CommandCatalog.EditConfig);

    public static string CommandPalette => WithShortcut(Strings.Palette_Title, CommandCatalog.CommandPalette);

    private static string WithShortcut(string name, KeyGesture gesture)
    {
        return Strings.FormatCommandBar_Tip(name, CommandCatalog.Describe(gesture));
    }
}
