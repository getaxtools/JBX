using System.Reflection;

namespace Jbx.Architecture.Tests;

internal sealed record ProductAssembly(string ProjectName, string AssemblyName)
{
    public static readonly ProductAssembly Core = new("Jbx.Core", "Jbx.Core");
    public static readonly ProductAssembly Platform = new("Jbx.Platform", "Jbx.Platform");
    public static readonly ProductAssembly PlatformWindows = new("Jbx.Platform.Windows", "Jbx.Platform.Windows");
    public static readonly ProductAssembly Services = new("Jbx.Services", "Jbx.Services");
    public static readonly ProductAssembly Desktop = new("Jbx.Desktop", "JBX");

    public static IReadOnlyList<ProductAssembly> All { get; } =
        [Core, Platform, PlatformWindows, Services, Desktop];

    public static IReadOnlyList<ProductAssembly> AllExcept(ProductAssembly excluded)
    {
        return All.Where(product => product != excluded).ToList();
    }

    public Assembly Load()
    {
        return Assembly.Load(AssemblyName);
    }

    public override string ToString()
    {
        return ProjectName;
    }
}
