using Jbx.Platform;

namespace Jbx.Testing;

/// <summary>Windows-style canonical paths without touching the OS, with optional mapped drives.</summary>
public sealed class SimplePathCanonicalizer(IReadOnlyDictionary<char, string>? mappedDrives = null) : IPathCanonicalizer
{
    public static SimplePathCanonicalizer Instance { get; } = new();

    public CanonicalPath? TryCanonicalize(string path)
    {
        var text = path.Trim().Replace('/', '\\');
        var isDrivePath = text.Length >= 3 && char.IsAsciiLetter(text[0]) && text[1] == ':' && text[2] == '\\';
        if (!isDrivePath && !text.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return null;
        }

        var display = text.Length > 3 ? text.TrimEnd('\\') : text;
        var key = display;
        if (isDrivePath && mappedDrives is not null && mappedDrives.TryGetValue(char.ToUpperInvariant(display[0]), out var remote))
        {
            key = (remote.TrimEnd('\\') + display[2..]).TrimEnd('\\');
        }

        return new CanonicalPath(display, key.ToUpperInvariant());
    }

    public CanonicalPath? Parent(CanonicalPath path)
    {
        var display = path.Display.TrimEnd('\\');
        var separator = display.LastIndexOf('\\');
        if (separator == 2 && display[1] == ':')
        {
            return display.Length > 3 ? TryCanonicalize(display[..3]) : null;
        }

        return separator <= 2 ? null : TryCanonicalize(display[..separator]);
    }

    public string FileName(CanonicalPath path)
    {
        var separator = path.Display.LastIndexOf('\\');
        return separator < 0 ? path.Display : path.Display[(separator + 1)..];
    }

    public CanonicalPath From(string path)
    {
        return TryCanonicalize(path) ?? throw new ArgumentException($"Not an absolute path: {path}", nameof(path));
    }
}
