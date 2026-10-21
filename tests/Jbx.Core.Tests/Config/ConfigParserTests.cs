using System.Diagnostics;
using System.Globalization;
using System.Text;
using Jbx.Core.Config;
using Jbx.Testing;
using Xunit;

namespace Jbx.Core.Tests.Config;

public sealed class ConfigParserTests
{
    [Fact]
    public void Reads_the_same_document_from_equivalent_yaml_and_json()
    {
        var fromYaml = Parse("config/valid-full.yaml");
        var fromJson = Parse("config/valid-full.json");

        Assert.Empty(fromYaml.Errors);
        Assert.Empty(fromJson.Errors);
        Assert.Equal(Describe(fromYaml.Document!), Describe(fromJson.Document!));
    }

    [Fact]
    public void Keeps_every_field_of_a_folder_variation()
    {
        var document = Parse("config/valid-full.yaml").Document!;

        var billing = document.Applications.Single(application => application.Id == "billing-portal");
        var api = billing.Folders[0];
        var etl = billing.Folders[2];
        Assert.Equal("api", api.Type.Key);
        Assert.Equal("api-*.log", api.LogFilePattern);
        Assert.Equal(30, api.RetentionDays);
        Assert.Equal("rotates daily", api.Notes);
        Assert.Equal(@"{{shareRoot}}\{{env}}\BillingPortal\Api\logs", api.PathTemplate.Text);
        Assert.True(etl.Type.IsCustom);
        Assert.False(etl.IsEnabled);
        Assert.Equal("sox", billing.Classification);
        Assert.Equal(["finance", "tier1"], billing.Tags);
        Assert.Equal(@"\\fs01.contoso.test\AppLogs", document.Globals["shareRoot"]);
    }

    [Fact]
    public void Reports_a_tab_in_indentation_with_its_line_instead_of_a_generic_syntax_error()
    {
        var result = ParseText("config.yaml", "version: 1\nenvironments:\n\t- id: dev\n");

        var problem = Assert.Single(result.Errors);
        Assert.Equal(ConfigErrorCode.TabIndentation, problem.Code);
        Assert.Equal(3, problem.Location?.Line);
    }

    [Fact]
    public void Reports_yaml_syntax_errors_with_a_position()
    {
        var result = ParseText("config.yaml", "version: 1\nenvironments: [dev\n");

        var problem = Assert.Single(result.Errors);
        Assert.Equal(ConfigErrorCode.YamlSyntax, problem.Code);
        Assert.NotNull(problem.Location);
    }

    [Fact]
    public void Reports_json_syntax_errors_with_a_line_number()
    {
        var result = ParseText("config.json", "{\n  \"version\": 1,\n  \"environments\": [\n}\n");

        var problem = Assert.Single(result.Errors);
        Assert.Equal(ConfigErrorCode.JsonSyntax, problem.Code);
        Assert.Equal(4, problem.Location?.Line);
    }

    [Fact]
    public void Stops_an_alias_expansion_bomb_quickly()
    {
        var stopwatch = Stopwatch.StartNew();

        var result = Parse("config/alias-bomb.yaml");

        Assert.Contains(result.Errors, problem => problem.Code == ConfigErrorCode.TooComplex);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public void Rejects_explicit_yaml_tags_that_could_name_a_type()
    {
        var result = ParseText("config.yaml", "version: 1\nenvironments: !!python/object:os.system [dev]\n");

        Assert.Contains(result.Errors, problem => problem.Code == ConfigErrorCode.UnsupportedTag);
        Assert.Null(result.Document);
    }

    [Fact]
    public void Rejects_a_file_over_the_size_limit_before_parsing_it()
    {
        var oversized = new byte[ConfigParser.MaxConfigSizeBytes + 1];

        var result = ConfigParser.Parse(new ConfigSource("huge.yaml", oversized));

        Assert.Equal(ConfigErrorCode.ConfigTooLarge, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("empty.yaml", "")]
    [InlineData("blank.json", "   ")]
    public void Reports_an_empty_file_as_empty(string fileName, string content)
    {
        var result = ParseText(fileName, content);

        Assert.Equal(ConfigErrorCode.EmptyConfig, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Warns_about_unknown_keys_so_newer_configs_still_open()
    {
        var result = ParseText("config.yaml", "version: 1\nlayout: wide\nenvironments:\n  - id: dev\n    displayName: Dev\n    colour: blue\n");

        Assert.Empty(result.Errors);
        Assert.Equal(["layout", "colour"], result.Warnings.Select(warning => warning.Subject));
    }

    [Fact]
    public void Names_the_missing_field_and_the_application_it_belongs_to()
    {
        var result = ParseText("config.yaml", "version: 1\napplications:\n  - id: billing-portal\n    folders:\n      - type: api\n        label: Billing API\n");

        Assert.Contains(result.Errors, problem => problem is { Code: ConfigErrorCode.MissingField, Subject: "displayName", ApplicationId: "billing-portal" });
        Assert.Contains(result.Errors, problem => problem is { Code: ConfigErrorCode.MissingField, Subject: "pathTemplate", FolderLabel: "Billing API" });
    }

    [Fact]
    public void Reports_values_of_the_wrong_kind()
    {
        var result = ParseText("config.yaml", "version: 1\nenvironments: dev\napplications:\n  - id: a\n    displayName: A\n    folders:\n      - type: api\n        label: L\n        pathTemplate: P\n        enabled: maybe\n        retentionDays: ten\n");

        Assert.Equal(
            [ConfigErrorCode.ExpectedSequence, ConfigErrorCode.InvalidBoolean, ConfigErrorCode.InvalidNumber],
            result.Errors.Select(problem => problem.Code));
    }

    [Fact]
    public void Picks_the_format_from_the_extension_then_from_the_content()
    {
        Assert.Equal(ConfigFormat.Json, ConfigParser.DetectFormat("team.JSON", "version: 1"));
        Assert.Equal(ConfigFormat.Yaml, ConfigParser.DetectFormat("team.yml", "{}"));
        Assert.Equal(ConfigFormat.Json, ConfigParser.DetectFormat("team.config", "  {\"version\": 1}"));
        Assert.Equal(ConfigFormat.Yaml, ConfigParser.DetectFormat("team.config", "version: 1"));
    }

    private static ConfigLoadResult Parse(string fixture)
    {
        return ConfigParser.Parse(new ConfigSource(Path.GetFileName(fixture), Fixture.ReadBytes(fixture)));
    }

    private static ConfigLoadResult ParseText(string fileName, string text)
    {
        return ConfigParser.Parse(new ConfigSource(fileName, Encoding.UTF8.GetBytes(text)));
    }

    private static string Describe(ConfigDocument document)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(";", document.Globals.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
        foreach (var environment in document.Environments)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{environment.Id}|{environment.DisplayName}|{environment.ServerHost}");
        }

        foreach (var application in document.Applications)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{application.Id}|{application.DisplayName}|{application.Description}|{application.Owner}|{string.Join(",", application.Tags)}|{application.Classification}");
            foreach (var folder in application.Folders)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"  {folder.Type}|{folder.Label}|{folder.PathTemplate}|{folder.IsEnabled}|{folder.Notes}|{folder.RetentionDays}|{folder.LogFilePattern}|{folder.ExternalLogUrl}");
            }
        }

        return text.ToString();
    }
}
