using Avalonia;
using Avalonia.Headless;
using Jbx.Desktop.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]

// Recreating the application for every test intermittently fails inside Avalonia's render loop setup.
// Each test builds its own shell and services and resets the theme it changes, so one application is enough.
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

// Sharing one application means sharing one UI thread and one mouse, so tests must not interleave.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Jbx.Desktop.Tests;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
