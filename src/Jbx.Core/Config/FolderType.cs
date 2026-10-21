namespace Jbx.Core.Config;

public readonly record struct FolderType(string Key)
{
    public const string CustomPrefix = "custom:";

    public static IReadOnlyList<string> BuiltInKeys { get; } =
        ["env", "client", "api", "web", "exe", "backgroundservice", "python", "node", "dotnetcore"];

    public bool IsCustom => Key.StartsWith(CustomPrefix, StringComparison.Ordinal);

    public bool IsBuiltIn => BuiltInKeys.Contains(Key);

    public override string ToString()
    {
        return Key;
    }
}
