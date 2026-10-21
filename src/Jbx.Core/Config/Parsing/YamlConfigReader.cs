using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Jbx.Core.Config.Parsing;

internal static class YamlConfigReader
{
    public const int MaxAliases = 100;
    public const int MaxNodes = 200_000;
    private const int MaxReportedTabLines = 10;

    public static ConfigNode? Read(string text, List<ConfigProblem> errors)
    {
        if (ReportTabIndentation(text, errors) || !PassesEventScan(text, errors))
        {
            return null;
        }

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException exception)
        {
            errors.Add(new ConfigProblem(ConfigErrorCode.YamlSyntax, exception.Message, ToLocation(exception.Start)));
            return null;
        }

        return ConvertSingleDocument(stream, errors);
    }

    private static ConfigNode? ConvertSingleDocument(YamlStream stream, List<ConfigProblem> errors)
    {
        if (stream.Documents.Count == 0)
        {
            errors.Add(new ConfigProblem(ConfigErrorCode.EmptyConfig, null, null));
            return null;
        }

        if (stream.Documents.Count > 1)
        {
            var second = stream.Documents[1].RootNode.Start;
            errors.Add(new ConfigProblem(ConfigErrorCode.MultipleDocuments, null, ToLocation(second)));
            return null;
        }

        var budget = new NodeBudget(MaxNodes);
        var root = Convert(stream.Documents[0].RootNode, budget);
        if (budget.IsExhausted)
        {
            errors.Add(new ConfigProblem(ConfigErrorCode.TooComplex, MaxNodes.ToString(System.Globalization.CultureInfo.InvariantCulture), null));
            return null;
        }

        return root;
    }

    private static bool ReportTabIndentation(string text, List<ConfigProblem> errors)
    {
        var lines = text.Split('\n');
        var reported = 0;
        for (var index = 0; index < lines.Length && reported < MaxReportedTabLines; index++)
        {
            var indentation = lines[index].Length - lines[index].TrimStart(' ', '\t').Length;
            var tabColumn = lines[index].IndexOf('\t', 0, indentation);
            if (tabColumn >= 0)
            {
                errors.Add(new ConfigProblem(ConfigErrorCode.TabIndentation, null, new SourceLocation(index + 1, tabColumn + 1)));
                reported++;
            }
        }

        return reported > 0;
    }

    private static bool PassesEventScan(string text, List<ConfigProblem> errors)
    {
        var errorCountBefore = errors.Count;
        var aliases = 0;
        var parser = new Parser(new StringReader(text));
        try
        {
            while (parser.MoveNext())
            {
                aliases += InspectEvent(parser.Current, errors);
            }
        }
        catch (YamlException exception)
        {
            errors.Add(new ConfigProblem(ConfigErrorCode.YamlSyntax, exception.Message, ToLocation(exception.Start)));
        }

        if (aliases > MaxAliases)
        {
            errors.Add(new ConfigProblem(ConfigErrorCode.TooManyAliases, MaxAliases.ToString(System.Globalization.CultureInfo.InvariantCulture), null));
        }

        return errors.Count == errorCountBefore;
    }

    private static int InspectEvent(ParsingEvent? parsingEvent, List<ConfigProblem> errors)
    {
        switch (parsingEvent)
        {
            case AnchorAlias:
                return 1;
            case NodeEvent { Tag.IsEmpty: false } tagged:
                errors.Add(new ConfigProblem(ConfigErrorCode.UnsupportedTag, tagged.Tag.ToString(), ToLocation(tagged.Start)));
                return 0;
            default:
                return 0;
        }
    }

    private static ConfigNode Convert(YamlNode node, NodeBudget budget)
    {
        if (!budget.TrySpend())
        {
            return new ConfigScalar(string.Empty, IsNull: true, null);
        }

        var location = ToLocation(node.Start);
        return node switch
        {
            YamlMappingNode mapping => new ConfigMapping(ConvertEntries(mapping, budget), location),
            YamlSequenceNode sequence => new ConfigSequence(sequence.Children.Select(child => Convert(child, budget)).ToList(), location),
            YamlScalarNode scalar => ConvertScalar(scalar, location),
            _ => new ConfigScalar(string.Empty, IsNull: true, location),
        };
    }

    private static List<ConfigMappingEntry> ConvertEntries(YamlMappingNode mapping, NodeBudget budget)
    {
        var entries = new List<ConfigMappingEntry>();
        foreach (var (key, value) in mapping.Children)
        {
            var keyText = key is YamlScalarNode scalarKey ? scalarKey.Value ?? string.Empty : string.Empty;
            entries.Add(new ConfigMappingEntry(keyText, Convert(value, budget), ToLocation(key.Start)));
        }

        return entries;
    }

    private static ConfigScalar ConvertScalar(YamlScalarNode scalar, SourceLocation location)
    {
        var value = scalar.Value ?? string.Empty;
        var isPlain = scalar.Style is ScalarStyle.Plain or ScalarStyle.Any;
        var isNull = isPlain && value is "" or "~" or "null" or "Null" or "NULL";
        return new ConfigScalar(value, isNull, location);
    }

    private static SourceLocation ToLocation(Mark mark)
    {
        return new SourceLocation((int)mark.Line, (int)mark.Column);
    }
}
