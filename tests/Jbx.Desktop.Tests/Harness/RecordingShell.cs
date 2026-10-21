using Jbx.Platform;

namespace Jbx.Desktop.Tests.Harness;

internal sealed class RecordingShell : IShell
{
    public List<string> OpenedFolders { get; } = [];

    public List<string> OpenedFiles { get; } = [];

    public Task<bool> OpenFolderAsync(CanonicalPath folder)
    {
        OpenedFolders.Add(folder.Display);
        return Task.FromResult(true);
    }

    public Task<bool> RevealInFolderAsync(CanonicalPath file)
    {
        OpenedFolders.Add(file.Display);
        return Task.FromResult(true);
    }

    public Task<bool> OpenFileAsync(CanonicalPath file)
    {
        if (IsProgramFile(file))
        {
            return Task.FromResult(false);
        }

        OpenedFiles.Add(file.Display);
        return Task.FromResult(true);
    }

    public bool IsProgramFile(CanonicalPath file)
    {
        return file.Display.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }
}
