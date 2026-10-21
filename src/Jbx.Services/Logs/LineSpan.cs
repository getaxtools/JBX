namespace Jbx.Services.Logs;

public readonly record struct LineSpan(long Start, long End, LineLevel Level, bool IsComplete)
{
    public long Length => End - Start;
}
