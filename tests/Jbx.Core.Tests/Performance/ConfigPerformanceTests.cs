using System.Diagnostics;
using System.Text;
using Jbx.Core.Config;
using Jbx.Core.Templating;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Core.Tests.Performance;

public sealed class ConfigPerformanceTests
{
    private const int Applications = 100;
    private const int FoldersPerApplication = 10;

    [Fact]
    public void Parses_and_validates_a_100_application_config_within_500_ms()
    {
        var content = Encoding.UTF8.GetBytes(SyntheticConfig());
        var loader = new ConfigLoader(new ConfigValidator(Resolver()));
        loader.Load(new ConfigSource("warmup.yaml", content));

        var stopwatch = Stopwatch.StartNew();
        var result = loader.Load(new ConfigSource("large.yaml", content));
        stopwatch.Stop();

        Assert.False(result.HasErrors);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Resolves_1000_folder_paths_within_50_ms()
    {
        var document = ConfigParser.Parse(new ConfigSource("large.yaml", Encoding.UTF8.GetBytes(SyntheticConfig()))).Document!;
        var resolver = Resolver();
        var folders = document.Applications.SelectMany(application => application.Folders.Select(folder => (application, folder))).ToList();
        ResolveAll(resolver, document, folders);

        var stopwatch = Stopwatch.StartNew();
        var resolved = ResolveAll(resolver, document, folders);
        stopwatch.Stop();

        Assert.Equal(Applications * FoldersPerApplication, resolved);
        Assert.True(stopwatch.ElapsedMilliseconds < 50, $"took {stopwatch.ElapsedMilliseconds} ms");
    }

    private static int ResolveAll(PathTemplateResolver resolver, ConfigDocument document, List<(ApplicationDefinition Application, FolderVariation Folder)> folders)
    {
        var resolved = 0;
        foreach (var (application, folder) in folders)
        {
            var scope = new TemplateScope("prod", application.Id, document.Globals);
            resolved += resolver.Resolve(folder.PathTemplate, scope).IsResolved ? 1 : 0;
        }

        return resolved;
    }

    private static PathTemplateResolver Resolver()
    {
        return new PathTemplateResolver(new FakeEnvironmentVariables(), new FakeTimeProvider());
    }

    private static string SyntheticConfig()
    {
        var yaml = new StringBuilder("version: 1\nglobals:\n  shareRoot: \"\\\\\\\\fs01.contoso.test\\\\AppLogs\"\nenvironments:\n");
        foreach (var environment in new[] { "dev", "qa", "uat", "prod", "dr" })
        {
            yaml.Append("  - id: ").Append(environment).Append("\n    displayName: ").Append(environment).Append('\n');
        }

        yaml.Append("applications:\n");
        for (var app = 0; app < Applications; app++)
        {
            yaml.Append("  - id: app-").Append(app).Append("\n    displayName: Application ").Append(app).Append("\n    tags: [synthetic]\n    folders:\n");
            for (var folder = 0; folder < FoldersPerApplication; folder++)
            {
                yaml.Append("      - type: api\n        label: Folder ").Append(folder)
                    .Append("\n        pathTemplate: \"{{shareRoot}}\\\\{{env}}\\\\{{app}}\\\\f").Append(folder).Append("\\\\{{date:yyyy-MM-dd}}\"\n");
            }
        }

        return yaml.ToString();
    }
}
