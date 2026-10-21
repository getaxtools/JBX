using Jbx.Core.Config;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Text;

/// <summary>Turns a config problem into one sentence that names the problem and where it is.</summary>
internal static class ConfigProblemText
{
    public static string Describe(ConfigProblem problem)
    {
        var subject = problem.Subject ?? string.Empty;
        var message = problem.Code switch
        {
            ConfigErrorCode.ConfigTooLarge => Strings.ConfigError_ConfigTooLarge,
            ConfigErrorCode.EmptyConfig => Strings.ConfigError_EmptyConfig,
            ConfigErrorCode.YamlSyntax => Strings.FormatConfigError_YamlSyntax(subject),
            ConfigErrorCode.TabIndentation => Strings.ConfigError_TabIndentation,
            ConfigErrorCode.JsonSyntax => Strings.ConfigError_JsonSyntax,
            ConfigErrorCode.UnsupportedTag => Strings.FormatConfigError_UnsupportedTag(subject),
            ConfigErrorCode.TooManyAliases or ConfigErrorCode.TooComplex => Strings.ConfigError_TooComplex,
            ConfigErrorCode.MultipleDocuments => Strings.ConfigError_MultipleDocuments,
            ConfigErrorCode.ExpectedMapping => Strings.FormatConfigError_ExpectedMapping(subject),
            ConfigErrorCode.ExpectedSequence => Strings.FormatConfigError_ExpectedSequence(subject),
            ConfigErrorCode.ExpectedText => Strings.FormatConfigError_ExpectedText(subject),
            ConfigErrorCode.MissingField => Strings.FormatConfigError_MissingField(subject),
            ConfigErrorCode.DuplicateKey => Strings.FormatConfigError_DuplicateKey(subject),
            ConfigErrorCode.InvalidBoolean => Strings.FormatConfigError_InvalidBoolean(subject),
            ConfigErrorCode.InvalidNumber => Strings.FormatConfigError_InvalidNumber(subject),
            ConfigErrorCode.UnknownKey => Strings.FormatConfigError_UnknownKey(subject),
            ConfigErrorCode.UnsupportedVersion => Strings.FormatConfigError_UnsupportedVersion(subject),
            ConfigErrorCode.NoEnvironments => Strings.ConfigError_NoEnvironments,
            ConfigErrorCode.InvalidId => Strings.FormatConfigError_InvalidId(subject),
            ConfigErrorCode.DuplicateEnvironmentId => Strings.FormatConfigError_DuplicateEnvironmentId(subject),
            ConfigErrorCode.DuplicateApplicationId => Strings.FormatConfigError_DuplicateApplicationId(subject),
            ConfigErrorCode.DuplicateFolderLabel => Strings.FormatConfigError_DuplicateFolderLabel(subject),
            ConfigErrorCode.InvalidFolderType => Strings.FormatConfigError_InvalidFolderType(subject),
            ConfigErrorCode.InvalidGlobalName => Strings.FormatConfigError_InvalidGlobalName(subject),
            ConfigErrorCode.NegativeRetentionDays => Strings.ConfigError_NegativeRetentionDays,
            ConfigErrorCode.InvalidLogFilePattern => Strings.FormatConfigError_InvalidLogFilePattern(subject),
            ConfigErrorCode.InvalidExternalLogUrl => Strings.FormatConfigError_InvalidExternalLogUrl(subject),
            ConfigErrorCode.InvalidClassification => Strings.FormatConfigError_InvalidClassification(subject),
            ConfigErrorCode.MalformedToken => Strings.ConfigError_MalformedToken,
            ConfigErrorCode.UnknownToken => Strings.FormatConfigError_UnknownToken(subject),
            ConfigErrorCode.UnsetEnvironmentVariable => Strings.FormatConfigError_UnsetEnvironmentVariable(subject),
            ConfigErrorCode.InvalidDateFormat => Strings.FormatConfigError_InvalidDateFormat(subject),
            _ => problem.Code.ToString(),
        };
        return message;
    }

    public static string Where(ConfigProblem problem)
    {
        var parts = new List<string>();
        if (problem.Location is { } location)
        {
            parts.Add(Strings.FormatConfigError_Line(location.Line, location.Column));
        }

        if (problem.ApplicationId is { Length: > 0 } applicationId)
        {
            parts.Add(problem.FolderLabel is { Length: > 0 } label
                ? Strings.FormatConfigError_InFolder(applicationId, label)
                : Strings.FormatConfigError_InApplication(applicationId));
        }

        return string.Join(" · ", parts);
    }
}
