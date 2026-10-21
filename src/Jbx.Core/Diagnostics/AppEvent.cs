namespace Jbx.Core.Diagnostics;

/// <summary>
/// What the program did, in codes and numbers only, so it can never carry a path or host name.
/// Anything describing a resource belongs in <see cref="ResourceDetail"/>.
/// </summary>
public sealed record AppEvent(EventCode Code, EventSeverity Severity, IReadOnlyList<EventMeasure> Measures)
{
    public AppEvent(EventCode code, EventSeverity severity, params EventMeasure[] measures)
        : this(code, severity, (IReadOnlyList<EventMeasure>)measures)
    {
    }
}
