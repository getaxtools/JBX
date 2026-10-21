using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Core.Config;
using Jbx.Desktop.Features.Dialogs;
using Jbx.Desktop.Features.Palette;
using Jbx.Desktop.Features.Tabs;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;

namespace Jbx.Desktop.Features.Shell;

/// <summary>The command palette, editing the config and reloading it when it changes on disk.</summary>
internal sealed partial class MainWindowViewModel : IDisposable
{
    public static readonly TimeSpan ConfigCheckInterval = TimeSpan.FromSeconds(2);

    private ITimer? _configWatch;
    private bool _isCheckingConfig;
    private IReadOnlyList<ConfigProblem> _pendingProblems = [];

    public CommandPaletteViewModel Palette { get; } = new();

    [ObservableProperty]
    public partial bool HasPendingProblems { get; set; }

    public string? ConfigPathText => _services.ConfigSession.Path?.Display;

    public void Dispose()
    {
        _configWatch?.Dispose();
        _configWatch = null;
    }

    [RelayCommand]
    private void OpenPalette()
    {
        Palette.Open(PaletteCatalog.For(this));
    }

    [RelayCommand]
    private void MoveTab(ReorderRequest? request)
    {
        if (request is null || request.From == request.To || !IsTabIndex(request.From) || !IsTabIndex(request.To))
        {
            return;
        }

        var selected = SelectedTab;
        Tabs.Move(request.From, request.To);
        SelectedTab = selected;
    }

    [RelayCommand]
    private void MoveTabLeft()
    {
        MoveSelectedTab(-1);
    }

    [RelayCommand]
    private void MoveTabRight()
    {
        MoveSelectedTab(1);
    }

    [RelayCommand]
    private void SelectTab(TabViewModel? tab)
    {
        if (tab is not null && Tabs.Contains(tab))
        {
            SelectedTab = tab;
        }
    }

    [RelayCommand(CanExecute = nameof(HasConfig))]
    private async Task EditConfigAsync()
    {
        if (_services.ConfigSession.Path is not { } path)
        {
            return;
        }

        var opened = await _services.Shell.OpenFileAsync(path).ConfigureAwait(true);
        StatusMessage = opened ? Strings.Shell_EditingConfig : Strings.FormatShell_EditConfigFailed(_services.Paths.FileName(path));
    }

    [RelayCommand]
    private async Task ShowPendingProblemsAsync()
    {
        if (_pendingProblems.Count == 0)
        {
            return;
        }

        var fileName = ConfigFileName ?? string.Empty;
        await _services.Interaction.ShowConfigProblemsAsync(new ConfigProblemsViewModel(_services.Interaction, fileName, _pendingProblems, keptPrevious: true)).ConfigureAwait(true);
    }

    private void MoveSelectedTab(int offset)
    {
        if (SelectedTab is { } tab)
        {
            var from = Tabs.IndexOf(tab);
            MoveTab(new ReorderRequest(from, from + offset));
        }
    }

    private bool IsTabIndex(int index)
    {
        return index >= 0 && index < Tabs.Count;
    }

    private bool HasConfig()
    {
        return _services.ConfigSession.Path is not null;
    }

    private void WatchConfig()
    {
        EditConfigCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ConfigPathText));
        _configWatch ??= _services.Time.CreateTimer(_ => _services.Dispatcher.Post(() => _ = CheckConfigAsync()), null, ConfigCheckInterval, ConfigCheckInterval);
    }

    private async Task CheckConfigAsync()
    {
        if (_isCheckingConfig)
        {
            return;
        }

        _isCheckingConfig = true;
        try
        {
            if (await _services.ConfigSession.HasChangedOnDiskAsync(CancellationToken.None).ConfigureAwait(true))
            {
                await ReloadChangedConfigAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            _isCheckingConfig = false;
        }
    }

    /// <summary>A save by the config's owner: reload quietly, and if the save is broken say so without a dialog.</summary>
    private async Task ReloadChangedConfigAsync()
    {
        var outcome = await _services.ConfigSession.ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        if (outcome.Succeeded && _services.ConfigSession.Document is { } document)
        {
            SetPendingProblems([]);
            ShowDocument(document);
            StatusMessage = Strings.Shell_ConfigChangedReloaded;
            return;
        }

        SetPendingProblems(outcome.Load?.Errors ?? []);
        StatusMessage = HasPendingProblems ? Strings.Shell_ConfigChangedInvalid : ConfigOpenText.Describe(outcome);
    }

    private void SetPendingProblems(IReadOnlyList<ConfigProblem> problems)
    {
        _pendingProblems = problems;
        HasPendingProblems = problems.Count > 0;
    }
}
