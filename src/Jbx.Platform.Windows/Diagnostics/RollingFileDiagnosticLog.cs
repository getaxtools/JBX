using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Jbx.Core.Diagnostics;

namespace Jbx.Platform.Windows.Diagnostics;

public sealed class RollingFileDiagnosticLog : IDiagnosticLog, IDisposable
{
    private const string FilePrefix = "jbx-";
    private const string FileExtension = ".log";
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly DiagnosticLogOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<string> _pendingLines;
    private readonly Task _writer;
    private string? _currentFile;
    private long _currentFileBytes;

    public RollingFileDiagnosticLog(DiagnosticLogOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
        _pendingLines = Channel.CreateBounded<string>(new BoundedChannelOptions(options.MaxQueuedLines)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        _writer = Task.Run(WriteQueuedLinesAsync);
    }

    public void Write(AppEvent appEvent)
    {
        Enqueue(DiagnosticLogLine.Format(_timeProvider.GetUtcNow(), appEvent, detail: null));
    }

    public void Write(AppEvent appEvent, ResourceDetail detail)
    {
        Enqueue(DiagnosticLogLine.Format(_timeProvider.GetUtcNow(), appEvent, detail));
    }

    public void Dispose()
    {
        _pendingLines.Writer.TryComplete();

        // Runs at shutdown, after the UI loop: a bounded wait so queued lines reach the disk.
        _writer.Wait(_options.FlushTimeout);
    }

    private void Enqueue(string line)
    {
        _pendingLines.Writer.TryWrite(line);
    }

    private async Task WriteQueuedLinesAsync()
    {
        await foreach (var line in _pendingLines.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            TryAppend(line);
        }
    }

    private void TryAppend(string line)
    {
        // A diagnostic log that throws turns a handled failure into an unhandled one,
        // so a failed write is dropped here and nowhere else.
        try
        {
            Append(line);
        }
        catch (IOException)
        {
            _currentFile = null;
        }
        catch (UnauthorizedAccessException)
        {
            _currentFile = null;
        }
    }

    private void Append(string line)
    {
        var bytes = Utf8WithoutBom.GetBytes(line + "\n");
        var file = _currentFile;
        if (file is null || _currentFileBytes + bytes.Length > _options.MaxFileBytes)
        {
            file = StartNewFile();
        }

        using (var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            stream.Write(bytes);
        }

        _currentFileBytes += bytes.Length;
    }

    private string StartNewFile()
    {
        Directory.CreateDirectory(_options.Folder);
        DeleteOldestFilesBeyondTotalLimit();
        var file = Path.Combine(_options.Folder, NewFileName());
        _currentFile = file;
        _currentFileBytes = 0;
        return file;
    }

    private string NewFileName()
    {
        var started = _timeProvider.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var processId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var unique = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));
        return $"{FilePrefix}{started}-{processId}-{unique}{FileExtension}";
    }

    private void DeleteOldestFilesBeyondTotalLimit()
    {
        var newestFirst = new DirectoryInfo(_options.Folder)
            .EnumerateFiles(FilePrefix + "*" + FileExtension)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        var budgetForExistingFiles = _options.MaxTotalBytes - _options.MaxFileBytes;
        long keptBytes = 0;
        foreach (var file in newestFirst)
        {
            keptBytes += file.Length;
            if (keptBytes > budgetForExistingFiles)
            {
                TryDelete(file);
            }
        }
    }

    private static void TryDelete(FileInfo file)
    {
        // A scanner or another instance may hold the file for a moment; the next rotation tries again.
        try
        {
            file.Delete();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
    }
}
