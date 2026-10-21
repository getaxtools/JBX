using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Services.Search;

namespace Jbx.Desktop.Features.Palette;

/// <summary>Every command in one searchable list, as in Windows Terminal and Visual Studio Code.</summary>
internal sealed partial class CommandPaletteViewModel : ObservableObject
{
    private IReadOnlyList<PaletteEntry> _entries = [];

    public ObservableCollection<PaletteEntry> Matches { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PaletteEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial bool HasNoMatches { get; set; }

    [ObservableProperty]
    public partial int FocusToken { get; set; }

    /// <summary>Shows the commands that can run right now.</summary>
    public void Open(IEnumerable<PaletteEntry> entries)
    {
        _entries = [.. entries.Where(entry => entry.Command.CanExecute(entry.Parameter))];
        FilterText = string.Empty;
        Refresh();
        IsOpen = true;
        FocusToken++;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
    }

    [RelayCommand]
    private void Run(PaletteEntry? entry)
    {
        entry ??= SelectedEntry;
        if (entry is null)
        {
            return;
        }

        IsOpen = false;
        if (entry.Command.CanExecute(entry.Parameter))
        {
            entry.Command.Execute(entry.Parameter);
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        Move(1);
    }

    [RelayCommand]
    private void MoveUp()
    {
        Move(-1);
    }

    partial void OnFilterTextChanged(string value)
    {
        Refresh();
    }

    private void Move(int offset)
    {
        if (Matches.Count == 0)
        {
            return;
        }

        var index = SelectedEntry is null ? -1 : Matches.IndexOf(SelectedEntry);
        SelectedEntry = Matches[(index + offset + Matches.Count) % Matches.Count];
    }

    private void Refresh()
    {
        Matches.Clear();
        foreach (var entry in FuzzyRanking.Rank(_entries, entry => entry.Title, FilterText))
        {
            Matches.Add(entry);
        }

        SelectedEntry = Matches.FirstOrDefault();
        HasNoMatches = Matches.Count == 0;
    }
}
