using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Jbx.Core.Config;
using Jbx.Desktop.Features.About;
using Jbx.Desktop.Features.Dialogs;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Infrastructure;

/// <summary>Shows dialogs and pickers over the main window and wires each dialog's close request to the window.</summary>
internal sealed class AvaloniaUserInteraction(Func<Window?> owner) : IUserInteraction
{
    private static readonly FilePickerFileType ConfigFiles = new(Strings.Shell_ConfigFile) { Patterns = ["*.yaml", "*.yml", "*.json"] };

    public async Task<string?> PickConfigFileAsync()
    {
        if (owner() is not { } window)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Shell_OpenConfig,
            AllowMultiple = false,
            FileTypeFilter = [ConfigFiles, FilePickerFileTypes.All],
        }).ConfigureAwait(true);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<DeploymentEnvironment?> PickEnvironmentAsync(IReadOnlyList<DeploymentEnvironment> environments, string? suggestedId)
    {
        if (owner() is not { } window)
        {
            return null;
        }

        var model = new EnvironmentPickerViewModel(environments, suggestedId);
        var dialog = new EnvironmentPickerWindow { DataContext = model };
        model.CloseRequested += (_, chosen) => dialog.Close(chosen);
        return await dialog.ShowDialog<DeploymentEnvironment?>(window).ConfigureAwait(true);
    }

    public async Task ShowConfigProblemsAsync(ConfigProblemsViewModel problems)
    {
        if (owner() is not { } window)
        {
            return;
        }

        var dialog = new ConfigProblemsWindow { DataContext = problems };
        problems.CloseRequested += (_, _) => dialog.Close();
        await dialog.ShowDialog(window).ConfigureAwait(true);
    }

    public async Task<string?> AskForTextAsync(TextPromptViewModel prompt)
    {
        if (owner() is not { } window)
        {
            return null;
        }

        var dialog = new TextPromptWindow { DataContext = prompt };
        prompt.CloseRequested += (_, text) => dialog.Close(text);
        return await dialog.ShowDialog<string?>(window).ConfigureAwait(true);
    }

    public async Task CopyTextAsync(string text)
    {
        if (owner()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);
        }
    }

    public async Task ShowAboutAsync()
    {
        if (owner() is not { } window)
        {
            return;
        }

        var model = new AboutViewModel();
        var dialog = new AboutWindow { DataContext = model };
        model.CloseRequested += (_, _) => dialog.Close();
        await dialog.ShowDialog(window).ConfigureAwait(true);
    }

    public async Task<bool?> SaveTextAsync(string suggestedFileName, string text)
    {
        if (owner() is not { } window)
        {
            return false;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = suggestedFileName }).ConfigureAwait(true);
        if (file is null)
        {
            return null;
        }

        try
        {
            var stream = await file.OpenWriteAsync().ConfigureAwait(true);
            await using (stream.ConfigureAwait(true))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(text).ConfigureAwait(true);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
