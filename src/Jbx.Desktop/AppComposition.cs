using Jbx.Desktop.Features.Shell;

namespace Jbx.Desktop;

internal static class AppComposition
{
    public static MainWindow CreateMainWindow()
    {
        return new MainWindow { DataContext = new MainWindowViewModel() };
    }
}
