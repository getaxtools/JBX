using System.Text;

namespace Jbx.Services.Logs;

/// <summary>
/// How a log file's bytes become lines. Chosen from the byte order mark; without one the file is read
/// as UTF-8, which also covers ASCII. Invalid bytes become replacement characters rather than errors.
/// </summary>
public sealed class LogEncoding
{
    private LogEncoding(string name, Encoding encoding, int unitSize, bool isBigEndian, int preambleLength)
    {
        Name = name;
        Encoding = encoding;
        UnitSize = unitSize;
        IsBigEndian = isBigEndian;
        PreambleLength = preambleLength;
    }

    public static LogEncoding Utf8 { get; } = new("UTF-8", new UTF8Encoding(false, false), 1, false, 0);

    public string Name { get; }

    public Encoding Encoding { get; }

    /// <summary>Bytes per code unit: newlines are searched unit by unit so a UTF-16 newline is never split.</summary>
    public int UnitSize { get; }

    public bool IsBigEndian { get; }

    public int PreambleLength { get; }

    public static LogEncoding Detect(ReadOnlySpan<byte> start)
    {
        if (start.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return new LogEncoding("UTF-8", Utf8.Encoding, 1, false, 3);
        }

        if (start.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return new LogEncoding("UTF-16 LE", new UnicodeEncoding(false, false, false), 2, false, 2);
        }

        if (start.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return new LogEncoding("UTF-16 BE", new UnicodeEncoding(true, false, false), 2, true, 2);
        }

        return Utf8;
    }

    /// <summary>The index of the first byte after the next newline at or after <paramref name="from"/>, or -1.</summary>
    public int FindLineEnd(ReadOnlySpan<byte> bytes, int from)
    {
        if (UnitSize == 1)
        {
            var found = bytes[from..].IndexOf((byte)'\n');
            return found < 0 ? -1 : from + found + 1;
        }

        for (var index = from; index + 1 < bytes.Length; index += 2)
        {
            var isNewline = IsBigEndian
                ? bytes[index] == 0 && bytes[index + 1] == (byte)'\n'
                : bytes[index] == (byte)'\n' && bytes[index + 1] == 0;
            if (isNewline)
            {
                return index + 2;
            }
        }

        return -1;
    }

    public string Decode(ReadOnlySpan<byte> line)
    {
        var text = Encoding.GetString(line[..(line.Length - (line.Length % UnitSize))]);
        return text.TrimEnd('\r', '\n');
    }
}
