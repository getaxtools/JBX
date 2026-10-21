using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Jbx.Platform.Windows.Settings;

public sealed class WindowsSettingsFileStore(string folder, TimeProvider timeProvider) : ISettingsFileStore
{
    private const string FileName = "settings.json";
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public string Location => Path.Combine(folder, FileName);

    public async Task<SettingsFileRead> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(Location, Utf8WithoutBom, cancellationToken).ConfigureAwait(false);
            return new SettingsFileRead(SettingsFileStatus.Read, content);
        }
        catch (FileNotFoundException)
        {
            return new SettingsFileRead(SettingsFileStatus.Missing, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new SettingsFileRead(SettingsFileStatus.Missing, null);
        }
        catch (IOException)
        {
            return new SettingsFileRead(SettingsFileStatus.Unreadable, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new SettingsFileRead(SettingsFileStatus.Unreadable, null);
        }
    }

    public async Task<bool> TryWriteAsync(string content, CancellationToken cancellationToken)
    {
        var temporaryFile = Path.Combine(folder, $"{FileName}.{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}.tmp");
        try
        {
            Directory.CreateDirectory(folder);
            await WriteThroughAsync(temporaryFile, content, cancellationToken).ConfigureAwait(false);

            // Same-volume replace is atomic on NTFS: readers see the old file or the new one, never half of either.
            File.Move(temporaryFile, Location, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            _ = TryDelete(temporaryFile);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            _ = TryDelete(temporaryFile);
            return false;
        }
    }

    public Task<string?> TrySetAsideAsync(CancellationToken cancellationToken)
    {
        var stamp = timeProvider.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var setAsideName = $"settings.corrupt-{stamp}.json";
        try
        {
            File.Move(Location, Path.Combine(folder, setAsideName), overwrite: true);
            return Task.FromResult<string?>(setAsideName);
        }
        catch (IOException)
        {
            return Task.FromResult<string?>(null);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private static async Task WriteThroughAsync(string file, string content, CancellationToken cancellationToken)
    {
        var bytes = Utf8WithoutBom.GetBytes(content);
        var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.WriteThrough);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }
    }

    private static bool TryDelete(string file)
    {
        // Cleanup after a failed write; the failure itself is already reported to the caller.
        try
        {
            File.Delete(file);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
