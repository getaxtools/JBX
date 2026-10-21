using System.Windows.Input;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Features.Palette;

/// <summary>One command the palette offers: what it is called, where it belongs, its shortcut and what it runs.</summary>
internal sealed record PaletteEntry(string Title, string Category, ICommand Command)
{
    public string? Shortcut { get; init; }

    public object? Parameter { get; init; }

    public bool HasShortcut => Shortcut is not null;

    public string AccessibleDescription => Shortcut is null ? Category : Strings.FormatPalette_Description(Category, Shortcut);

    public override string ToString()
    {
        return Title;
    }
}
