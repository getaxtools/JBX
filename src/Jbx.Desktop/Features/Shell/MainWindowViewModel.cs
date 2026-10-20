using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Features.Shell;

internal sealed class MainWindowViewModel
{
    public string WindowTitle { get; } = Strings.Shell_Window_Title;

    public string EmptyStateTitle { get; } = Strings.Shell_EmptyState_Title;
}
