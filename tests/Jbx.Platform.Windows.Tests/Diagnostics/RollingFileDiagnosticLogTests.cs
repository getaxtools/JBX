using System.Text.Json;
using Jbx.Core.Diagnostics;
using Jbx.Platform.Windows.Diagnostics;
using Xunit;

namespace Jbx.Platform.Windows.Tests.Diagnostics;

public sealed class RollingFileDiagnosticLogTests : IDisposable
{
    private static readonly AppEvent Started = new(EventCode.AppStarted, EventSeverity.Information);

    // These tests read the folder after disposal, so they wait for the whole queue even on a loaded machine.
    private static readonly TimeSpan FullFlush = TimeSpan.FromSeconds(30);

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("jbx-log-tests-");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    [Fact]
    public void Writes_each_event_as_one_json_line_in_the_log_folder()
    {
        var detail = new ResourceDetail(new ResourceItem(ResourceKind.Path, @"\\fs01.contoso.test\logs"));

        using (var log = CreateLog(new DiagnosticLogOptions(_folder.FullName)))
        {
            log.Write(Started);
            log.Write(new AppEvent(EventCode.ProbeTimedOut, EventSeverity.Warning), detail);
        }

        var lines = AllLines();
        Assert.Equal(2, lines.Count);
        Assert.Equal("AppStarted", EventName(lines[0]));
        Assert.Equal("ProbeTimedOut", EventName(lines[1]));
    }

    [Fact]
    public void Starts_a_new_file_when_the_current_one_would_exceed_its_size_limit()
    {
        var options = new DiagnosticLogOptions(_folder.FullName) { MaxFileBytes = 400, MaxTotalBytes = 100_000, FlushTimeout = FullFlush };

        using (var log = CreateLog(options))
        {
            WriteEvents(log, count: 20);
        }

        var files = _folder.GetFiles("jbx-*.log");
        Assert.True(files.Length > 1);
        Assert.All(files, file => Assert.True(file.Length <= options.MaxFileBytes));
        Assert.Equal(20, AllLines().Count);
    }

    [Fact]
    public void Deletes_the_oldest_files_so_the_folder_stays_within_its_total_limit()
    {
        var options = new DiagnosticLogOptions(_folder.FullName) { MaxFileBytes = 400, MaxTotalBytes = 1_200, FlushTimeout = FullFlush };

        using (var log = CreateLog(options))
        {
            WriteEvents(log, count: 200);
        }

        var totalBytes = _folder.GetFiles("jbx-*.log").Sum(file => file.Length);
        Assert.True(totalBytes <= options.MaxTotalBytes, $"{totalBytes} bytes on disk");
    }

    [Fact]
    public void Never_throws_when_the_log_folder_cannot_be_created()
    {
        var blockingFile = Path.Combine(_folder.FullName, "not-a-folder");
        File.WriteAllText(blockingFile, "occupied");
        var options = new DiagnosticLogOptions(Path.Combine(blockingFile, "logs"));

        var exception = Record.Exception(() =>
        {
            using var log = CreateLog(options);
            WriteEvents(log, count: 5);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Keeps_every_line_whole_when_many_threads_write_at_once()
    {
        var options = new DiagnosticLogOptions(_folder.FullName) { MaxQueuedLines = 10_000, FlushTimeout = FullFlush };

        using (var log = CreateLog(options))
        {
            Parallel.For(0, 8, _ => WriteEvents(log, count: 250));
        }

        var lines = AllLines();
        Assert.Equal(2_000, lines.Count);
        Assert.All(lines, line => Assert.Equal("ProbeCompleted", EventName(line)));
    }

    [Fact]
    public void Two_logs_sharing_a_folder_write_to_separate_files()
    {
        var options = new DiagnosticLogOptions(_folder.FullName);

        using (var first = CreateLog(options))
        using (var second = CreateLog(options))
        {
            first.Write(Started);
            second.Write(Started);
        }

        Assert.Equal(2, _folder.GetFiles("jbx-*.log").Length);
    }

    private static RollingFileDiagnosticLog CreateLog(DiagnosticLogOptions options)
    {
        return new RollingFileDiagnosticLog(options, TimeProvider.System);
    }

    private static void WriteEvents(RollingFileDiagnosticLog log, int count)
    {
        for (var i = 0; i < count; i++)
        {
            log.Write(new AppEvent(EventCode.ProbeCompleted, EventSeverity.Information, new EventMeasure(MeasureKind.Count, i)));
        }
    }

    private List<string> AllLines()
    {
        return _folder.GetFiles("jbx-*.log")
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .SelectMany(file => File.ReadAllLines(file.FullName))
            .ToList();
    }

    private static string? EventName(string line)
    {
        using var json = JsonDocument.Parse(line);
        return json.RootElement.GetProperty("event").GetString();
    }
}
