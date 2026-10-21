using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Jbx.Desktop.Infrastructure;

/// <summary>Lets a view model bring a list item into view, such as the next match or the end of a followed log.</summary>
internal static class ScrollRequest
{
    public static readonly AttachedProperty<ScrollTarget?> TargetProperty =
        AvaloniaProperty.RegisterAttached<ListBox, ScrollTarget?>("Target", typeof(ScrollRequest));

    static ScrollRequest()
    {
        TargetProperty.Changed.AddClassHandler<ListBox>((list, change) =>
        {
            if (change.NewValue is ScrollTarget target)
            {
                Dispatcher.UIThread.Post(() => ScrollTo(list, target.Index), DispatcherPriority.Background);
            }
        });
    }

    public static ScrollTarget? GetTarget(ListBox list)
    {
        return list.GetValue(TargetProperty);
    }

    public static void SetTarget(ListBox list, ScrollTarget? value)
    {
        list.SetValue(TargetProperty, value);
    }

    private static void ScrollTo(ListBox list, int index)
    {
        if (index >= 0 && index < list.ItemCount)
        {
            list.ScrollIntoView(index);
        }
    }
}
