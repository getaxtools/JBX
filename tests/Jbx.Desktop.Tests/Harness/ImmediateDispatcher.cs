using Avalonia.Threading;
using Jbx.Desktop.Infrastructure;

namespace Jbx.Desktop.Tests.Harness;

internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
