using Microsoft.Extensions.DependencyModel;
using Xunit;

namespace Jbx.Architecture.Tests;

public sealed class LayeringTests
{
    private static readonly Dictionary<ProductAssembly, ProductAssembly[]> AllowedProjectReferences = new()
    {
        [ProductAssembly.Core] = [],
        [ProductAssembly.Platform] = [],
        [ProductAssembly.PlatformWindows] = [ProductAssembly.Platform, ProductAssembly.Core],
        [ProductAssembly.Services] = [ProductAssembly.Core, ProductAssembly.Platform],
        [ProductAssembly.Desktop] =
        [
            ProductAssembly.Services,
            ProductAssembly.Core,
            ProductAssembly.Platform,
            ProductAssembly.PlatformWindows,
        ],
    };

    private static readonly string[] InterfacePackagePrefixes = ["Avalonia", "CommunityToolkit"];

    [Fact]
    public void Every_product_project_appears_in_the_dependency_manifest()
    {
        var libraryNames = ProjectLibraries().Select(library => library.Name).ToList();

        foreach (var product in ProductAssembly.All)
        {
            Assert.Contains(product.AssemblyName, libraryNames);
        }
    }

    [Fact]
    public void Project_references_point_only_down_the_layers()
    {
        var violations = new List<string>();

        foreach (var library in ProjectLibraries())
        {
            var product = ProductAssembly.All.Single(p => p.AssemblyName == library.Name);
            var allowed = AllowedProjectReferences[product].Select(p => p.AssemblyName).ToHashSet();
            var referencedProducts = library.Dependencies
                .Select(dependency => dependency.Name)
                .Where(name => ProductAssembly.All.Any(p => p.AssemblyName == name));

            violations.AddRange(referencedProducts
                .Where(name => !allowed.Contains(name))
                .Select(name => $"{library.Name} -> {name}"));
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Only_the_desktop_project_references_interface_packages()
    {
        var violations = ProjectLibraries()
            .Where(library => library.Name != ProductAssembly.Desktop.AssemblyName)
            .SelectMany(library => library.Dependencies
                .Where(dependency => InterfacePackagePrefixes.Any(dependency.Name.StartsWith))
                .Select(dependency => $"{library.Name} -> {dependency.Name}"))
            .ToList();

        Assert.Empty(violations);
    }

    private static IEnumerable<RuntimeLibrary> ProjectLibraries()
    {
        var manifest = DependencyContext.Default
            ?? throw new InvalidOperationException("The test host has no dependency manifest.");

        return manifest.RuntimeLibraries.Where(library =>
            library.Type == "project" && ProductAssembly.All.Any(p => p.AssemblyName == library.Name));
    }
}
