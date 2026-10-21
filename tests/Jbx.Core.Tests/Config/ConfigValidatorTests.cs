using System.Text;
using Jbx.Core.Config;
using Jbx.Core.Templating;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Core.Tests.Config;

public sealed class ConfigValidatorTests
{
    [Fact]
    public void Reports_every_problem_in_the_file_at_once()
    {
        var result = Load("several-errors.yaml", Fixture.ReadBytes("config/several-errors.yaml"));

        Assert.Equal(
            [
                ConfigErrorCode.DuplicateEnvironmentId,
                ConfigErrorCode.NegativeRetentionDays,
                ConfigErrorCode.UnknownToken,
                ConfigErrorCode.InvalidFolderType,
                ConfigErrorCode.DuplicateApplicationId,
            ],
            result.Errors.Select(problem => problem.Code));
    }

    [Fact]
    public void Accepts_the_full_example_config()
    {
        var result = Load("valid-full.yaml", Fixture.ReadBytes("config/valid-full.yaml"));

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Names_the_unknown_token_and_where_it_is_used()
    {
        var result = Load("several-errors.yaml", Fixture.ReadBytes("config/several-errors.yaml"));

        var problem = Assert.Single(result.Errors, error => error.Code == ConfigErrorCode.UnknownToken);
        Assert.Equal("missingRoot", problem.Subject);
        Assert.Equal("billing-portal", problem.ApplicationId);
        Assert.Equal("Billing API", problem.FolderLabel);
    }

    [Theory]
    [InlineData("version: 2", ConfigErrorCode.UnsupportedVersion)]
    [InlineData("environments: []", ConfigErrorCode.NoEnvironments)]
    [InlineData("environments: [{id: Prod_1, displayName: P}]", ConfigErrorCode.InvalidId)]
    [InlineData("globals: {env: x}", ConfigErrorCode.InvalidGlobalName)]
    [InlineData("globals: {2root: x}", ConfigErrorCode.InvalidGlobalName)]
    [InlineData("applications: [{id: a, displayName: A, classification: secret, folders: []}]", ConfigErrorCode.InvalidClassification)]
    [InlineData("applications: [{id: a, displayName: A, folders: [{type: api, label: L, pathTemplate: 'D:\\x', logFilePattern: 'logs\\*.log'}]}]", ConfigErrorCode.InvalidLogFilePattern)]
    [InlineData("applications: [{id: a, displayName: A, folders: [{type: api, label: L, pathTemplate: 'D:\\x', externalLogUrl: 'http://logs.contoso.test'}]}]", ConfigErrorCode.InvalidExternalLogUrl)]
    [InlineData("applications: [{id: a, displayName: A, folders: [{type: api, label: L, pathTemplate: 'D:\\x'}, {type: web, label: l, pathTemplate: 'D:\\y'}]}]", ConfigErrorCode.DuplicateFolderLabel)]
    [InlineData("applications: [{id: a, displayName: A, folders: [{type: 'custom:Bad Name', label: L, pathTemplate: 'D:\\x'}]}]", ConfigErrorCode.InvalidFolderType)]
    [InlineData("applications: [{id: a, displayName: A, folders: [{type: api, label: L, pathTemplate: 'D:\\{{envvar:JBX_UNSET}}'}]}]", ConfigErrorCode.UnsetEnvironmentVariable)]
    [InlineData("applications: [{id: a, displayName: A, folders: [{type: api, label: L, pathTemplate: 'D:\\{{env'}]}]", ConfigErrorCode.MalformedToken)]
    public void Reports_each_rule_with_its_own_code(string yamlFragment, ConfigErrorCode expected)
    {
        var yaml = yamlFragment.StartsWith("version", StringComparison.Ordinal)
            ? yamlFragment + "\nenvironments: [{id: dev, displayName: Dev}]\n"
            : "version: 1\n" + (yamlFragment.StartsWith("environments", StringComparison.Ordinal) ? string.Empty : "environments: [{id: dev, displayName: Dev}]\n") + yamlFragment + "\n";

        var result = Load("config.yaml", Encoding.UTF8.GetBytes(yaml));

        Assert.Equal(expected, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Treats_unreachable_paths_as_valid_because_reachability_is_not_validation()
    {
        var yaml = "version: 1\nenvironments: [{id: dev, displayName: Dev}]\napplications: [{id: a, displayName: A, folders: [{type: api, label: L, pathTemplate: '\\\\192.0.2.1\\nowhere'}]}]\n";

        var result = Load("config.yaml", Encoding.UTF8.GetBytes(yaml));

        Assert.False(result.HasErrors);
    }

    private static ConfigLoadResult Load(string fileName, byte[] content)
    {
        var resolver = new PathTemplateResolver(new FakeEnvironmentVariables(), new FakeTimeProvider());
        var loader = new ConfigLoader(new ConfigValidator(resolver));
        return loader.Load(new ConfigSource(fileName, content));
    }
}
