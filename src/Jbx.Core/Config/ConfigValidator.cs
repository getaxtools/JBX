using System.Text.RegularExpressions;
using Jbx.Core.Templating;

namespace Jbx.Core.Config;

/// <summary>Checks a parsed document and reports every problem at once; never stops at the first.</summary>
public sealed partial class ConfigValidator(PathTemplateResolver resolver)
{
    public const int SupportedVersion = 1;

    private static readonly string[] Classifications = ["sox", "pci", "phi", "internal", "none"];

    public IReadOnlyList<ConfigProblem> Validate(ConfigDocument document)
    {
        var problems = new List<ConfigProblem>();
        if (document.Version != SupportedVersion)
        {
            problems.Add(new ConfigProblem(ConfigErrorCode.UnsupportedVersion, document.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), null));
        }

        ValidateGlobals(document, problems);
        ValidateEnvironments(document, problems);
        ValidateApplications(document, problems);
        return problems;
    }

    private static void ValidateGlobals(ConfigDocument document, List<ConfigProblem> problems)
    {
        foreach (var name in document.Globals.Keys)
        {
            if (!GlobalNamePattern().IsMatch(name) || PathTemplateResolver.ReservedTokens.Contains(name))
            {
                problems.Add(new ConfigProblem(ConfigErrorCode.InvalidGlobalName, name, null));
            }
        }
    }

    private static void ValidateEnvironments(ConfigDocument document, List<ConfigProblem> problems)
    {
        if (document.Environments.IsEmpty)
        {
            problems.Add(new ConfigProblem(ConfigErrorCode.NoEnvironments, null, null));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var environment in document.Environments.Where(environment => environment.Id.Length > 0))
        {
            if (!IdPattern().IsMatch(environment.Id))
            {
                problems.Add(new ConfigProblem(ConfigErrorCode.InvalidId, environment.Id, environment.Location) { EnvironmentId = environment.Id });
            }
            else if (!seen.Add(environment.Id))
            {
                problems.Add(new ConfigProblem(ConfigErrorCode.DuplicateEnvironmentId, environment.Id, environment.Location) { EnvironmentId = environment.Id });
            }
        }
    }

    private void ValidateApplications(ConfigDocument document, List<ConfigProblem> problems)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var application in document.Applications)
        {
            if (application.Id.Length > 0 && !IdPattern().IsMatch(application.Id))
            {
                problems.Add(ForApplication(ConfigErrorCode.InvalidId, application.Id, application));
            }
            else if (application.Id.Length > 0 && !seen.Add(application.Id))
            {
                problems.Add(ForApplication(ConfigErrorCode.DuplicateApplicationId, application.Id, application));
            }

            if (application.Classification is { } classification && !Classifications.Contains(classification))
            {
                problems.Add(ForApplication(ConfigErrorCode.InvalidClassification, classification, application));
            }

            ValidateFolders(document, application, problems);
        }
    }

    private void ValidateFolders(ConfigDocument document, ApplicationDefinition application, List<ConfigProblem> problems)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in application.Folders)
        {
            if (folder.Label.Length > 0 && !labels.Add(folder.Label))
            {
                problems.Add(ForFolder(ConfigErrorCode.DuplicateFolderLabel, folder.Label, application, folder));
            }

            ValidateFolderFields(application, folder, problems);
            ValidateTemplate(document, application, folder, problems);
        }
    }

    private static void ValidateFolderFields(ApplicationDefinition application, FolderVariation folder, List<ConfigProblem> problems)
    {
        if (folder.Type.Key.Length > 0 && !folder.Type.IsBuiltIn && !CustomTypePattern().IsMatch(folder.Type.Key))
        {
            problems.Add(ForFolder(ConfigErrorCode.InvalidFolderType, folder.Type.Key, application, folder));
        }

        if (folder.RetentionDays < 0)
        {
            problems.Add(ForFolder(ConfigErrorCode.NegativeRetentionDays, null, application, folder));
        }

        if (folder.LogFilePattern is { } pattern && !IsValidFilePattern(pattern))
        {
            problems.Add(ForFolder(ConfigErrorCode.InvalidLogFilePattern, pattern, application, folder));
        }

        if (folder.ExternalLogUrl is { } url && !IsHttpsUrl(url))
        {
            problems.Add(ForFolder(ConfigErrorCode.InvalidExternalLogUrl, url, application, folder));
        }
    }

    private void ValidateTemplate(ConfigDocument document, ApplicationDefinition application, FolderVariation folder, List<ConfigProblem> problems)
    {
        var reported = new HashSet<(ConfigErrorCode, string?)>();
        foreach (var environment in document.Environments)
        {
            var scope = new TemplateScope(environment.Id, application.Id, document.Globals);
            foreach (var problem in resolver.Resolve(folder.PathTemplate, scope).Problems)
            {
                if (reported.Add((problem.Code, problem.Subject)))
                {
                    problems.Add(ForFolder(problem.Code, problem.Subject, application, folder));
                }
            }
        }
    }

    private static bool IsValidFilePattern(string pattern)
    {
        var invalid = Path.GetInvalidFileNameChars().Where(character => character is not ('*' or '?'));
        return pattern.Length > 0 && pattern.IndexOfAny([.. invalid, '\\', '/']) < 0;
    }

    private static bool IsHttpsUrl(string text)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static ConfigProblem ForApplication(ConfigErrorCode code, string? subject, ApplicationDefinition application)
    {
        return new ConfigProblem(code, subject, application.Location) { ApplicationId = application.Id };
    }

    private static ConfigProblem ForFolder(ConfigErrorCode code, string? subject, ApplicationDefinition application, FolderVariation folder)
    {
        return new ConfigProblem(code, subject, folder.Location) { ApplicationId = application.Id, FolderLabel = folder.Label };
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    [GeneratedRegex("^custom:[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CustomTypePattern();

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex GlobalNamePattern();
}
