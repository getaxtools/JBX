using System.Globalization;
using System.Text.RegularExpressions;
using Jbx.Core.Config;

namespace Jbx.Core.Templating;

public sealed partial class PathTemplateResolver(IEnvironmentVariables environmentVariables, TimeProvider timeProvider)
{
    private const string EnvironmentToken = "env";
    private const string ApplicationToken = "app";
    private const string DatePrefix = "date:";
    private const string EnvironmentVariablePrefix = "envvar:";

    public static IReadOnlyList<string> ReservedTokens { get; } = [EnvironmentToken, ApplicationToken];

    public TemplateResolution Resolve(PathTemplate template, TemplateScope scope)
    {
        var problems = new List<ConfigProblem>();
        if (HasUnbalancedBraces(template.Text))
        {
            problems.Add(new ConfigProblem(ConfigErrorCode.MalformedToken, template.Text, null));
            return new TemplateResolution(null, problems);
        }

        var now = timeProvider.GetLocalNow();
        var path = TokenPattern().Replace(template.Text, match => ResolveToken(match.Groups["body"].Value.Trim(), scope, now, problems));
        return problems.Count == 0 ? new TemplateResolution(path, problems) : new TemplateResolution(null, problems);
    }

    private static bool HasUnbalancedBraces(string text)
    {
        var withoutTokens = TokenPattern().Replace(text, string.Empty);
        return withoutTokens.Contains("{{", StringComparison.Ordinal) || withoutTokens.Contains("}}", StringComparison.Ordinal);
    }

    private string ResolveToken(string token, TemplateScope scope, DateTimeOffset now, List<ConfigProblem> problems)
    {
        if (token == EnvironmentToken)
        {
            return scope.EnvironmentId;
        }

        if (token == ApplicationToken)
        {
            return scope.ApplicationId;
        }

        if (token.StartsWith(DatePrefix, StringComparison.Ordinal))
        {
            return FormatDate(token[DatePrefix.Length..], now, problems);
        }

        if (token.StartsWith(EnvironmentVariablePrefix, StringComparison.Ordinal))
        {
            return ReadEnvironmentVariable(token[EnvironmentVariablePrefix.Length..], problems);
        }

        if (scope.Globals.TryGetValue(token, out var global))
        {
            return global;
        }

        problems.Add(new ConfigProblem(ConfigErrorCode.UnknownToken, token, null));
        return string.Empty;
    }

    private static string FormatDate(string format, DateTimeOffset now, List<ConfigProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            problems.Add(new ConfigProblem(ConfigErrorCode.InvalidDateFormat, format, null));
            return string.Empty;
        }

        try
        {
            return now.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            problems.Add(new ConfigProblem(ConfigErrorCode.InvalidDateFormat, format, null));
            return string.Empty;
        }
    }

    private string ReadEnvironmentVariable(string name, List<ConfigProblem> problems)
    {
        var value = string.IsNullOrWhiteSpace(name) ? null : environmentVariables.Read(name);
        if (value is null)
        {
            problems.Add(new ConfigProblem(ConfigErrorCode.UnsetEnvironmentVariable, name, null));
            return string.Empty;
        }

        return value;
    }

    [GeneratedRegex(@"\{\{(?<body>[^{}]*)\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenPattern();
}
