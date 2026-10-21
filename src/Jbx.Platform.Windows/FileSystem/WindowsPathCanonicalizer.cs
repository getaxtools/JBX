namespace Jbx.Platform.Windows.FileSystem;

public sealed class WindowsPathCanonicalizer(IMappedDriveResolver mappedDrives) : IPathCanonicalizer
{
    private const string LongUncPrefix = @"\\?\UNC\";
    private const string LongPathPrefix = @"\\?\";
    private const string DevicePrefix = @"\\.\";

    public CanonicalPath? TryCanonicalize(string path)
    {
        var text = StripLongPathPrefix(path.Trim().Replace('/', '\\'));
        if (text.Length == 0 || text.StartsWith(DevicePrefix, StringComparison.Ordinal) || !Path.IsPathFullyQualified(text))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(text);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var display = TrimTrailingSeparator(full);
        return new CanonicalPath(display, ResolveMappedDrive(display).ToUpperInvariant());
    }

    public CanonicalPath? Parent(CanonicalPath path)
    {
        var parent = Path.GetDirectoryName(path.Display);
        return parent is null ? null : TryCanonicalize(parent);
    }

    public string FileName(CanonicalPath path)
    {
        var name = Path.GetFileName(path.Display);
        return name.Length > 0 ? name : path.Display;
    }

    private static string StripLongPathPrefix(string text)
    {
        if (text.StartsWith(LongUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + text[LongUncPrefix.Length..];
        }

        return text.StartsWith(LongPathPrefix, StringComparison.Ordinal) ? text[LongPathPrefix.Length..] : text;
    }

    private static string TrimTrailingSeparator(string full)
    {
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var trimmed = full.TrimEnd('\\');
        return trimmed.Length < root.TrimEnd('\\').Length || (trimmed.Length == 2 && trimmed[1] == ':')
            ? root
            : trimmed;
    }

    private string ResolveMappedDrive(string display)
    {
        if (display.Length < 2 || display[1] != ':' || !char.IsAsciiLetter(display[0]))
        {
            return display;
        }

        var remote = mappedDrives.TryResolve(display[0]);
        return remote is null ? display : remote.TrimEnd('\\') + display[2..];
    }
}
