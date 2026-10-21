namespace Jbx.Platform;

/// <summary>The one file JBX writes, kept in the user's own data folder. Failures are values, never exceptions.</summary>
public interface ISettingsFileStore
{
    string Location { get; }

    Task<SettingsFileRead> ReadAsync(CancellationToken cancellationToken);

    Task<bool> TryWriteAsync(string content, CancellationToken cancellationToken);

    /// <summary>Renames the current file out of the way and returns the new name, or null when it could not be moved.</summary>
    Task<string?> TrySetAsideAsync(CancellationToken cancellationToken);
}
