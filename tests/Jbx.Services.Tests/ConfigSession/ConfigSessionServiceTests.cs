using Jbx.Core.Config;
using Jbx.Services.ConfigSession;
using Jbx.Services.Probing;
using Jbx.Services.Settings;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Services.Tests.ConfigSession;

public sealed class ConfigSessionServiceTests : IDisposable
{
    private const string ConfigFolder = @"\\fs01.contoso.test\config";
    private const string ConfigPath = ConfigFolder + @"\jbx.yaml";

    private readonly FakeTimeProvider _time = new();
    private readonly SimplePathCanonicalizer _paths = new(new Dictionary<char, string> { ['Z'] = ConfigFolder });
    private readonly InMemoryFileSystem _fileSystem;
    private readonly InMemorySettingsFileStore _store = new();
    private readonly SettingsService _settings;
    private readonly FileSystemGate _gate;
    private readonly ConfigSessionService _session;

    public ConfigSessionServiceTests()
    {
        _fileSystem = new InMemoryFileSystem(_paths);
        _settings = new SettingsService(_store, new RecordingDiagnosticLog());
        _gate = new FileSystemGate(_time);
        _session = new ConfigSessionService(_fileSystem, _gate, _paths, TestConfigs.Loader(_time), _settings, new RecordingDiagnosticLog());
    }

    public void Dispose()
    {
        _fileSystem.ReleaseBlackholed();
        _session.Dispose();
        _settings.Dispose();
        _gate.Dispose();
    }

    [Fact]
    public async Task Notices_a_change_on_disk_once_and_not_after_its_own_reload()
    {
        _fileSystem.AddFileContent(ConfigFolder, "jbx.yaml", Fixture.ReadBytes("config/valid-full.yaml"));
        await _session.OpenAsync(ConfigPath, TestContext.Current.CancellationToken);
        var beforeAnyChange = await _session.HasChangedOnDiskAsync(TestContext.Current.CancellationToken);

        _fileSystem.SetFileContent(_paths.From(ConfigPath), [.. Fixture.ReadBytes("config/valid-full.yaml"), .. "\n# edited\n"u8.ToArray()]);
        var afterEdit = await _session.HasChangedOnDiskAsync(TestContext.Current.CancellationToken);
        var checkedAgain = await _session.HasChangedOnDiskAsync(TestContext.Current.CancellationToken);
        await _session.ReloadAsync(TestContext.Current.CancellationToken);
        var afterReload = await _session.HasChangedOnDiskAsync(TestContext.Current.CancellationToken);

        Assert.Equal((false, true, false, false), (beforeAnyChange, afterEdit, checkedAgain, afterReload));
    }

    [Fact]
    public async Task Opens_a_valid_config_and_remembers_it_for_next_time()
    {
        _fileSystem.AddFileContent(ConfigFolder, "jbx.yaml", Fixture.ReadBytes("config/valid-full.yaml"));

        var outcome = await _session.OpenAsync(ConfigPath, TestContext.Current.CancellationToken);

        Assert.Equal(ConfigOpenStatus.Opened, outcome.Status);
        Assert.Equal(2, _session.Document?.Applications.Length);
        Assert.Equal(ConfigPath, _settings.Current.LastConfigPath);
        Assert.Equal([ConfigPath], _settings.Current.RecentConfigPaths);
    }

    [Fact]
    public async Task Keeps_the_previous_config_when_a_reload_finds_errors_and_reports_them_all()
    {
        _fileSystem.AddFileContent(ConfigFolder, "jbx.yaml", Fixture.ReadBytes("config/valid-full.yaml"));
        await _session.OpenAsync(ConfigPath, TestContext.Current.CancellationToken);
        var before = _session.Document;
        _fileSystem.AddFileContent(ConfigFolder, "jbx.yaml", Fixture.ReadBytes("config/several-errors.yaml"));

        var outcome = await _session.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConfigOpenStatus.Invalid, outcome.Status);
        Assert.Equal(5, outcome.Load?.Errors.Count);
        Assert.Same(before, _session.Document);
    }

    [Fact]
    public async Task Gives_up_on_a_config_on_an_unreachable_share_within_the_read_timeout()
    {
        _fileSystem.AddFolder(ConfigFolder, FolderBehaviour.Blackholed);
        await _settings.UpdateAsync(settings => settings with { LastConfigPath = ConfigPath }, TestContext.Current.CancellationToken);

        var pending = _session.OpenRememberedAsync(TestContext.Current.CancellationToken);
        _time.Advance(ConfigSessionService.ConfigReadTimeout);
        var outcome = await pending;

        Assert.Equal(ConfigOpenStatus.Timeout, outcome.Status);
        Assert.Null(_session.Document);
    }

    [Theory]
    [InlineData(FolderBehaviour.Denied, ConfigOpenStatus.Denied)]
    [InlineData(FolderBehaviour.Missing, ConfigOpenStatus.NotFound)]
    public async Task Says_why_a_config_could_not_be_read(FolderBehaviour behaviour, ConfigOpenStatus expected)
    {
        _fileSystem.AddFolder(ConfigFolder, behaviour);

        var outcome = await _session.OpenAsync(ConfigPath, TestContext.Current.CancellationToken);

        Assert.Equal(expected, outcome.Status);
    }

    [Fact]
    public async Task Refuses_a_file_too_large_to_be_a_config_without_reading_it()
    {
        _fileSystem.AddFileContent(ConfigFolder, "huge.yaml", new byte[ConfigParser.MaxConfigSizeBytes + 1]);

        var outcome = await _session.OpenAsync(ConfigFolder + @"\huge.yaml", TestContext.Current.CancellationToken);

        Assert.Equal(ConfigOpenStatus.TooLarge, outcome.Status);
    }

    [Fact]
    public async Task Lists_a_config_opened_through_a_mapped_drive_once_in_recent_configs()
    {
        _fileSystem.AddFileContent(ConfigFolder, "jbx.yaml", Fixture.ReadBytes("config/valid-full.yaml"));

        await _session.OpenAsync(ConfigPath, TestContext.Current.CancellationToken);
        await _session.OpenAsync(@"Z:\jbx.yaml", TestContext.Current.CancellationToken);

        Assert.Equal([@"Z:\jbx.yaml"], _settings.Current.RecentConfigPaths);
    }

    [Fact]
    public async Task Reports_when_there_is_no_remembered_config_to_open()
    {
        var outcome = await _session.OpenRememberedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConfigOpenStatus.NothingRemembered, outcome.Status);
    }

    [Fact]
    public async Task Rejects_text_that_is_not_a_path()
    {
        var outcome = await _session.OpenAsync("jbx.yaml", TestContext.Current.CancellationToken);

        Assert.Equal(ConfigOpenStatus.InvalidPath, outcome.Status);
    }
}
