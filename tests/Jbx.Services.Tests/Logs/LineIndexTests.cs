using System.Text;
using Jbx.Services.Logs;
using Xunit;

namespace Jbx.Services.Tests.Logs;

public sealed class LineIndexTests
{
    [Fact]
    public void Finds_lines_split_across_reads_and_shows_an_unfinished_last_line()
    {
        var index = new LineIndex(LogEncoding.Utf8);

        index.Append("first\nsec"u8);
        var whileWriting = index.Count;
        index.Append("ond\nthird\n"u8);

        Assert.Equal(2, whileWriting);
        Assert.Equal(3, index.Count);
        Assert.Equal((6L, 13L), (index.SpanOf(1).Start, index.SpanOf(1).End));
        Assert.True(index.SpanOf(2).IsComplete);
    }

    [Fact]
    public void Classifies_each_line_and_lets_a_stack_trace_keep_the_level_of_its_error()
    {
        var index = new LineIndex(LogEncoding.Utf8);

        index.Append("10:00 INFO started\n10:01 ERROR failed\n   at Billing.Run()\nplain\n"u8);

        Assert.Equal([LineLevel.Info, LineLevel.Error, LineLevel.Error, LineLevel.None], Enumerable.Range(0, 4).Select(index.LevelOf));
    }

    [Fact]
    public void Reads_utf16_files_by_code_unit_so_no_newline_is_split()
    {
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("WARN one\r\n਀two\r\n")).ToArray();
        var encoding = LogEncoding.Detect(bytes);
        var index = new LineIndex(encoding);

        index.Append(bytes.AsSpan(encoding.PreambleLength));

        Assert.Equal("UTF-16 LE", encoding.Name);
        Assert.Equal(2, index.Count);
        Assert.Equal(LineLevel.Warning, index.LevelOf(0));
        Assert.Equal(2 + 20, index.SpanOf(1).Start);
    }

    [Fact]
    public void Skips_a_utf8_byte_order_mark()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\n' };

        var encoding = LogEncoding.Detect(bytes);

        Assert.Equal(3, encoding.PreambleLength);
        Assert.Equal("a", encoding.Decode(bytes.AsSpan(3)));
    }
}
