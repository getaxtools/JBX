using System.Globalization;
using System.Text.Json;

namespace Jbx.Core.Config.Parsing;

internal static class JsonConfigReader
{
    private const int MaxDepth = 64;

    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = MaxDepth,
    };

    public static ConfigNode? Read(string text, List<ConfigProblem> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add(new ConfigProblem(ConfigErrorCode.EmptyConfig, null, null));
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text, Options);
            var budget = new NodeBudget(YamlConfigReader.MaxNodes);
            var root = Convert(document.RootElement, budget);
            if (budget.IsExhausted)
            {
                errors.Add(new ConfigProblem(ConfigErrorCode.TooComplex, YamlConfigReader.MaxNodes.ToString(CultureInfo.InvariantCulture), null));
                return null;
            }

            return root;
        }
        catch (JsonException exception)
        {
            var location = new SourceLocation(
                (int)(exception.LineNumber ?? 0) + 1,
                (int)(exception.BytePositionInLine ?? 0) + 1);
            errors.Add(new ConfigProblem(ConfigErrorCode.JsonSyntax, null, location));
            return null;
        }
    }

    private static ConfigNode Convert(JsonElement element, NodeBudget budget)
    {
        if (!budget.TrySpend())
        {
            return new ConfigScalar(string.Empty, IsNull: true, null);
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => new ConfigMapping(
                element.EnumerateObject().Select(property => new ConfigMappingEntry(property.Name, Convert(property.Value, budget), null)).ToList(),
                null),
            JsonValueKind.Array => new ConfigSequence(element.EnumerateArray().Select(item => Convert(item, budget)).ToList(), null),
            JsonValueKind.String => new ConfigScalar(element.GetString() ?? string.Empty, IsNull: false, null),
            JsonValueKind.Null => new ConfigScalar(string.Empty, IsNull: true, null),
            _ => new ConfigScalar(element.GetRawText(), IsNull: false, null),
        };
    }
}
