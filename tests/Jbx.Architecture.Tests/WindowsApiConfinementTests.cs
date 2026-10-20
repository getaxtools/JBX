using Xunit;

namespace Jbx.Architecture.Tests;

public sealed class WindowsApiConfinementTests
{
    private static readonly string[] OperatingSystemNamespaces = ["Microsoft.Win32", "Windows", "System.Management"];

    private static readonly string[] ProcessLaunchTypes =
    [
        "System.Diagnostics.Process",
        "System.Diagnostics.ProcessStartInfo",
    ];

    public static TheoryData<string> ProductsOutsidePlatformWindows()
    {
        return new TheoryData<string>(ProductAssembly.AllExcept(ProductAssembly.PlatformWindows)
            .Select(product => product.ProjectName));
    }

    [Theory]
    [MemberData(nameof(ProductsOutsidePlatformWindows))]
    public void Only_the_windows_platform_project_declares_native_calls(string projectName)
    {
        var product = ProductAssembly.All.Single(p => p.ProjectName == projectName);

        Assert.Empty(AssemblyMetadata.PlatformInvokeMethods(product));
    }

    [Theory]
    [MemberData(nameof(ProductsOutsidePlatformWindows))]
    public void Only_the_windows_platform_project_names_windows_or_process_types(string projectName)
    {
        var product = ProductAssembly.All.Single(p => p.ProjectName == projectName);

        var violations = AssemblyMetadata.ReferencedTypes(product)
            .Where(type => ProcessLaunchTypes.Contains(type.FullName)
                || OperatingSystemNamespaces.Any(ns => IsWithinNamespace(type.Namespace, ns)))
            .Select(type => type.FullName)
            .Distinct()
            .ToList();

        Assert.Empty(violations);
    }

    private static bool IsWithinNamespace(string typeNamespace, string root)
    {
        return typeNamespace == root || typeNamespace.StartsWith(root + ".", StringComparison.Ordinal);
    }
}
