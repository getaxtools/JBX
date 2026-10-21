using Avalonia.Input;

namespace Jbx.Desktop.Infrastructure;

/// <summary>Every keyboard shortcut, in one place. Key bindings, menus and tooltips all read from here.</summary>
internal static class CommandCatalog
{
    public static KeyGesture NewTab { get; } = new(Key.T, KeyModifiers.Control);

    public static KeyGesture CloseTab { get; } = new(Key.W, KeyModifiers.Control);

    public static KeyGesture CloseTabAlternative { get; } = new(Key.F4, KeyModifiers.Control);

    public static KeyGesture NextTab { get; } = new(Key.Tab, KeyModifiers.Control);

    public static KeyGesture PreviousTab { get; } = new(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift);

    public static KeyGesture NextTabAlternative { get; } = new(Key.PageDown, KeyModifiers.Control);

    public static KeyGesture PreviousTabAlternative { get; } = new(Key.PageUp, KeyModifiers.Control);

    public static KeyGesture FocusSearch { get; } = new(Key.F, KeyModifiers.Control);

    public static KeyGesture FocusSearchAlternative { get; } = new(Key.E, KeyModifiers.Control);

    public static KeyGesture Refresh { get; } = new(Key.F5);

    public static KeyGesture OpenConfig { get; } = new(Key.O, KeyModifiers.Control);

    public static KeyGesture ReloadConfig { get; } = new(Key.R, KeyModifiers.Control);

    public static KeyGesture ToggleFavourite { get; } = new(Key.D, KeyModifiers.Control);

    public static KeyGesture AddFavouritePath { get; } = new(Key.D, KeyModifiers.Control | KeyModifiers.Shift);

    public static KeyGesture ToggleFavouritesPane { get; } = new(Key.B, KeyModifiers.Control);

    public static KeyGesture OpenInExplorer { get; } = new(Key.E, KeyModifiers.Control | KeyModifiers.Shift);

    public static KeyGesture CopyPath { get; } = new(Key.C, KeyModifiers.Control | KeyModifiers.Shift);

    public static KeyGesture NextPane { get; } = new(Key.F6);

    public static KeyGesture PreviousPane { get; } = new(Key.F6, KeyModifiers.Shift);

    public static KeyGesture UndoFavourite { get; } = new(Key.Z, KeyModifiers.Control);

    public static KeyGesture RenameFavourite { get; } = new(Key.F2);

    public static KeyGesture RemoveFavourite { get; } = new(Key.Delete);

    public static KeyGesture MoveFavouriteUp { get; } = new(Key.Up, KeyModifiers.Alt);

    public static KeyGesture MoveFavouriteDown { get; } = new(Key.Down, KeyModifiers.Alt);

    public static KeyGesture ExpandAll { get; } = new(Key.Multiply);

    public static KeyGesture ContextMenu { get; } = new(Key.F10, KeyModifiers.Shift);

    public static KeyGesture About { get; } = new(Key.F1);

    public static KeyGesture CommandPalette { get; } = new(Key.P, KeyModifiers.Control | KeyModifiers.Shift);

    public static KeyGesture NextMatch { get; } = new(Key.F3);

    public static KeyGesture PreviousMatch { get; } = new(Key.F3, KeyModifiers.Shift);

    public static KeyGesture GoToLine { get; } = new(Key.G, KeyModifiers.Control);

    public static KeyGesture ToggleFollow { get; } = new(Key.L, KeyModifiers.Control);

    public static KeyGesture EditConfig { get; } = new(Key.O, KeyModifiers.Control | KeyModifiers.Shift);

    public static KeyGesture MoveTabLeft { get; } = new(Key.PageUp, KeyModifiers.Control | KeyModifiers.Shift);

    public static KeyGesture MoveTabRight { get; } = new(Key.PageDown, KeyModifiers.Control | KeyModifiers.Shift);

    public static string Describe(KeyGesture gesture)
    {
        return gesture.ToString("p", null);
    }
}
