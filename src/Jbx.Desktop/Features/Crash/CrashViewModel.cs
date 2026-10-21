using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jbx.Desktop.Infrastructure;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Features.Crash;

/// <summary>The crash dialog: the report exactly as it would be shared, plus Copy, Save and Close.</summary>
internal sealed partial class CrashViewModel(string report, IUserInteraction interaction) : ObservableObject
{
    public event EventHandler? CloseRequested;

    public string Report { get; } = report;

    [ObservableProperty]
    public partial string? ActionMessage { get; set; }

    [RelayCommand]
    private async Task CopyAsync()
    {
        await interaction.CopyTextAsync(Report).ConfigureAwait(true);
        ActionMessage = Strings.Crash_Copied;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var saved = await interaction.SaveTextAsync("jbx-crash-report.txt", Report).ConfigureAwait(true);
        ActionMessage = saved switch
        {
            true => Strings.Crash_Saved,
            false => Strings.Crash_SaveFailed,
            null => null,
        };
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
