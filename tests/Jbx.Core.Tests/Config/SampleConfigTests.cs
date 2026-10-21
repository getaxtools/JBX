using Jbx.Core.Config;
using Jbx.Core.Templating;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Core.Tests.Config;

public sealed class SampleConfigTests
{
    [Fact]
    public void The_shipped_sample_config_loads_without_errors_or_warnings()
    {
        var content = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "jbx-sample.yaml"));
        var variables = new FakeEnvironmentVariables(new Dictionary<string, string> { ["USERPROFILE"] = @"C:\Users\asha.rao" });
        var loader = new ConfigLoader(new ConfigValidator(new PathTemplateResolver(variables, new FakeTimeProvider())));

        var result = loader.Load(new ConfigSource("jbx-sample.yaml", content));

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Equal(4, result.Document?.Applications.Length);
    }
}
