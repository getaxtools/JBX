using System.Collections.Specialized;
using System.ComponentModel;
using Jbx.Core.Config;
using Jbx.Desktop.Features.LogViewer;
using Jbx.Desktop.Features.Tabs;
using Jbx.Services.Settings;

namespace Jbx.Desktop.Features.Shell;

/// <summary>Remembers the open tabs and files, and reopens them for the same config on the next start.</summary>
internal sealed partial class MainWindowViewModel
{
    private bool _isRestoring;

    private bool TryRestoreSession(ConfigDocument document)
    {
        if (_services.ConfigSession.Path is not { } config)
        {
            return false;
        }

        var plan = SessionRestore.Plan(_services.Settings.Current.Session, config, document, _services.Paths);
        if (plan.IsEmpty)
        {
            return false;
        }

        _isRestoring = true;
        try
        {
            foreach (var saved in plan.Tabs)
            {
                var tab = CreateTab(document, saved.Environment);
                Tabs.Add(tab);
                var documents = saved.Files.Take(TabViewModel.MaxOpenFiles).Select(file => tab.AddDocument(new LogTarget(file.Path, file.Title, file.Subtitle))).ToList();
                tab.SelectedDocument = saved.SelectedFile >= 0 && saved.SelectedFile < documents.Count ? documents[saved.SelectedFile] : null;
            }

            SelectedTab = Tabs[plan.SelectedTab];
        }
        finally
        {
            _isRestoring = false;
        }

        SaveSession();
        return true;
    }

    private TabViewModel CreateTab(ConfigDocument document, DeploymentEnvironment environment)
    {
        var tab = new TabViewModel(_services, document, environment, closing => CloseTab(closing));
        tab.PropertyChanged += OnTabChangedForSession;
        tab.Documents.CollectionChanged += OnTabDocumentsChanged;
        return tab;
    }

    private void OnTabChangedForSession(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TabViewModel.Environment) or nameof(TabViewModel.SelectedDocument))
        {
            SaveSession();
        }
    }

    private void OnTabDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SaveSession();
    }

    private void SaveSession()
    {
        if (_isRestoring || State != ShellState.Ready || _services.ConfigSession.Path is not { } config)
        {
            return;
        }

        var session = new SavedSession
        {
            ConfigPath = config.Display,
            Tabs = [.. Tabs.Take(SavedSession.MaxTabs).Select(Saved)],
            SelectedTab = SelectedTab is null ? 0 : Tabs.IndexOf(SelectedTab),
        };
        _ = _services.Settings.UpdateAsync(settings => settings with { Session = session }, CancellationToken.None);
    }

    private static SavedTab Saved(TabViewModel tab)
    {
        return new SavedTab(tab.Environment.Id)
        {
            Files = [.. tab.Documents.Select(document => new SavedFile(document.Target.Path.Display, document.Title, document.Subtitle))],
            SelectedFile = tab.SelectedDocument is { } selected ? tab.Documents.IndexOf(selected) : -1,
        };
    }
}
