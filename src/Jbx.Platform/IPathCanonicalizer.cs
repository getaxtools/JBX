namespace Jbx.Platform;

public interface IPathCanonicalizer
{
    /// <summary>Returns null for text that is not an absolute path on this platform.</summary>
    CanonicalPath? TryCanonicalize(string path);

    /// <summary>The containing folder, or null at a root.</summary>
    CanonicalPath? Parent(CanonicalPath path);

    string FileName(CanonicalPath path);
}
