using Jbx.Desktop.Resources;
using Jbx.Platform;

namespace Jbx.Desktop.Features.Details;

/// <summary>Opens a file with its default app, refusing programs. Returns the message to show, or null when it opened.</summary>
internal static class FileOpener
{
    public static Task<string?> OpenAsync(IShell shell, FileEntry file)
    {
        return OpenAsync(shell, file.Path, file.Name);
    }

    public static async Task<string?> OpenAsync(IShell shell, CanonicalPath path, string name)
    {
        if (shell.IsProgramFile(path))
        {
            return Strings.FormatDetails_ProgramFileRefused(name);
        }

        var opened = await shell.OpenFileAsync(path).ConfigureAwait(true);
        return opened ? null : Strings.FormatDetails_OpenFileFailed(name);
    }
}
