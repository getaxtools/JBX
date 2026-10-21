using System.Text;
using Jbx.Platform.Windows.FileSystem;
using Jbx.Services.Logs;
using Jbx.Services.Probing;
using Jbx.Testing;
using Xunit;

namespace Jbx.Platform.Windows.Tests.FileSystem;

/// <summary>The viewer's document against real files, written the way services write their logs.</summary>
public sealed class LogDocumentOnDiskTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("jbx-log-tests-");
    private readonly WindowsPathCanonicalizer _paths = new(new NetworkDriveResolver());
    private readonly FileSystemGate _gate = new(TimeProvider.System);
    private readonly LogFileReader _reader;

    public LogDocumentOnDiskTests()
    {
        _reader = new LogFileReader(new WindowsFileSystem(_paths), _gate, new RecordingDiagnosticLog(), TimeProvider.System);
    }

    public void Dispose()
    {
        _gate.Dispose();
        _root.Delete(recursive: true);
    }

    [Fact]
    public async Task Follows_a_log_that_its_writer_keeps_open_and_appends_to()
    {
        var file = Path.Combine(_root.FullName, "api.log");
        using var writer = new StreamWriter(new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        await writer.WriteAsync("09:00 INFO started\n09:01 WARN slow share\n");
        var document = new LogDocument(_paths.TryCanonicalize(file)!, _reader);
        await document.LoadAsync(null, TestContext.Current.CancellationToken);

        await writer.WriteAsync("09:02 ERROR payment run failed\n");
        var refresh = await document.RefreshAsync(TestContext.Current.CancellationToken);
        var page = await document.ReadLinesAsync(0, 10, TestContext.Current.CancellationToken);

        Assert.Equal(3, refresh.LineCount);
        Assert.Equal(["09:00 INFO started", "09:01 WARN slow share", "09:02 ERROR payment run failed"], page.Lines.Select(line => line.Text));
        Assert.Equal(LineLevel.Error, page.Lines[2].Level);
    }

    [Fact]
    public async Task Reads_a_utf16_log_as_written_by_windows_powershell()
    {
        var file = Path.Combine(_root.FullName, "deploy.log");
        await File.WriteAllTextAsync(file, "INFO Copying files\r\nERROR Access to C:\\Apps was denied\r\n", Encoding.Unicode, TestContext.Current.CancellationToken);
        var document = new LogDocument(_paths.TryCanonicalize(file)!, _reader);

        await document.LoadAsync(null, TestContext.Current.CancellationToken);
        var page = await document.ReadLinesAsync(0, 10, TestContext.Current.CancellationToken);

        Assert.Equal("UTF-16 LE", document.Encoding?.Name);
        Assert.Equal(["INFO Copying files", @"ERROR Access to C:\Apps was denied"], page.Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task Notices_a_log_rotated_to_a_new_shorter_file()
    {
        var file = Path.Combine(_root.FullName, "api.log");
        await File.WriteAllTextAsync(file, string.Concat(Enumerable.Repeat("09:00 INFO request served\n", 50)), TestContext.Current.CancellationToken);
        var document = new LogDocument(_paths.TryCanonicalize(file)!, _reader);
        await document.LoadAsync(null, TestContext.Current.CancellationToken);

        File.Move(file, file + ".1");
        await File.WriteAllTextAsync(file, "10:00 INFO new day\n", TestContext.Current.CancellationToken);
        var refresh = await document.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(refresh.WasTruncated);
    }
}
