using System.Diagnostics;
using System.Text;
using Jbx.Platform;
using Jbx.Services.Logs;
using Jbx.Services.Probing;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Services.Tests.Performance;

public sealed class LogIndexPerformanceTests : IDisposable
{
    private const string Folder = @"\\fs01.contoso.test\AppLogs\prod\BillingPortal\Api\logs";
    private const int SizeBytes = 50 << 20;

    private readonly InMemoryFileSystem _fileSystem = new();
    private readonly FileSystemGate _gate = new(new FakeTimeProvider());
    private readonly CanonicalPath _file = SimplePathCanonicalizer.Instance.From(Folder + @"\api.log");

    public void Dispose()
    {
        _gate.Dispose();
    }

    [Fact]
    public async Task Indexes_fifty_megabytes_of_log_within_the_budget_and_reads_any_page_at_once()
    {
        _fileSystem.AddFolder(Folder);
        _fileSystem.SetFileContent(_file, SyntheticLog(SizeBytes));
        var document = new LogDocument(_file, new LogFileReader(_fileSystem, _gate, new RecordingDiagnosticLog(), TimeProvider.System));
        var indexing = Stopwatch.StartNew();

        await document.LoadAsync(null, TestContext.Current.CancellationToken);
        indexing.Stop();
        var reading = Stopwatch.StartNew();
        await document.ReadLinesAsync(document.LineCount / 2, 200, TestContext.Current.CancellationToken);
        reading.Stop();

        TestContext.Current.SendDiagnosticMessage($"{document.LineCount:N0} lines indexed in {indexing.ElapsedMilliseconds} ms; page read in {reading.ElapsedMilliseconds} ms");
        Assert.True(indexing.Elapsed < TimeSpan.FromSeconds(4), $"Indexing took {indexing.ElapsedMilliseconds} ms");
        Assert.True(reading.Elapsed < TimeSpan.FromMilliseconds(100), $"Reading a page took {reading.ElapsedMilliseconds} ms");
    }

    private static byte[] SyntheticLog(int sizeBytes)
    {
        var builder = new StringBuilder(sizeBytes + 200);
        var line = 0;
        while (builder.Length < sizeBytes)
        {
            builder.Append("2026-10-20 09:00:00.").Append(line % 1000).Append(line % 7 == 0 ? " WARN " : " INFO ")
                .Append("Billing.Api request ").Append(line).Append(" served in ").Append(line % 300).Append(" ms\n");
            line++;
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }
}
