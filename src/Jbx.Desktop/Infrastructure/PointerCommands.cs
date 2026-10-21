using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Jbx.Desktop.Infrastructure;

/// <summary>Click, double-click and middle-click as commands, so views stay free of event handlers.</summary>
internal static class PointerCommands
{
    public static readonly AttachedProperty<ICommand?> TapCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("TapCommand", typeof(PointerCommands));

    public static readonly AttachedProperty<ICommand?> DoubleTapCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("DoubleTapCommand", typeof(PointerCommands));

    public static readonly AttachedProperty<ICommand?> MiddleClickCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("MiddleClickCommand", typeof(PointerCommands));

    public static readonly AttachedProperty<object?> CommandParameterProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("CommandParameter", typeof(PointerCommands));

    static PointerCommands()
    {
        TapCommandProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.Tapped -= OnTapped;
            if (change.NewValue is not null)
            {
                control.Tapped += OnTapped;
            }
        });
        DoubleTapCommandProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.DoubleTapped -= OnDoubleTapped;
            if (change.NewValue is not null)
            {
                control.DoubleTapped += OnDoubleTapped;
            }
        });
        MiddleClickCommandProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.PointerReleased -= OnPointerReleased;
            if (change.NewValue is not null)
            {
                control.PointerReleased += OnPointerReleased;
            }
        });
    }

    public static ICommand? GetTapCommand(Control control)
    {
        return control.GetValue(TapCommandProperty);
    }

    public static void SetTapCommand(Control control, ICommand? value)
    {
        control.SetValue(TapCommandProperty, value);
    }

    public static ICommand? GetDoubleTapCommand(Control control)
    {
        return control.GetValue(DoubleTapCommandProperty);
    }

    public static void SetDoubleTapCommand(Control control, ICommand? value)
    {
        control.SetValue(DoubleTapCommandProperty, value);
    }

    public static ICommand? GetMiddleClickCommand(Control control)
    {
        return control.GetValue(MiddleClickCommandProperty);
    }

    public static void SetMiddleClickCommand(Control control, ICommand? value)
    {
        control.SetValue(MiddleClickCommandProperty, value);
    }

    public static object? GetCommandParameter(Control control)
    {
        return control.GetValue(CommandParameterProperty);
    }

    public static void SetCommandParameter(Control control, object? value)
    {
        control.SetValue(CommandParameterProperty, value);
    }

    private static void OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && Execute(GetTapCommand(control), GetCommandParameter(control)))
        {
            e.Handled = true;
        }
    }

    private static void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && Execute(GetDoubleTapCommand(control), GetCommandParameter(control)))
        {
            e.Handled = true;
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control control && e.InitialPressMouseButton == MouseButton.Middle
            && Execute(GetMiddleClickCommand(control), GetCommandParameter(control)))
        {
            e.Handled = true;
        }
    }

    private static bool Execute(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) != true)
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }
}
