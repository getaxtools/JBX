using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Jbx.Desktop.Infrastructure;

/// <summary>
/// Dragging an item of a list along the list moves it, as with browser tabs. The command receives a
/// <see cref="ReorderRequest"/> each time the dragged item passes another one.
/// </summary>
internal static class DragReorder
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, ICommand?>("Command", typeof(DragReorder));

    private const double DragThreshold = 6;

    private static readonly AttachedProperty<DragState?> StateProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, DragState?>("State", typeof(DragReorder));

    static DragReorder()
    {
        CommandProperty.Changed.AddClassHandler<ItemsControl>((list, change) =>
        {
            list.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
            list.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
            list.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
            list.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
            if (change.NewValue is not null)
            {
                // Tunnel and handled-too: the list item handles the press itself to select.
                list.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
                list.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
                list.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
                list.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
            }
        });
    }

    public static ICommand? GetCommand(ItemsControl list)
    {
        return list.GetValue(CommandProperty);
    }

    public static void SetCommand(ItemsControl list, ICommand? value)
    {
        list.SetValue(CommandProperty, value);
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is ItemsControl list && e.GetCurrentPoint(list).Properties.IsLeftButtonPressed && IndexUnder(list, e.Source) is { } index)
        {
            list.SetValue(StateProperty, new DragState(index, e.GetPosition(list)));
        }
    }

    private static void OnMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not ItemsControl list || list.GetValue(StateProperty) is not { } state || GetCommand(list) is not { } command)
        {
            return;
        }

        // A release the list never saw, such as after Alt+Tab mid-drag, must not leave hovering reordering tabs.
        if (!e.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
        {
            list.SetValue(StateProperty, null);
            return;
        }

        var position = e.GetPosition(list);
        if (!state.IsDragging && Point.Distance(position, state.Start) < DragThreshold)
        {
            return;
        }

        if (!state.IsDragging)
        {
            // The pressed item's container moves while dragging; capture on the list keeps the drag, and the release, here.
            e.Pointer.Capture(list);
            state.IsDragging = true;
        }

        if (IndexUnder(list, list.InputHitTest(position)) is { } target && target != state.Index)
        {
            var request = new ReorderRequest(state.Index, target);
            if (command.CanExecute(request))
            {
                command.Execute(request);
                state.Index = target;
            }
        }
    }

    private static void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is ItemsControl list && list.GetValue(StateProperty) is { } state)
        {
            list.SetValue(StateProperty, null);
            if (state.IsDragging)
            {
                e.Pointer.Capture(null);
            }
        }
    }

    private static void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (sender is ItemsControl list && list.GetValue(StateProperty) is { IsDragging: true })
        {
            list.SetValue(StateProperty, null);
        }
    }

    private static int? IndexUnder(ItemsControl list, object? source)
    {
        var container = (source as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(control => list.IndexFromContainer(control) >= 0);
        return container is null ? null : list.IndexFromContainer(container);
    }

    private sealed class DragState(int index, Point start)
    {
        public int Index { get; set; } = index;

        public Point Start { get; } = start;

        public bool IsDragging { get; set; }
    }
}
