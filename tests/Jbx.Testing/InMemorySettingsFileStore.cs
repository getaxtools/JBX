using Jbx.Platform;

namespace Jbx.Testing;

public sealed class InMemorySettingsFileStore : ISettingsFileStore
{
    public string Location => @"C:\Users\asha.rao\AppData\Local\getaxtools\JBX\settings.json";

    public string? Content { get; set; }

    public bool IsReadable { get; set; } = true;

    public bool IsWritable { get; set; } = true;

    public List<string> SetAsideContents { get; } = [];

    public int WriteCount { get; private set; }

    public Task<SettingsFileRead> ReadAsync(CancellationToken cancellationToken)
    {
        if (!IsReadable)
        {
            return Task.FromResult(new SettingsFileRead(SettingsFileStatus.Unreadable, null));
        }

        return Task.FromResult(Content is null
            ? new SettingsFileRead(SettingsFileStatus.Missing, null)
            : new SettingsFileRead(SettingsFileStatus.Read, Content));
    }

    public Task<bool> TryWriteAsync(string content, CancellationToken cancellationToken)
    {
        if (!IsWritable)
        {
            return Task.FromResult(false);
        }

        Content = content;
        WriteCount++;
        return Task.FromResult(true);
    }

    public Task<string?> TrySetAsideAsync(CancellationToken cancellationToken)
    {
        if (Content is null)
        {
            return Task.FromResult<string?>(null);
        }

        SetAsideContents.Add(Content);
        Content = null;
        return Task.FromResult<string?>("settings.corrupt-20261020090000.json");
    }
}
