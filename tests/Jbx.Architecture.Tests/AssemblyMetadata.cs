using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Jbx.Architecture.Tests;

internal sealed record ReferencedType(string Namespace, string FullName);

internal static class AssemblyMetadata
{
    public static IReadOnlyList<ReferencedType> ReferencedTypes(ProductAssembly product)
    {
        using var stream = File.OpenRead(product.Load().Location);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        var types = new List<ReferencedType>();
        foreach (var handle in reader.TypeReferences)
        {
            types.Add(Describe(reader, reader.GetTypeReference(handle)));
        }

        return types;
    }

    public static IReadOnlyList<string> PlatformInvokeMethods(ProductAssembly product)
    {
        using var stream = File.OpenRead(product.Load().Location);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        var methods = new List<string>();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (method.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
            {
                var declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
                methods.Add($"{reader.GetString(declaringType.Name)}.{reader.GetString(method.Name)}");
            }
        }

        return methods;
    }

    private static ReferencedType Describe(MetadataReader reader, TypeReference type)
    {
        var name = reader.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var outer = reader.GetTypeReference((TypeReferenceHandle)type.ResolutionScope);
            var outerType = Describe(reader, outer);
            return new ReferencedType(outerType.Namespace, $"{outerType.FullName}+{name}");
        }

        var typeNamespace = reader.GetString(type.Namespace);
        var fullName = typeNamespace.Length == 0 ? name : $"{typeNamespace}.{name}";
        return new ReferencedType(typeNamespace, fullName);
    }
}
