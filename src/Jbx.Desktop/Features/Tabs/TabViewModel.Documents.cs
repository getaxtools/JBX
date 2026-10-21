using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Features.LogViewer;

namespace Jbx.Desktop.Features.Tabs;

/// <summary>Files opened from this environment, each in its own tab inside the environment tab.</summary>
internal sealed partial class TabViewModel
{
    public const int MaxOpenFiles = 20;

    private bool _isActive;

    public ObservableCollection<LogViewerViewModel> Documents { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBrowsing))]
    public partial LogViewerViewModel? SelectedDocument { get; set; }

    public bool IsBrowsing => SelectedDocument is null;

    public bool HasDocuments => Documents.Count > 0;

    public void OpenInViewer(LogTarget target)
    {
        SelectedDocument = AddDocument(target);
    }

    /// <summary>Adds a file tab without showing it, or returns the one already open for that path.</summary>
    public LogViewerViewModel AddDocument(LogTarget target)
    {
        var document = Documents.FirstOrDefault(open => open.Target.Path == target.Path);
        if (document is not null)
        {
            return document;
        }

        if (Documents.Count >= MaxOpenFiles)
        {
            CloseDocument(Documents[0]);
        }

        document = new LogViewerViewModel(_services, target);
        Documents.Add(document);
        return document;
    }

    /// <summary>Closes the file being viewed, if any. Lets Ctrl+W close a file before it closes the environment.</summary>
    public bool TryCloseSelectedDocument()
    {
        if (SelectedDocument is not { } document)
        {
            return false;
        }

        CloseDocument(document);
        return true;
    }

    public void Deactivate()
    {
        _isActive = false;
        if (SelectedDocument is { } document)
        {
            document.IsActive = false;
        }
    }

    [RelayCommand]
    private void SelectDocument(LogViewerViewModel? document)
    {
        if (document is not null && Documents.Contains(document))
        {
            SelectedDocument = document;
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private void ShowFolders()
    {
        SelectedDocument = null;
        ListFocusToken++;
    }

    [RelayCommand]
    private void CloseDocument(LogViewerViewModel? document)
    {
        document ??= SelectedDocument;
        if (document is null)
        {
            return;
        }

        var index = Documents.IndexOf(document);
        var wasSelected = ReferenceEquals(document, SelectedDocument);
        Documents.Remove(document);
        document.Dispose();
        if (wasSelected)
        {
            SelectedDocument = Documents.Count == 0 ? null : Documents[Math.Clamp(index - 1, 0, Documents.Count - 1)];
        }
    }

    partial void OnSelectedDocumentChanged(LogViewerViewModel? oldValue, LogViewerViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = _isActive;
        }
    }

    private void DisposeDocuments()
    {
        foreach (var document in Documents)
        {
            document.Dispose();
        }

        Documents.Clear();
    }
}
