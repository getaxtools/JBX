using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Jbx.Core.Diagnostics;
using Jbx.Desktop.Features.Crash;
using Jbx.Desktop.Infrastructure;

namespace Jbx.Desktop;

internal sealed partial class App : Application
{
    private AppComposition? _composition;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            CrashHandlers.Install(() => _composition?.DiagnosticLog);
            PlatformSettings?.HotkeyConfiguration.OpenContextMenu.Add(CommandCatalog.ContextMenu);
            _composition = AppComposition.ForCurrentPlatform();
            ContrastThemes.Follow(this, _composition.Contrast);
            _composition.DiagnosticLog.Write(new AppEvent(EventCode.AppStarted, EventSeverity.Information));
            desktop.MainWindow = _composition.CreateMainWindow();
            desktop.Exit += OnDesktopExit;
            var viewModel = _composition.MainViewModel;
            Dispatcher.UIThread.Post(async () =>
            {
                if (viewModel is not null)
                {
                    await viewModel.InitializeAsync();
                }
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_composition is null)
        {
            return;
        }

        _composition.DiagnosticLog.Write(new AppEvent(EventCode.AppExiting, EventSeverity.Information));
        _composition.Dispose();
        _composition = null;
    }
}
