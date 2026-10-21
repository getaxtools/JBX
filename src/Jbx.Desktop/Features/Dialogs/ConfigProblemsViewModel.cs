using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Core.Config;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;

namespace Jbx.Desktop.Features.Dialogs;

/// <summary>Every problem in a config at once, so the owner can fix them all in one pass.</summary>
internal sealed partial class ConfigProblemsViewModel : ObservableObject
{
    private readonly IUserInteraction _interaction;

    public ConfigProblemsViewModel(IUserInteraction interaction, string fileName, IReadOnlyList<ConfigProblem> errors, bool keptPrevious)
    {
        _interaction = interaction;
        Problems = [.. errors.Select(error => new ConfigProblemLine(ConfigProblemText.Describe(error), ConfigProblemText.Where(error)))];
        Intro = Strings.FormatProblems_Intro(fileName, Problems.Count);
        Outcome = keptPrevious ? Strings.Problems_KeptPrevious : Strings.Problems_NothingLoaded;
    }

    public event EventHandler? CloseRequested;

    public string Intro { get; }

    public string Outcome { get; }

    public IReadOnlyList<ConfigProblemLine> Problems { get; }

    [ObservableProperty]
    public partial ConfigProblemLine? Selected { get; set; }

    [ObservableProperty]
    public partial string? ActionMessage { get; set; }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        var text = string.Join(System.Environment.NewLine, Problems.Select(problem => problem.ToString()));
        await _interaction.CopyTextAsync(text).ConfigureAwait(true);
        ActionMessage = Strings.Problems_Copied;
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
