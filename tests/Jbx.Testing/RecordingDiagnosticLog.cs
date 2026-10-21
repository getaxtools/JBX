using System.Collections.Concurrent;
using Jbx.Core.Diagnostics;

namespace Jbx.Testing;

public sealed class RecordingDiagnosticLog : IDiagnosticLog
{
    private readonly ConcurrentQueue<(AppEvent Event, ResourceDetail? Detail)> _entries = new();

    public IReadOnlyList<AppEvent> Events => _entries.Select(entry => entry.Event).ToList();

    public IReadOnlyList<ResourceDetail?> Details => _entries.Select(entry => entry.Detail).ToList();

    public void Write(AppEvent appEvent)
    {
        _entries.Enqueue((appEvent, null));
    }

    public void Write(AppEvent appEvent, ResourceDetail detail)
    {
        _entries.Enqueue((appEvent, detail));
    }

    public bool Contains(EventCode code)
    {
        return _entries.Any(entry => entry.Event.Code == code);
    }
}
