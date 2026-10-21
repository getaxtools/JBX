using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Jbx.Desktop.Features.Dialogs;

internal sealed partial class TextPromptViewModel(string title, string prompt, string initialText, string acceptLabel) : ObservableObject
{
    public event EventHandler<string?>? CloseRequested;

    public string Title { get; } = title;

    public string Prompt { get; } = prompt;

    public string AcceptLabel { get; } = acceptLabel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    public partial string Text { get; set; } = initialText;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept()
    {
        CloseRequested?.Invoke(this, Text.Trim());
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, null);
    }

    private bool CanAccept()
    {
        return Text.Trim().Length > 0;
    }
}
