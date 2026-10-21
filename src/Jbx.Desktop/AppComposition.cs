using Jbx.Core.Config;
using Jbx.Core.Diagnostics;
using Jbx.Core.Templating;
using Jbx.Desktop.Features.Shell;
using Jbx.Desktop.Infrastructure;
using Jbx.Platform;
using Jbx.Platform.Windows;
using Jbx.Platform.Windows.Diagnostics;
using Jbx.Platform.Windows.FileSystem;
using Jbx.Platform.Windows.Settings;
using Jbx.Platform.Windows.Shell;
using Jbx.Services.ConfigSession;
using Jbx.Services.Favourites;
using Jbx.Services.Logs;
using Jbx.Services.Probing;
using Jbx.Services.Settings;

namespace Jbx.Desktop;

/// <summary>The one place that creates services and names platform types.</summary>
internal sealed class AppComposition : IDisposable
{
    private readonly RollingFileDiagnosticLog _diagnosticLog;
    private readonly List<IDisposable> _owned = [];
    private MainWindow? _window;

    private AppComposition(RollingFileDiagnosticLog diagnosticLog, ShellServices services)
    {
        _diagnosticLog = diagnosticLog;
        Services = services;
    }

    public ShellServices Services { get; }

    public IDiagnosticLog DiagnosticLog => _diagnosticLog;

    public required ISystemContrast Contrast { get; init; }

    public MainWindowViewModel? MainViewModel { get; private set; }

    public static AppComposition ForCurrentPlatform()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var time = TimeProvider.System;
        var knownFolders = new WindowsKnownFolders();
        var diagnosticLog = new RollingFileDiagnosticLog(new DiagnosticLogOptions(knownFolders.DiagnosticLogFolder), time);
        var paths = new WindowsPathCanonicalizer(new NetworkDriveResolver());
        var fileSystem = new WindowsFileSystem(paths);
        var gate = new FileSystemGate(time);
        var probe = new ReachabilityProbe(fileSystem, gate, new ProbeCache(time), diagnosticLog);
        var resolver = new PathTemplateResolver(new ProcessEnvironmentVariables(), time);
        var settings = new SettingsService(new WindowsSettingsFileStore(knownFolders.UserDataFolder, time), diagnosticLog);
        var session = new ConfigSessionService(fileSystem, gate, paths, new ConfigLoader(new ConfigValidator(resolver)), settings, diagnosticLog);
        AppComposition? composition = null;
        var services = new ShellServices(
            session,
            settings,
            new FavouritesService(settings, paths),
            probe,
            new FolderListingService(fileSystem, gate, diagnosticLog),
            resolver,
            paths,
            new WindowsShell())
        {
            Interaction = new AvaloniaUserInteraction(() => composition?._window),
            Dispatcher = new AvaloniaUiDispatcher(),
            Time = time,
            DiagnosticLog = diagnosticLog,
            Logs = new LogFileReader(fileSystem, gate, diagnosticLog, time),
        };
        composition = new AppComposition(diagnosticLog, services) { Contrast = new WindowsSystemContrast() };
        composition._owned.AddRange([session, settings, gate]);
        return composition;
    }

    public MainWindow CreateMainWindow()
    {
        MainViewModel = new MainWindowViewModel(Services);
        _window = new MainWindow { DataContext = MainViewModel };
        return _window;
    }

    public void Dispose()
    {
        MainViewModel?.Dispose();
        _owned.ForEach(owned => owned.Dispose());
        if (OperatingSystem.IsWindows())
        {
            _diagnosticLog.Dispose();
        }
    }
}
