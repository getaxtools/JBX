using Jbx.Core.Config;
using Jbx.Desktop.Features.Dialogs;

namespace Jbx.Desktop.Infrastructure;

/// <summary>Everything a view model needs from the person in front of the screen: pickers, dialogs, the clipboard.</summary>
internal interface IUserInteraction
{
    Task<string?> PickConfigFileAsync();

    Task<DeploymentEnvironment?> PickEnvironmentAsync(IReadOnlyList<DeploymentEnvironment> environments, string? suggestedId);

    Task ShowConfigProblemsAsync(ConfigProblemsViewModel problems);

    Task<string?> AskForTextAsync(TextPromptViewModel prompt);

    Task CopyTextAsync(string text);

    Task ShowAboutAsync();

    /// <summary>Asks where to save and writes the text there. Returns null when the user cancelled.</summary>
    Task<bool?> SaveTextAsync(string suggestedFileName, string text);
}
