using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Jbx.Desktop.Resources;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void Shows_the_no_config_message_when_started_without_a_config()
    {
        var window = AppComposition.CreateMainWindow();

        window.Show();

        var message = FindByAutomationId<TextBlock>(window, "Shell.EmptyState.Title");
        Assert.Equal(Strings.Shell_EmptyState_Title, message.Text);
    }

    [AvaloniaFact]
    public void Window_title_names_the_product_so_the_taskbar_and_alt_tab_show_it()
    {
        var window = AppComposition.CreateMainWindow();

        window.Show();

        Assert.Equal(Strings.Shell_Window_Title, window.Title);
    }

    [AvaloniaFact]
    public void Corner_radii_follow_windows_11_rather_than_the_fluent_theme_defaults()
    {
        var window = AppComposition.CreateMainWindow();

        window.Show();

        Assert.Equal(new CornerRadius(4), FindResource<CornerRadius>(window, "ControlCornerRadius"));
        Assert.Equal(new CornerRadius(8), FindResource<CornerRadius>(window, "OverlayCornerRadius"));
    }

    private static T FindByAutomationId<T>(Window window, string automationId)
        where T : Control
    {
        return window.GetVisualDescendants()
            .OfType<T>()
            .Single(control => AutomationProperties.GetAutomationId(control) == automationId);
    }

    private static T FindResource<T>(Window window, string key)
    {
        Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var value));
        return Assert.IsType<T>(value);
    }
}
