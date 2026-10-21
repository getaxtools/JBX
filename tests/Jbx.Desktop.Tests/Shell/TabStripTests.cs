using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Jbx.Desktop.Tests.Harness;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

public sealed class TabStripTests : IDisposable
{
    private readonly TestShell _shell = new();

    public void Dispose()
    {
        _shell.Dispose();
    }

    [AvaloniaFact]
    public async Task Ctrl_shift_page_down_and_up_move_the_tab_and_keep_it_selected()
    {
        var window = await OpenTwoTabsAsync();
        _shell.ViewModel.SelectedTab = _shell.ViewModel.Tabs[0];

        window.Press(Key.PageDown, RawInputModifiers.Control | RawInputModifiers.Shift);
        var afterRight = Order();
        window.Press(Key.PageUp, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.Equal(["prod", "dev"], afterRight);
        Assert.Equal(["dev", "prod"], Order());
        Assert.Equal("dev", _shell.ViewModel.SelectedTab?.Environment.Id);
    }

    [AvaloniaFact]
    public async Task Dragging_a_tab_over_another_moves_it_there()
    {
        var window = await OpenTwoTabsAsync();
        var strip = window.ByAutomationId<ListBox>("Shell.TabStrip");
        var first = CentreOf(window, strip.ContainerFromIndex(0)!);
        var second = CentreOf(window, strip.ContainerFromIndex(1)!);

        window.MouseDown(first, MouseButton.Left);
        window.MouseMove(new Point((first.X + second.X) / 2, first.Y), RawInputModifiers.LeftMouseButton);
        window.MouseMove(second, RawInputModifiers.LeftMouseButton);
        window.MouseUp(second, MouseButton.Left);
        window.Settle();

        Assert.Equal(["prod", "dev"], Order());
    }

    private async Task<Window> OpenTwoTabsAsync()
    {
        _shell.AddConfig();
        _shell.RememberConfig();
        var window = _shell.CreateWindow();
        window.Show();
        await _shell.ViewModel.InitializeAsync();
        _shell.Interaction.EnvironmentToPick = "prod";
        await _shell.ViewModel.NewTabCommand.ExecuteAsync(null);
        window.Settle();
        return window;
    }

    private List<string> Order()
    {
        return [.. _shell.ViewModel.Tabs.Select(tab => tab.Environment.Id)];
    }

    private static Point CentreOf(Window window, Control control)
    {
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
    }
}
