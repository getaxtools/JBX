using System.Collections.Immutable;
using Jbx.Core.Config;
using Jbx.Core.Templating;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Core.Tests.Templating;

public sealed class PathTemplateResolverTests
{
    private static readonly ImmutableDictionary<string, string> Globals = new Dictionary<string, string>
    {
        ["shareRoot"] = @"\\fs01.contoso.test\AppLogs",
        ["selfReference"] = "{{selfReference}}",
        ["nested"] = "{{shareRoot}}",
    }.ToImmutableDictionary();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 23, 30, 0, TimeSpan.Zero));

    [Fact]
    public void Substitutes_environment_application_globals_and_environment_variables()
    {
        var resolver = Resolver(new Dictionary<string, string> { ["USERNAME"] = "asha.rao" });

        var resolution = resolver.Resolve(new PathTemplate(@"{{shareRoot}}\{{env}}\{{app}}\{{envvar:USERNAME}}"), Scope());

        Assert.Equal(@"\\fs01.contoso.test\AppLogs\prod\billing-portal\asha.rao", resolution.Path);
    }

    [Fact]
    public void Leaves_a_self_referencing_global_as_literal_text_instead_of_looping()
    {
        var resolution = Resolver().Resolve(new PathTemplate(@"D:\{{selfReference}}"), Scope());

        Assert.Equal(@"D:\{{selfReference}}", resolution.Path);
    }

    [Fact]
    public void Never_rescans_a_substituted_value()
    {
        var resolution = Resolver().Resolve(new PathTemplate("{{nested}}"), Scope());

        Assert.Equal("{{shareRoot}}", resolution.Path);
    }

    [Fact]
    public void Formats_the_date_when_the_path_is_resolved_not_when_the_config_was_loaded()
    {
        var resolver = Resolver();
        var template = new PathTemplate(@"D:\logs\{{date:yyyy-MM-dd}}");
        var before = resolver.Resolve(template, Scope()).Path;

        _time.Advance(TimeSpan.FromHours(1));
        var after = resolver.Resolve(template, Scope()).Path;

        Assert.NotEqual(before, after);
        Assert.EndsWith(_time.GetLocalNow().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), after, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{{envvar:JBX_UNSET}}", ConfigErrorCode.UnsetEnvironmentVariable, "JBX_UNSET")]
    [InlineData("{{shareRot}}", ConfigErrorCode.UnknownToken, "shareRot")]
    [InlineData("{{date:}}", ConfigErrorCode.InvalidDateFormat, "")]
    [InlineData("{{date:yyyy'}}", ConfigErrorCode.InvalidDateFormat, "yyyy'")]
    public void Refuses_to_produce_a_path_when_a_token_cannot_be_resolved(string template, ConfigErrorCode expected, string subject)
    {
        var resolution = Resolver().Resolve(new PathTemplate(template), Scope());

        Assert.False(resolution.IsResolved);
        var problem = Assert.Single(resolution.Problems);
        Assert.Equal(expected, problem.Code);
        Assert.Equal(subject, problem.Subject);
    }

    private PathTemplateResolver Resolver(Dictionary<string, string>? variables = null)
    {
        return new PathTemplateResolver(new FakeEnvironmentVariables(variables ?? []), _time);
    }

    private static TemplateScope Scope()
    {
        return new TemplateScope("prod", "billing-portal", Globals);
    }
}
