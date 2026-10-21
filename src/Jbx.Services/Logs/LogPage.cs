using Jbx.Platform;

namespace Jbx.Services.Logs;

public sealed record LogPage(Reachability Reachability, IReadOnlyList<LogLine> Lines);
