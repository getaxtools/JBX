namespace Jbx.Desktop.Features.Dialogs;

internal sealed record ConfigProblemLine(string Message, string Where)
{
    public bool HasWhere => Where.Length > 0;

    public override string ToString()
    {
        return HasWhere ? $"{Message} ({Where})" : Message;
    }
}
