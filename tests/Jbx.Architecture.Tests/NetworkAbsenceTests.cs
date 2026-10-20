using Xunit;

namespace Jbx.Architecture.Tests;

public sealed class NetworkAbsenceTests
{
    private static readonly string[] NetworkNamespaces = ["System.Net", "Microsoft.AspNetCore", "Grpc"];

    public static TheoryData<string> AllProducts()
    {
        return new TheoryData<string>(ProductAssembly.All.Select(product => product.ProjectName));
    }

    [Theory]
    [MemberData(nameof(AllProducts))]
    public void No_product_assembly_names_a_networking_type(string projectName)
    {
        var product = ProductAssembly.All.Single(p => p.ProjectName == projectName);

        var violations = AssemblyMetadata.ReferencedTypes(product)
            .Where(type => NetworkNamespaces.Any(ns => IsWithinNamespace(type.Namespace, ns)))
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
