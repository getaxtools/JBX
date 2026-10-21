using Jbx.Core.Config;
using Jbx.Core.Templating;
using Jbx.Platform;

namespace Jbx.Services.Rows;

/// <summary>Every enabled folder of a config resolved for one environment. Pure: touches no file system.</summary>
public sealed class EnvironmentRows
{
    private EnvironmentRows(ConfigDocument document, DeploymentEnvironment environment, IReadOnlyList<ApplicationEntry> applications)
    {
        Document = document;
        Environment = environment;
        Applications = applications;
        Folders = applications.SelectMany(application => application.Folders).ToList();
    }

    public ConfigDocument Document { get; }

    public DeploymentEnvironment Environment { get; }

    public IReadOnlyList<ApplicationEntry> Applications { get; }

    public IReadOnlyList<FolderEntry> Folders { get; }

    public static EnvironmentRows Resolve(ConfigDocument document, DeploymentEnvironment environment, PathTemplateResolver resolver, IPathCanonicalizer paths)
    {
        var applications = document.Applications
            .Select(application => new ApplicationEntry(application, ResolveFolders(document, environment, application, resolver, paths)))
            .ToList();
        return new EnvironmentRows(document, environment, applications);
    }

    private static List<FolderEntry> ResolveFolders(
        ConfigDocument document,
        DeploymentEnvironment environment,
        ApplicationDefinition application,
        PathTemplateResolver resolver,
        IPathCanonicalizer paths)
    {
        var scope = new TemplateScope(environment.Id, application.Id, document.Globals);
        return application.Folders
            .Where(folder => folder.IsEnabled)
            .Select(folder =>
            {
                var resolution = resolver.Resolve(folder.PathTemplate, scope);
                var path = resolution.Path is null ? null : paths.TryCanonicalize(resolution.Path);
                return new FolderEntry(application, folder, path);
            })
            .ToList();
    }
}
