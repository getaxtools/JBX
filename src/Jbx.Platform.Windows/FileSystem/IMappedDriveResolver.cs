namespace Jbx.Platform.Windows.FileSystem;

public interface IMappedDriveResolver
{
    /// <summary>The UNC path behind a mapped drive letter, or null when the letter is local or not mapped.</summary>
    string? TryResolve(char driveLetter);
}
