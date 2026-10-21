namespace Jbx.Platform;

/// <summary>The only way JBX starts another program. Paths are always passed as arguments, never in a command string.</summary>
public interface IShell
{
    Task<bool> OpenFolderAsync(CanonicalPath folder);

    Task<bool> RevealInFolderAsync(CanonicalPath file);

    /// <summary>Opens a document with its default app. Refuses program files: see <see cref="IsProgramFile"/>.</summary>
    Task<bool> OpenFileAsync(CanonicalPath file);

    bool IsProgramFile(CanonicalPath file);
}
