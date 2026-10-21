using System.Collections.Immutable;
using System.Globalization;
using Jbx.Core.Templating;

namespace Jbx.Core.Config.Parsing;

internal static class ConfigDocumentMapper
{
    public static ConfigDocument? Map(ConfigNode root, ProblemContext context)
    {
        if (root is not ConfigMapping rootMapping)
        {
            context.AddError(ConfigErrorCode.ExpectedMapping, null, root.Location);
            return null;
        }

        var reader = new MappingReader(rootMapping, context);
        var versionText = reader.RequiredText("version");
        var version = int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var globals = MapGlobals(reader.OptionalMapping("globals"), context);
        var environments = reader.Sequence("environments").Select(node => MapEnvironment(node, context)).OfType<DeploymentEnvironment>();
        var applications = reader.Sequence("applications").Select(node => MapApplication(node, context)).OfType<ApplicationDefinition>();
        var document = new ConfigDocument(version, [.. environments], [.. applications]) { Globals = globals };
        reader.ReportUnknownKeys();
        return document;
    }

    private static ImmutableDictionary<string, string> MapGlobals(ConfigMapping? mapping, ProblemContext context)
    {
        if (mapping is null)
        {
            return ImmutableDictionary<string, string>.Empty;
        }

        var reader = new MappingReader(mapping, context);
        var globals = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var entry in mapping.Entries)
        {
            globals[entry.Key] = reader.OptionalText(entry.Key) ?? string.Empty;
        }

        return globals.ToImmutable();
    }

    private static DeploymentEnvironment? MapEnvironment(ConfigNode node, ProblemContext context)
    {
        if (node is not ConfigMapping mapping)
        {
            context.AddError(ConfigErrorCode.ExpectedMapping, "environments", node.Location);
            return null;
        }

        var reader = new MappingReader(mapping, context);
        var environment = new DeploymentEnvironment(
            reader.RequiredText("id"),
            reader.RequiredText("displayName"),
            reader.OptionalText("serverHost"))
        {
            Location = reader.Location,
        };
        reader.ReportUnknownKeys();
        return environment;
    }

    private static ApplicationDefinition? MapApplication(ConfigNode node, ProblemContext context)
    {
        if (node is not ConfigMapping mapping)
        {
            context.AddError(ConfigErrorCode.ExpectedMapping, "applications", node.Location);
            return null;
        }

        var applicationContext = context with { ApplicationId = PeekText(mapping, "id") };
        var reader = new MappingReader(mapping, applicationContext);
        var id = reader.RequiredText("id");
        var folders = reader.Sequence("folders").Select(folder => MapFolder(folder, applicationContext)).OfType<FolderVariation>();
        var application = new ApplicationDefinition(id, reader.RequiredText("displayName"), [.. folders])
        {
            Description = reader.OptionalText("description"),
            Owner = reader.OptionalText("owner"),
            Tags = [.. MapTags(reader.Sequence("tags"), applicationContext)],
            Classification = reader.OptionalText("classification"),
            Location = reader.Location,
        };
        reader.ReportUnknownKeys();
        return application;
    }

    private static string? PeekText(ConfigMapping mapping, string key)
    {
        return (mapping.Entries.FirstOrDefault(entry => entry.Key == key)?.Value as ConfigScalar)?.Value;
    }

    private static IEnumerable<string> MapTags(IReadOnlyList<ConfigNode> nodes, ProblemContext context)
    {
        foreach (var node in nodes)
        {
            if (node is ConfigScalar { IsNull: false } scalar)
            {
                yield return scalar.Value;
            }
            else
            {
                context.AddError(ConfigErrorCode.ExpectedText, "tags", node.Location);
            }
        }
    }

    private static FolderVariation? MapFolder(ConfigNode node, ProblemContext context)
    {
        if (node is not ConfigMapping mapping)
        {
            context.AddError(ConfigErrorCode.ExpectedMapping, "folders", node.Location);
            return null;
        }

        var reader = new MappingReader(mapping, context with { FolderLabel = PeekText(mapping, "label") });
        var folder = new FolderVariation(
            new FolderType(reader.RequiredText("type")),
            reader.RequiredText("label"),
            new PathTemplate(reader.RequiredText("pathTemplate")))
        {
            IsEnabled = reader.OptionalBoolean("enabled") ?? true,
            Notes = reader.OptionalText("notes"),
            RetentionDays = reader.OptionalInteger("retentionDays"),
            LogFilePattern = reader.OptionalText("logFilePattern"),
            ExternalLogUrl = reader.OptionalText("externalLogUrl"),
            Location = reader.Location,
        };
        reader.ReportUnknownKeys();
        return folder;
    }
}
