using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Core.Config;

namespace Jbx.Desktop.Features.Dialogs;

internal sealed partial class EnvironmentPickerViewModel : ObservableObject
{
    public EnvironmentPickerViewModel(IReadOnlyList<DeploymentEnvironment> environments, string? suggestedId)
    {
        Choices = [.. environments.Select(environment => new EnvironmentChoice(environment))];
        Selected = Choices.FirstOrDefault(choice => choice.Environment.Id == suggestedId) ?? (Choices.Count > 0 ? Choices[0] : null);
    }

    public event EventHandler<DeploymentEnvironment?>? CloseRequested;

    public IReadOnlyList<EnvironmentChoice> Choices { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    public partial EnvironmentChoice? Selected { get; set; }

    [ObservableProperty]
    public partial int FocusToken { get; set; } = 1;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept()
    {
        CloseRequested?.Invoke(this, Selected?.Environment);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, null);
    }

    private bool CanAccept()
    {
        return Selected is not null;
    }
}
