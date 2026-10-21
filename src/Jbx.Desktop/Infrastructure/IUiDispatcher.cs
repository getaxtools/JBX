namespace Jbx.Desktop.Infrastructure;

internal interface IUiDispatcher
{
    void Post(Action action);
}
