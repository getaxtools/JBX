using System.Text;
using Jbx.Platform;
using Jbx.Services.Logs;
using Jbx.Services.Probing;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Services.Tests.Logs;

public sealed class LogDocumentTests : IDisposable
{
    private const string Folder = @"\\fs01.contoso.test\AppLogs\prod\BillingPortal\Api\logs";

    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryFileSystem _fileSystem = new();
    private readonly FileSystemGate _gate;
    private readonly LogFileReader _reader;
    private readonly CanonicalPath _file = SimplePathCanonicalizer.Instance.From(Folder + @"\api-2026-10-20.log");

    public LogDocumentTests()
    {
        _gate = new FileSystemGate(_time);
        _reader = new LogFileReader(_fileSystem, _gate, new RecordingDiagnosticLog(), _time);
    }

    public void Dispose()
    {
        _fileSystem.ReleaseBlackholed();
        _gate.Dispose();
    }

    [Fact]
    public async Task Indexes_a_file_larger_than_one_read_and_reads_any_page_of_it()
    {
        WriteLog(Lines(40_000));
        var document = new LogDocument(_file, _reader);

        var outcome = await document.LoadAsync(null, TestContext.Current.CancellationToken);
        var page = await document.ReadLinesAsync(25_000, 3, TestContext.Current.CancellationToken);

        Assert.Equal(Reachability.Reachable, outcome);
        Assert.True(document.FileLength > LogDocument.ChunkBytes);
        Assert.Equal(40_000, document.LineCount);
        Assert.Equal(["25000 INFO request 25000 served", "25001 WARN request 25001 served", "25002 INFO request 25002 served"], page.Lines.Select(line => line.Text));
        Assert.Equal(LineLevel.Warning, page.Lines[1].Level);
    }

    [Fact]
    public async Task Follows_new_lines_and_a_line_still_being_written()
    {
        WriteLog("one\ntw");
        var document = new LogDocument(_file, _reader);
        await document.LoadAsync(null, TestContext.Current.CancellationToken);

        _fileSystem.AppendFileContent(_file, "o\nthree\n"u8.ToArray());
        var refresh = await document.RefreshAsync(TestContext.Current.CancellationToken);
        var page = await document.ReadLinesAsync(0, 10, TestContext.Current.CancellationToken);

        Assert.Equal((1, 3), (refresh.FirstChangedLine, refresh.LineCount));
        Assert.Equal(["one", "two", "three"], page.Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task Does_not_report_a_finished_last_line_as_changed_when_lines_are_added()
    {
        WriteLog("one\ntwo\n");
        var document = new LogDocument(_file, _reader);
        await document.LoadAsync(null, TestContext.Current.CancellationToken);

        _fileSystem.AppendFileContent(_file, "three\n"u8.ToArray());
        var refresh = await document.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal((2, 3), (refresh.FirstChangedLine, refresh.LineCount));
    }

    [Fact]
    public async Task Notices_when_the_file_was_rotated_or_cleared()
    {
        WriteLog(Lines(100));
        var document = new LogDocument(_file, _reader);
        await document.LoadAsync(null, TestContext.Current.CancellationToken);

        _fileSystem.SetFileContent(_file, "fresh\n"u8.ToArray());
        var refresh = await document.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(refresh.WasTruncated);
    }

    [Fact]
    public async Task Scans_for_text_and_for_levels_and_returns_line_numbers_in_order()
    {
        WriteLog(Lines(1_000));
        var document = new LogDocument(_file, _reader);
        await document.LoadAsync(null, TestContext.Current.CancellationToken);

        var text = await document.ScanAsync(new LogScanRequest(Matcher(new LogQuery { Text = "request 99" }), 0), TestContext.Current.CancellationToken);
        var warnings = await document.ScanAsync(new LogScanRequest(Matcher(new LogQuery { Levels = [LineLevel.Warning] }), 990), TestContext.Current.CancellationToken);

        Assert.Equal([99, 990, 991, 992, 993, 994, 995, 996, 997, 998, 999], text.Lines);
        Assert.Equal([991, 993, 995, 997, 999], warnings.Lines);
    }

    [Fact]
    public async Task Cuts_a_line_longer_than_the_display_limit_and_says_so()
    {
        WriteLog("short\n" + new string('x', LogDocument.MaxLineBytes + 10) + "\n");
        var document = new LogDocument(_file, _reader);
        await document.LoadAsync(null, TestContext.Current.CancellationToken);

        var page = await document.ReadLinesAsync(1, 1, TestContext.Current.CancellationToken);

        var line = Assert.Single(page.Lines);
        Assert.True(line.IsCut);
        Assert.Equal(LogDocument.MaxLineBytes, line.Text.Length);
    }

    [Fact]
    public async Task Reports_denied_rather_than_an_empty_file()
    {
        _fileSystem.AddFolder(Folder, FolderBehaviour.Denied);
        var document = new LogDocument(_file, _reader);

        var outcome = await document.LoadAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(Reachability.Denied, outcome);
        Assert.Equal(0, document.LineCount);
    }

    [Fact]
    public async Task Gives_up_on_a_share_that_stops_answering_instead_of_waiting_forever()
    {
        _fileSystem.AddFolder(Folder, FolderBehaviour.Blackholed);
        var document = new LogDocument(_file, _reader);

        var loading = document.LoadAsync(null, TestContext.Current.CancellationToken);
        await Eventually.TrueAsync(() => _fileSystem.InFlight == 1);
        _time.Advance(LogFileReader.ReadTimeout);

        Assert.Equal(Reachability.Timeout, await loading);
    }

    private void WriteLog(string content)
    {
        _fileSystem.AddFolder(Folder);
        _fileSystem.SetFileContent(_file, Encoding.UTF8.GetBytes(content));
    }

    private static string Lines(int count)
    {
        var builder = new StringBuilder();
        for (var line = 0; line < count; line++)
        {
            builder.Append(line).Append(line % 2 == 0 ? " INFO" : " WARN").Append(" request ").Append(line).Append(" served\n");
        }

        return builder.ToString();
    }

    private static LogMatcher Matcher(LogQuery query)
    {
        return LogMatcher.Create(query).Matcher!;
    }
}
