using System.Text.Json;
using Jbx.Platform.Windows.Settings;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Platform.Windows.Tests.Settings;

public sealed class WindowsSettingsFileStoreTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("jbx-settings-tests-");
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    [Fact]
    public async Task Reports_a_missing_file_as_missing_rather_than_unreadable()
    {
        var store = new WindowsSettingsFileStore(Path.Combine(_folder.FullName, "not-created-yet"), _time);

        var read = await store.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SettingsFileStatus.Missing, read.Status);
    }

    [Fact]
    public async Task Creates_the_folder_and_reads_back_what_it_wrote()
    {
        var store = new WindowsSettingsFileStore(Path.Combine(_folder.FullName, "getaxtools", "JBX"), _time);

        var written = await store.TryWriteAsync("{\"schemaVersion\":1}", TestContext.Current.CancellationToken);
        var read = await store.ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(written);
        Assert.Equal("{\"schemaVersion\":1}", read.Content);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.Location)!, "*.tmp"));
    }

    [Fact]
    public async Task Leaves_the_previous_file_intact_when_a_write_is_interrupted()
    {
        var store = new WindowsSettingsFileStore(_folder.FullName, _time);
        await store.TryWriteAsync("{\"schemaVersion\":1,\"lastConfigPath\":\"old\"}", TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(_folder.FullName, "settings.json.deadbeef.tmp"), "{\"schemaVer");

        var read = await store.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("{\"schemaVersion\":1,\"lastConfigPath\":\"old\"}", read.Content);
    }

    [Fact]
    public async Task Two_writers_never_leave_a_truncated_file()
    {
        var first = new WindowsSettingsFileStore(_folder.FullName, _time);
        var second = new WindowsSettingsFileStore(_folder.FullName, _time);
        var payload = "{\"schemaVersion\":1,\"padding\":\"" + new string('x', 20_000) + "\"}";

        await Task.WhenAll(
            Task.Run(() => WriteRepeatedlyAsync(first, payload), TestContext.Current.CancellationToken),
            Task.Run(() => WriteRepeatedlyAsync(second, payload), TestContext.Current.CancellationToken));

        var read = await first.ReadAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(read.Content!);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task Sets_a_corrupt_file_aside_under_a_timestamped_name()
    {
        var store = new WindowsSettingsFileStore(_folder.FullName, _time);
        await store.TryWriteAsync("{ corrupt", TestContext.Current.CancellationToken);

        var setAsideName = await store.TrySetAsideAsync(TestContext.Current.CancellationToken);

        Assert.Equal("settings.corrupt-20261020090000.json", setAsideName);
        Assert.False(File.Exists(store.Location));
        Assert.Equal("{ corrupt", File.ReadAllText(Path.Combine(_folder.FullName, setAsideName!)));
    }

    private static async Task WriteRepeatedlyAsync(WindowsSettingsFileStore store, string payload)
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            await store.TryWriteAsync(payload, TestContext.Current.CancellationToken);
        }
    }
}
