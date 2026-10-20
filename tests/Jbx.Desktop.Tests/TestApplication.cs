using Avalonia;
using Avalonia.Headless;
using Jbx.Desktop.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]

namespace Jbx.Desktop.Tests;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
