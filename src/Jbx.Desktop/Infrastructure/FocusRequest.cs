using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Jbx.Desktop.Infrastructure;

/// <summary>
/// Lets a view model move keyboard focus without code-behind: each change of the bound token focuses
/// the element. Uses keyboard navigation so the focus ring shows, as it should for a keyboard move.
/// </summary>
internal static class FocusRequest
{
    public static readonly AttachedProperty<int> TokenProperty =
        AvaloniaProperty.RegisterAttached<InputElement, int>("Token", typeof(FocusRequest));

    static FocusRequest()
    {
        TokenProperty.Changed.AddClassHandler<InputElement>((element, change) =>
        {
            if (change.NewValue is int token && token > 0)
            {
                Dispatcher.UIThread.Post(() => FocusElement(element), DispatcherPriority.Input);
            }
        });
    }

    public static int GetToken(InputElement element)
    {
        return element.GetValue(TokenProperty);
    }

    public static void SetToken(InputElement element, int value)
    {
        element.SetValue(TokenProperty, value);
    }

    private static void FocusElement(InputElement element)
    {
        if (element is ListBox list)
        {
            var target = list.ContainerFromIndex(Math.Max(list.SelectedIndex, 0));
            if (target is InputElement container)
            {
                container.Focus(NavigationMethod.Tab);
                return;
            }
        }

        element.Focus(NavigationMethod.Tab);
        if (element is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }
}
