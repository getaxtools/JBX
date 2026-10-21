using Jbx.Core.Config;
using Jbx.Platform;

namespace Jbx.Services.Settings;

/// <summary>
/// Which saved tabs to reopen: only for the same config, only environments it still has, only files
/// whose paths are still valid paths. Files are not checked here; the viewer reports a missing one.
/// </summary>
public static class SessionRestore
{
    public static SessionPlan Plan(SavedSession? session, CanonicalPath config, ConfigDocument document, IPathCanonicalizer paths)
    {
        if (session?.ConfigPath is not { } savedConfig || paths.TryCanonicalize(savedConfig) != config)
        {
            return SessionPlan.Nothing;
        }

        var tabs = new List<RestoredTab>();
        var selected = 0;
        foreach (var (saved, index) in session.Tabs.Take(SavedSession.MaxTabs).Select((tab, index) => (tab, index)))
        {
            if (document.FindEnvironment(saved.EnvironmentId) is not { } environment)
            {
                continue;
            }

            if (index == session.SelectedTab)
            {
                selected = tabs.Count;
            }

            tabs.Add(Restore(saved, environment, paths));
        }

        return new SessionPlan(tabs, selected);
    }

    private static RestoredTab Restore(SavedTab saved, DeploymentEnvironment environment, IPathCanonicalizer paths)
    {
        var files = new List<RestoredFile>();
        var selectedFile = -1;
        foreach (var (file, index) in saved.Files.Select((file, index) => (file, index)))
        {
            if (paths.TryCanonicalize(file.Path) is not { } path)
            {
                continue;
            }

            if (index == saved.SelectedFile)
            {
                selectedFile = files.Count;
            }

            files.Add(new RestoredFile(path, file.Title, file.Subtitle));
        }

        return new RestoredTab(environment, files, selectedFile);
    }
}
