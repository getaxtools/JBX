using Xunit;

namespace Jbx.Architecture.Tests;

public sealed class FileSystemConfinementTests
{
    private static readonly string[] FileAccessTypes =
    [
        "System.IO.File",
        "System.IO.Directory",
        "System.IO.FileInfo",
        "System.IO.DirectoryInfo",
        "System.IO.FileSystemInfo",
        "System.IO.FileStream",
        "System.IO.DriveInfo",
        "System.IO.FileSystemWatcher",
        "System.IO.RandomAccess",
        "System.IO.Enumeration.FileSystemEnumerable`1",
        "System.IO.Enumeration.FileSystemEnumerator`1",
    ];

    public static TheoryData<string> ProductsOutsidePlatformWindows()
    {
        return new TheoryData<string>(ProductAssembly.AllExcept(ProductAssembly.PlatformWindows)
            .Select(product => product.ProjectName));
    }

    [Theory]
    [MemberData(nameof(ProductsOutsidePlatformWindows))]
    public void Only_the_windows_platform_project_touches_the_file_system_directly(string projectName)
    {
        var product = ProductAssembly.All.Single(p => p.ProjectName == projectName);

        var violations = AssemblyMetadata.ReferencedTypes(product)
            .Where(type => FileAccessTypes.Contains(type.FullName))
            .Select(type => type.FullName)
            .Distinct()
            .ToList();

        Assert.Empty(violations);
    }
}
