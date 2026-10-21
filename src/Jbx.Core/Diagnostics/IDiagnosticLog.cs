namespace Jbx.Core.Diagnostics;

/// <summary>Never throws and never blocks the caller on I/O.</summary>
public interface IDiagnosticLog
{
    void Write(AppEvent appEvent);

    void Write(AppEvent appEvent, ResourceDetail detail);
}
