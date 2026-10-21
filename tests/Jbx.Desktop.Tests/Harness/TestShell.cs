using Jbx.Core.Config;
using Jbx.Desktop.Features.Shell;
using Jbx.Desktop.Infrastructure;
using Jbx.Services.ConfigSession;
using Jbx.Services.Favourites;
using Jbx.Services.Logs;
using Jbx.Services.Probing;
using Jbx.Services.Settings;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Jbx.Desktop.Tests.Harness;

/// <summary>A main window wired to in-memory services: no disk, no network, no real dialogs.</summary>
internal sealed class TestShell : IDisposable
{
    public const string ConfigFolder = @"\\fs01.contoso.test\config";
    public const string ConfigPath = ConfigFolder + @"\jbx.yaml";

    public TestShell()
    {
        FileSystem = new InMemoryFileSystem(Paths);
        Gate = new FileSystemGate(Time);
        Settings = new SettingsService(SettingsStore, Log);
        var probe = new ReachabilityProbe(FileSystem, Gate, new ProbeCache(Time), Log);
        Session = new ConfigSessionService(FileSystem, Gate, Paths, TestConfigs.Loader(Time), Settings, Log);
        Services = new ShellServices(Session, Settings, new FavouritesService(Settings, Paths), probe, new FolderListingService(FileSystem, Gate, Log), TestConfigs.Resolver(Time), Paths, Shell)
        {
            Interaction = Interaction,
            Dispatcher = new ImmediateDispatcher(),
            Time = Time,
            DiagnosticLog = Log,
            Logs = new LogFileReader(FileSystem, Gate, Log, Time),
        };
        ViewModel = new MainWindowViewModel(Services);
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

    public SimplePathCanonicalizer Paths { get; } = SimplePathCanonicalizer.Instance;

    public InMemoryFileSystem FileSystem { get; }

    public InMemorySettingsFileStore SettingsStore { get; } = new();

    public RecordingDiagnosticLog Log { get; } = new();

    public FakeUserInteraction Interaction { get; } = new();

    public RecordingShell Shell { get; } = new();

    public FileSystemGate Gate { get; }

    public SettingsService Settings { get; }

    public ConfigSessionService Session { get; }

    public ShellServices Services { get; }

    public MainWindowViewModel ViewModel { get; }

    public void AddConfig(string fixture = "config/valid-full.yaml")
    {
        FileSystem.AddFileContent(ConfigFolder, "jbx.yaml", Fixture.ReadBytes(fixture));
    }

    public void RememberConfig()
    {
        SettingsStore.Content = $$"""{"schemaVersion": 1, "lastConfigPath": "{{ConfigPath.Replace(@"\", @"\\", StringComparison.Ordinal)}}"}""";
    }

    public void AddFoldersFor(string environmentId, ConfigDocument document, FolderBehaviour behaviour = FolderBehaviour.Present)
    {
        var environment = document.FindEnvironment(environmentId)!;
        var rows = Services.Rows(document, environment);
        foreach (var folder in rows.Folders.Where(folder => folder.Path is not null))
        {
            FileSystem.AddFolder(folder.Path!.Display, behaviour);
        }
    }

    public MainWindow CreateWindow()
    {
        return new MainWindow { DataContext = ViewModel };
    }

    public void Dispose()
    {
        ViewModel.Dispose();
        FileSystem.ReleaseBlackholed();
        Session.Dispose();
        Settings.Dispose();
        Gate.Dispose();
    }
}
