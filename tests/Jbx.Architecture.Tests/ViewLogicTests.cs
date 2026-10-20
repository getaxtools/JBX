using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Xunit;

namespace Jbx.Architecture.Tests;

public sealed class ViewLogicTests
{
    private const BindingFlags DeclaredMembers = BindingFlags.DeclaredOnly | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void The_desktop_assembly_contains_views_to_check()
    {
        Assert.NotEmpty(Views());
    }

    [Fact]
    public void Views_hold_no_code_beyond_initialisation()
    {
        var violations = Views()
            .SelectMany(view => view.GetMethods(DeclaredMembers)
                .Where(method => !IsGeneratedOrInitialisation(method))
                .Select(method => $"{view.Name}.{method.Name}"))
            .ToList();

        Assert.Empty(violations);
    }

    private static List<Type> Views()
    {
        return ProductAssembly.Desktop.Load().GetTypes()
            .Where(type => type.IsSubclassOf(typeof(UserControl)) || type.IsSubclassOf(typeof(Window)))
            .ToList();
    }

    private static bool IsGeneratedOrInitialisation(MethodInfo method)
    {
        return method.Name == "InitializeComponent"
            || method.Name.StartsWith('!')
            || method.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
    }
}
