using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Jbx.Desktop.Features.FolderList;

namespace Jbx.Desktop.Tests.Harness;

internal static class WindowQueries
{
    public static T ByAutomationId<T>(this Window window, string automationId)
        where T : Control
    {
        return window.GetVisualDescendants()
            .OfType<T>()
            .Single(control => AutomationProperties.GetAutomationId(control) == automationId);
    }

    public static bool IsShown(this Control control)
    {
        return control.GetSelfAndVisualAncestors().OfType<Control>().All(ancestor => ancestor.IsVisible);
    }

    public static void Settle(this Window window)
    {
        for (var pass = 0; pass < 5; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    public static T ByName<T>(this Window window, string name)
        where T : Control
    {
        return window.GetVisualDescendants()
            .OfType<T>()
            .Single(control => AutomationProperties.GetName(control) == name && control.IsShown());
    }

    public static ListBoxItem RowItem(this Window window, string name)
    {
        return window.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Single(item => item.DataContext is RowViewModel row && row.Name == name);
    }

    /// <summary>Lets layout and posted work run until the condition holds. Stays on the UI thread, unlike Eventually.</summary>
    public static async Task SettleUntilAsync(this Window window, Func<bool> condition)
    {
        var limit = TimeSpan.FromSeconds(5);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            window.Settle();
            if (condition())
            {
                return;
            }

            if (stopwatch.Elapsed > limit)
            {
                throw new TimeoutException($"Condition still false after {limit}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }
    }

    public static void Press(this Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        window.Settle();
    }

    public static void Click(this Window window, Control control)
    {
        var centre = CentreOf(window, control);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        window.Settle();
    }

    /// <summary>
    /// A real first click, then the double-tap Avalonia raises on the second press, sent directly: a
    /// loaded test machine can space two simulated clicks further apart than the double-click time.
    /// </summary>
    public static void DoubleClick(this Window window, Control control)
    {
        var centre = CentreOf(window, control);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        window.Settle();
        var target = window.InputHitTest(centre) as Control ?? control;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed);
        var press = new PointerEventArgs(InputElement.PointerPressedEvent, target, pointer, window, centre, 0, properties, KeyModifiers.None);
        target.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, press));
        window.Settle();
    }

    private static Point CentreOf(Window window, Control control)
    {
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The control is not in the window.");
    }
}
