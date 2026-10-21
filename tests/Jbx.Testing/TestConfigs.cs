using System.Text;
using Jbx.Core.Config;
using Jbx.Core.Templating;
using Microsoft.Extensions.Time.Testing;

namespace Jbx.Testing;

public static class TestConfigs
{
    public const string ShareRoot = @"\\fs01.contoso.test\AppLogs";

    public static PathTemplateResolver Resolver(TimeProvider? timeProvider = null)
    {
        return new PathTemplateResolver(new FakeEnvironmentVariables(), timeProvider ?? new FakeTimeProvider());
    }

    public static ConfigLoader Loader(TimeProvider? timeProvider = null)
    {
        return new ConfigLoader(new ConfigValidator(Resolver(timeProvider)));
    }

    public static ConfigDocument FullExample()
    {
        return FromYaml(Encoding.UTF8.GetString(Fixture.ReadBytes("config/valid-full.yaml")));
    }

    public static ConfigDocument FromYaml(string yaml)
    {
        var result = Loader().Load(new ConfigSource("test.yaml", Encoding.UTF8.GetBytes(yaml)));
        if (result.HasErrors)
        {
            throw new InvalidOperationException("Test config is invalid: " + string.Join("; ", result.Errors.Select(error => $"{error.Code} {error.Subject}")));
        }

        return result.Document!;
    }

    public static ConfigDocument Synthetic(int applications, int foldersPerApplication)
    {
        var yaml = new StringBuilder("version: 1\nglobals:\n  shareRoot: \"\\\\\\\\fs01.contoso.test\\\\AppLogs\"\nenvironments:\n  - id: prod\n    displayName: Production\n  - id: uat\n    displayName: UAT\napplications:\n");
        for (var application = 0; application < applications; application++)
        {
            yaml.Append("  - id: app-").Append(application).Append("\n    displayName: Application ").Append(application)
                .Append("\n    owner: Team ").Append(application % 7).Append("\n    tags: [tier").Append(application % 3).Append("]\n    folders:\n");
            for (var folder = 0; folder < foldersPerApplication; folder++)
            {
                yaml.Append("      - type: ").Append(folder % 2 == 0 ? "api" : "web").Append("\n        label: Folder ").Append(folder)
                    .Append("\n        pathTemplate: \"{{shareRoot}}\\\\{{env}}\\\\{{app}}\\\\f").Append(folder).Append("\"\n");
            }
        }

        return FromYaml(yaml.ToString());
    }
}
