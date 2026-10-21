using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Core.Config;
using Jbx.Desktop.Features.Details;
using Jbx.Desktop.Features.Dialogs;
using Jbx.Desktop.Features.Favourites;
using Jbx.Desktop.Features.LogViewer;
using Jbx.Desktop.Features.Palette;
using Jbx.Desktop.Features.Tabs;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Services.ConfigSession;
using Jbx.Services.Rows;
using Jbx.Services.Settings;

namespace Jbx.Desktop.Features.Shell;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ShellServices _services;
    private FocusedPane _focusedPane = FocusedPane.List;

    public MainWindowViewModel(ShellServices services)
    {
        _services = services;
        Favourites = new FavouritesPaneViewModel(services, OpenFavourite);
        Tabs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTabs));
            SaveSession();
        };
        Palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.IsOpen) && !Palette.IsOpen)
            {
                FocusPane(_focusedPane);
            }
        };
    }

    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    public FavouritesPaneViewModel Favourites { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    public partial TabViewModel? SelectedTab { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStarting), nameof(IsNoConfig), nameof(IsConfigUnavailable), nameof(IsReadyWithoutTabs), nameof(IsReadyWithTabs))]
    public partial ShellState State { get; set; } = ShellState.Starting;

    [ObservableProperty]
    public partial string? ConfigProblemMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsFavouritesPaneVisible { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecentConfigs))]
    public partial IReadOnlyList<string> RecentConfigs { get; set; } = [];

    [ObservableProperty]
    public partial string? ConfigFileName { get; set; }

    [ObservableProperty]
    public partial int EmptyStateFocusToken { get; set; }

    public bool HasTabs => Tabs.Count > 0;

    public bool HasRecentConfigs => RecentConfigs.Count > 0;

    public bool IsStarting => State == ShellState.Starting;

    public bool IsNoConfig => State == ShellState.NoConfig;

    public bool IsConfigUnavailable => State == ShellState.ConfigUnavailable;

    public bool IsReadyWithoutTabs => State == ShellState.Ready && !HasTabs;

    public bool IsReadyWithTabs => State == ShellState.Ready && HasTabs;

    public string WindowTitle => SelectedTab is null
        ? Strings.Shell_Window_Title
        : SelectedTab.IsProduction && !SelectedTab.Header.Contains("prod", StringComparison.OrdinalIgnoreCase)
            ? Strings.FormatShell_Window_TitleProduction(SelectedTab.Header)
            : Strings.FormatShell_Window_TitleWithEnvironment(SelectedTab.Header);

    public async Task InitializeAsync()
    {
        var settings = await _services.Settings.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        if (settings.Status == SettingsLoadStatus.SetAside)
        {
            StatusMessage = Strings.FormatShell_SettingsSetAside(settings.SetAsideFileName ?? string.Empty);
        }

        IsFavouritesPaneVisible = _services.Settings.Current.Window.IsFavouritesPaneVisible;
        RecentConfigs = _services.Settings.Current.RecentConfigPaths;
        Favourites.ShowConfig(null);
        var outcome = await _services.ConfigSession.OpenRememberedAsync(CancellationToken.None).ConfigureAwait(true);
        await HandleOpenOutcomeAsync(outcome).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenConfigAsync()
    {
        var path = await _services.Interaction.PickConfigFileAsync().ConfigureAwait(true);
        if (path is not null)
        {
            await OpenConfigPathAsync(path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private Task OpenRecentConfigAsync(string? path)
    {
        return path is null ? Task.CompletedTask : OpenConfigPathAsync(path);
    }

    [RelayCommand]
    private async Task ReloadConfigAsync()
    {
        var outcome = await _services.ConfigSession.ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        await HandleOpenOutcomeAsync(outcome).ConfigureAwait(true);
        if (outcome.Succeeded)
        {
            SetPendingProblems([]);
            StatusMessage = Strings.Shell_ConfigReloaded;
        }
    }

    [RelayCommand]
    private async Task RetryConfigAsync()
    {
        State = ShellState.Starting;
        var outcome = await _services.ConfigSession.OpenRememberedAsync(CancellationToken.None).ConfigureAwait(true);
        await HandleOpenOutcomeAsync(outcome).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NewTabAsync()
    {
        var document = _services.ConfigSession.Document;
        if (document is null)
        {
            await OpenConfigAsync().ConfigureAwait(true);
            return;
        }

        var environment = await _services.Interaction.PickEnvironmentAsync(document.Environments, _services.Settings.Current.LastEnvironmentId).ConfigureAwait(true);
        if (environment is not null)
        {
            AddTab(document, environment);
        }
    }

    [RelayCommand]
    private void CloseTab(TabViewModel? tab)
    {
        // From the keyboard there is no tab argument: close the open file first, then the environment.
        if (tab is null && SelectedTab?.TryCloseSelectedDocument() == true)
        {
            return;
        }

        tab ??= SelectedTab;
        if (tab is null)
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        tab.Dispose();
        SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        OnPropertyChanged(nameof(IsReadyWithoutTabs));
        OnPropertyChanged(nameof(IsReadyWithTabs));
        if (SelectedTab is null)
        {
            EmptyStateFocusToken++;
        }
    }

    [RelayCommand]
    private void NextTab()
    {
        MoveTabSelection(1);
    }

    [RelayCommand]
    private void PreviousTab()
    {
        MoveTabSelection(-1);
    }

    [RelayCommand]
    private void SelectTabAt(int number)
    {
        if (Tabs.Count > 0)
        {
            SelectedTab = number >= 9 ? Tabs[^1] : Tabs.ElementAtOrDefault(number - 1) ?? SelectedTab;
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return SelectedTab?.RefreshAsync() ?? Task.CompletedTask;
    }

    [RelayCommand]
    private void FocusSearch()
    {
        SelectedTab?.RequestSearchFocus();
    }

    [RelayCommand]
    private void ToggleFavouritesPane()
    {
        IsFavouritesPaneVisible = !IsFavouritesPaneVisible;
        if (!IsFavouritesPaneVisible && _focusedPane == FocusedPane.Favourites)
        {
            FocusPane(FocusedPane.List);
        }
    }

    [RelayCommand]
    private Task ShowAboutAsync()
    {
        return _services.Interaction.ShowAboutAsync();
    }

    [RelayCommand]
    private void NextPane()
    {
        FocusPane(StepPane(1));
    }

    [RelayCommand]
    private void PreviousPane()
    {
        FocusPane(StepPane(-1));
    }

    partial void OnSelectedTabChanged(TabViewModel? oldValue, TabViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnSelectedTabPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnSelectedTabPropertyChanged;
        }

        oldValue?.Deactivate();
        newValue?.Activate();
        SaveSession();
        OnPropertyChanged(nameof(IsReadyWithoutTabs));
        OnPropertyChanged(nameof(IsReadyWithTabs));
    }

    private void OnSelectedTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.Header))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    partial void OnIsFavouritesPaneVisibleChanged(bool value)
    {
        if (State != ShellState.Starting)
        {
            _ = _services.Settings.UpdateAsync(settings => settings with { Window = settings.Window with { IsFavouritesPaneVisible = value } }, CancellationToken.None);
        }
    }

    private async Task OpenConfigPathAsync(string path)
    {
        var outcome = await _services.ConfigSession.OpenAsync(path, CancellationToken.None).ConfigureAwait(true);
        await HandleOpenOutcomeAsync(outcome).ConfigureAwait(true);
    }

    private async Task HandleOpenOutcomeAsync(ConfigOpenOutcome outcome)
    {
        RecentConfigs = _services.Settings.Current.RecentConfigPaths;
        var document = _services.ConfigSession.Document;
        if (outcome.Succeeded && document is not null)
        {
            ShowDocument(document);
            return;
        }

        if (outcome.Status == ConfigOpenStatus.NothingRemembered)
        {
            State = document is null ? ShellState.NoConfig : ShellState.Ready;
            EmptyStateFocusToken++;
            return;
        }

        ConfigProblemMessage = ConfigOpenText.Describe(outcome);
        State = document is null ? ShellState.ConfigUnavailable : ShellState.Ready;
        StatusMessage = document is null ? null : ConfigProblemMessage;
        if (outcome.Load is { HasErrors: true } load && load.Errors.Count > 0)
        {
            var fileName = Path.GetFileName(outcome.PathText ?? string.Empty);
            await _services.Interaction.ShowConfigProblemsAsync(new ConfigProblemsViewModel(_services.Interaction, fileName, load.Errors, keptPrevious: document is not null)).ConfigureAwait(true);
        }

        EmptyStateFocusToken++;
    }

    private void ShowDocument(ConfigDocument document)
    {
        ConfigFileName = _services.ConfigSession.Path is { } path ? _services.Paths.FileName(path) : null;
        ConfigProblemMessage = null;
        WatchConfig();
        foreach (var tab in Tabs.ToList())
        {
            if (!tab.TryReload(document))
            {
                CloseTab(tab);
            }
        }

        State = ShellState.Ready;
        Favourites.ShowConfig(document);
        if (Tabs.Count == 0 && !TryRestoreSession(document))
        {
            var remembered = _services.Settings.Current.LastEnvironmentId;
            var environment = (remembered is null ? null : document.FindEnvironment(remembered)) ?? document.Environments.FirstOrDefault();
            if (environment is not null)
            {
                AddTab(document, environment);
            }
        }
    }

    private void AddTab(ConfigDocument document, DeploymentEnvironment environment)
    {
        var tab = CreateTab(document, environment);
        Tabs.Add(tab);
        SelectedTab = tab;
        _ = _services.Settings.UpdateAsync(settings => settings with { LastEnvironmentId = environment.Id }, CancellationToken.None);
        tab.RequestSearchFocus();
    }

    private void MoveTabSelection(int offset)
    {
        if (Tabs.Count == 0 || SelectedTab is null)
        {
            return;
        }

        var index = (Tabs.IndexOf(SelectedTab) + offset + Tabs.Count) % Tabs.Count;
        SelectedTab = Tabs[index];
    }

    private FocusedPane StepPane(int offset)
    {
        var panes = IsFavouritesPaneVisible
            ? new[] { FocusedPane.Favourites, FocusedPane.List, FocusedPane.Details }
            : [FocusedPane.List, FocusedPane.Details];
        var current = Array.IndexOf(panes, _focusedPane);
        return panes[(Math.Max(current, 0) + offset + panes.Length) % panes.Length];
    }

    private void FocusPane(FocusedPane pane)
    {
        _focusedPane = pane;
        switch (pane)
        {
            case FocusedPane.Favourites:
                Favourites.RequestFocus();
                break;
            case FocusedPane.List:
                SelectedTab?.FocusListCommand.Execute(null);
                break;
            case FocusedPane.Details:
                SelectedTab?.Details.RequestFocus();
                break;
        }
    }

    private void OpenFavourite(FavouriteItemViewModel item)
    {
        if (item.IsFile)
        {
            if (SelectedTab is { } tab)
            {
                tab.OpenInViewer(new LogTarget(item.Path, item.Label, Strings.Favourites_DetailsSubtitle));
            }
            else
            {
                _ = _services.Shell.OpenFileAsync(item.Path);
            }

            return;
        }

        if (SelectedTab is null)
        {
            _ = _services.Shell.OpenFolderAsync(item.Path);
            return;
        }

        var target = new DetailsTarget(item.Label, Strings.Favourites_DetailsSubtitle, item.Path) { FavouriteLabel = item.Label, Origin = item.Item.Favourite.Origin };
        SelectedTab.SelectedDocument = null;
        SelectedTab.SelectedRow = null;
        SelectedTab.Details.ShowFolder(target, new FolderStatus(item.Status, null));
        SelectedTab.Details.RequestFocus();
    }
}
