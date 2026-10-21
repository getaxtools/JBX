using System.Reflection;
using Avalonia.Input;
using Jbx.Desktop.Infrastructure;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

public sealed class ShortcutCatalogTests
{
    private static readonly string[] SharedBetweenScopes =
    [
        nameof(CommandCatalog.RemoveFavourite),
    ];

    [Fact]
    public void Gives_every_command_its_own_gesture()
    {
        var gestures = typeof(CommandCatalog)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(KeyGesture) && !SharedBetweenScopes.Contains(property.Name))
            .Select(property => (property.Name, Gesture: (KeyGesture)property.GetValue(null)!))
            .ToList();

        var duplicates = gestures
            .GroupBy(entry => entry.Gesture)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" = ", group.Select(entry => entry.Name)))
            .ToList();

        Assert.NotEmpty(gestures);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Leaves_windows_and_text_editing_shortcuts_alone()
    {
        string[] reserved = ["Alt+F4", "Ctrl+C", "Ctrl+V", "Ctrl+X", "Ctrl+A", "Ctrl+Shift+Escape"];

        var taken = typeof(CommandCatalog)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(KeyGesture))
            .Select(property => ((KeyGesture)property.GetValue(null)!).ToString());

        Assert.Empty(taken.Intersect(reserved));
    }
}
