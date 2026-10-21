namespace Jbx.Core.Templating;

public sealed record PathTemplate(string Text)
{
    public override string ToString()
    {
        return Text;
    }
}
